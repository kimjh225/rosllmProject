using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.BasicCapstone;
using System.Collections;

public enum UnityTaskMode
{
    PickAndPlace,
    PickAndCarryOnly,
    PickMoveAndReturnObject
}

public class RobotArmController : MonoBehaviour
{
    [Header("관절 순서: shoulder → arm → elbow → forearm → wrist → hand")]
    public ArticulationBody[] joints = new ArticulationBody[6];

    [Header("End Effector")]
    public Transform endEffector;

    [Header("Target Objects")]
    public GameObject targetObject;
    public GameObject targetPlacement;

    [Header("Task Mode")]
    public UnityTaskMode taskMode = UnityTaskMode.PickAndPlace;

    [Header("Realistic Grasp Check")]
    public bool requireGraspDistanceCheck = true;
    public float graspAttachDistance = 0.13f;
    public bool cleanupPreviousAttachmentOnNewCommand = true;

    [Header("Release Behavior")]
    public bool snapToTargetPlacementOnRelease = true;
    public bool snapToOriginalPoseOnReturn = true;
    public float placeHeightOffset = 0.03f;

    [Header("Mode Wait")]
    public float carryOnlyHoldSeconds = 1.0f;
    public float returnModeWaitSeconds = 0.8f;

    [Header("Gripper")]
    public Transform gripperLeft;
    public Transform gripperRight;
    public float gripOpenX = 0.03f;
    public float gripCloseX = 0.003f;

    [Header("Object Attach Offset")]
    public Vector3 attachLocalPosition = new Vector3(0f, 0f, 0.08f);

    [Header("Pose 설정")]
    public float[] homePose = new float[6];
    public float[] pickPose = new float[6];
    public float[] liftPose = new float[6];
    public float[] placePose = new float[6];

    [Header("Move Settings")]
    public float moveTime = 2.0f;
    public float minCommandSpeed = 0.3f;
    public float maxCommandSpeed = 3.0f;
    public float gentleGripperMultiplier = 1.5f;
    public float fastGripperMultiplier = 0.7f;

    [Header("Joint Lock")]
    public bool lockJoints = true;

    [Header("Reset Settings")]
    public bool resetTargetBeforeCommand = false;
    public bool moveHomeBeforeCommand = false;

    private ROSConnection ros;
    private bool isBusy = false;
    private float[] currentTargets;

    private Transform originalParent;
    private Vector3 initialTargetPosition;
    private Quaternion initialTargetRotation;
    private bool hasInitialTargetPose = false;

    private float currentMoveSpeed = 1.0f;
    private string currentMotionStyle = "normal";

    private GameObject currentAttachedObject;
    private Transform currentAttachedOriginalParent;

    IEnumerator Start()
    {
        ros = ROSConnection.GetOrCreateInstance();

        ros.Subscribe<GraspCommandMsg>("/grasp_command", OnGraspCommand);
        ros.RegisterPublisher<GraspFeedbackMsg>("/grasp_feedback");

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        if (!ValidateRequiredReferences())
        {
            Debug.LogError("필수 슬롯 연결이 부족합니다. Inspector 연결을 확인하세요.");
            yield break;
        }

        SaveInitialTargetPose();
        InitializeJointDrives();

        Debug.Log("✅ RobotArmController 준비 완료");
        Debug.Log($"🧩 현재 Unity Task Mode: {taskMode}");
    }

    private bool ValidateRequiredReferences()
    {
        if (joints == null || joints.Length == 0)
        {
            Debug.LogError("Joints 배열이 비어 있습니다.");
            return false;
        }

        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i] == null)
            {
                Debug.LogError($"Joints Element {i}가 비어 있습니다.");
                return false;
            }
        }

        if (endEffector == null)
        {
            Debug.LogError("End Effector가 비어 있습니다. tool_link를 연결하세요.");
            return false;
        }

        if (targetObject == null)
        {
            GameObject foundTarget = GameObject.Find("Target");
            if (foundTarget != null)
            {
                targetObject = foundTarget;
                Debug.Log("Target Object 자동 연결: Target");
            }
            else
            {
                Debug.LogWarning("Target Object가 비어 있습니다.");
            }
        }

        if (targetPlacement == null)
        {
            GameObject foundPlacement = GameObject.Find("TargetPlacement");
            if (foundPlacement != null)
            {
                targetPlacement = foundPlacement;
                Debug.Log("Target Placement 자동 연결: TargetPlacement");
            }
            else
            {
                Debug.LogWarning("TargetPlacement가 연결되지 않았습니다.");
            }
        }

        return true;
    }

    private void SaveInitialTargetPose()
    {
        GameObject target = targetObject;

        if (target == null)
            target = GameObject.Find("Target");

        if (target == null)
        {
            Debug.LogWarning("초기 Target 위치 저장 실패: Target을 찾을 수 없습니다.");
            hasInitialTargetPose = false;
            return;
        }

        targetObject = target;
        originalParent = target.transform.parent;

        initialTargetPosition = target.transform.position;
        initialTargetRotation = target.transform.rotation;
        hasInitialTargetPose = true;

        Debug.Log($"✅ Target 초기 위치 저장: {initialTargetPosition}");
    }

    private void InitializeJointDrives()
    {
        currentTargets = new float[joints.Length];

        for (int i = 0; i < joints.Length; i++)
        {
            ArticulationDrive drive = joints[i].xDrive;

            drive.stiffness = 100000f;
            drive.damping = 1000f;
            drive.forceLimit = 100000f;

            float currentAngle = joints[i].jointPosition[0] * Mathf.Rad2Deg;

            drive.target = currentAngle;
            joints[i].xDrive = drive;

            currentTargets[i] = currentAngle;
        }
    }

    void FixedUpdate()
    {
        if (!lockJoints) return;
        if (isBusy) return;
        if (currentTargets == null) return;

        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i] == null) continue;

            ArticulationDrive drive = joints[i].xDrive;
            drive.target = currentTargets[i];
            joints[i].xDrive = drive;
        }
    }

    private void OnGraspCommand(GraspCommandMsg msg)
    {
        if (isBusy)
        {
            Debug.LogWarning("이미 동작 중입니다.");
            return;
        }

        ApplyMotionCommand(msg);

        GameObject target = ResolveTargetObject(msg.object_type);

        if (target == null)
        {
            Debug.LogWarning($"잡을 물체를 찾을 수 없음: {msg.object_type}");
            PublishFeedback(false, msg.force, "object_not_found");
            return;
        }

        if (targetPlacement == null)
            targetPlacement = GameObject.Find("TargetPlacement");

        Debug.Log(
            $"✅ 명령 수신: object_type={msg.object_type}, " +
            $"force={msg.force}, is_fragile={msg.is_fragile}, " +
            $"move_speed={msg.move_speed}, motion_style={msg.motion_style}, " +
            $"applied_speed={currentMoveSpeed}, taskMode={taskMode}"
        );

        StartCoroutine(ExecuteTaskMode(target, msg));
    }

    private void ApplyMotionCommand(GraspCommandMsg msg)
    {
        string style = string.IsNullOrEmpty(msg.motion_style)
            ? "normal"
            : msg.motion_style.ToLower().Trim();

        float speed = msg.move_speed;

        if (speed <= 0f)
            speed = 0.9f;

        if (style != "gentle" && style != "normal" && style != "fast")
            style = "normal";

        if (msg.is_fragile || style == "gentle")
        {
            style = "gentle";
            speed = Mathf.Clamp(speed, minCommandSpeed, 0.7f);
        }
        else if (style == "fast")
        {
            speed = Mathf.Clamp(speed, 1.5f, maxCommandSpeed);
        }
        else
        {
            style = "normal";
            speed = Mathf.Clamp(speed, 0.8f, 1.2f);
        }

        currentMotionStyle = style;
        currentMoveSpeed = speed;

        Debug.Log($"🎚️ Motion 설정 적용: style={currentMotionStyle}, speed={currentMoveSpeed}");
    }

    private GameObject ResolveTargetObject(string objectName)
    {
        if (targetObject != null)
            return targetObject;

        if (!string.IsNullOrEmpty(objectName))
        {
            GameObject foundByMsg = GameObject.Find(objectName);
            if (foundByMsg != null)
            {
                targetObject = foundByMsg;
                return foundByMsg;
            }
        }

        GameObject foundTarget = GameObject.Find("Target");
        if (foundTarget != null)
        {
            targetObject = foundTarget;
            return foundTarget;
        }

        return null;
    }

    private IEnumerator ExecuteTaskMode(GameObject target, GraspCommandMsg msg)
    {
        isBusy = true;

        if (cleanupPreviousAttachmentOnNewCommand)
        {
            CleanupPreviousAttachment();
            yield return new WaitForSeconds(GetScaledWaitTime(0.15f));
        }

        Rigidbody rb = target.GetComponent<Rigidbody>();

        Transform commandOriginalParent = target.transform.parent;
        Vector3 commandOriginalPosition = target.transform.position;
        Quaternion commandOriginalRotation = target.transform.rotation;

        originalParent = commandOriginalParent;

        Debug.Log($"🚀 작업 시작: taskMode={taskMode}, motion_style={currentMotionStyle}, move_speed={currentMoveSpeed}");
        Debug.Log($"📌 명령 시작 시 Target 위치 저장: {commandOriginalPosition}");

        if (resetTargetBeforeCommand)
        {
            Debug.Log("명령 시작 전 Target Reset...");
            ResetTargetToInitialPose();
            yield return new WaitForSeconds(GetScaledWaitTime(0.2f));

            commandOriginalParent = target.transform.parent;
            commandOriginalPosition = target.transform.position;
            commandOriginalRotation = target.transform.rotation;
            originalParent = commandOriginalParent;
        }

        if (moveHomeBeforeCommand)
        {
            Debug.Log("명령 시작 전 Home Pose 이동...");
            yield return StartCoroutine(MoveToPose(homePose));
        }

        Debug.Log("Pick 방향으로 수평 이동...");
        float[] approachPose = ClonePose(homePose);
        approachPose[0] = pickPose[0];
        yield return StartCoroutine(MoveToPose(approachPose));

        Debug.Log("Pick 위치로 하강...");
        yield return StartCoroutine(MoveToPose(pickPose));

        Debug.Log("그리퍼 닫는 중...");
        yield return StartCoroutine(AnimateGripper(gripOpenX, gripCloseX));

        Debug.Log("잡기 판정 확인...");
        bool attached = TryAttachObject(target, rb);

        if (!attached)
        {
            Debug.LogWarning("❌ 잡기 실패: 그리퍼가 물체에 충분히 가까이 가지 못했습니다.");
            yield return StartCoroutine(AnimateGripper(gripCloseX, gripOpenX));
            yield return StartCoroutine(MoveToPose(homePose));
            PublishFeedback(false, msg.force, "grasp_failed:not_close_enough");
            isBusy = false;
            yield break;
        }

        Debug.Log("들어올리는 중...");
        yield return StartCoroutine(MoveToPose(liftPose));

        Debug.Log("Place 방향으로 수평 이동...");
        float[] horizontalPose = ClonePose(liftPose);
        horizontalPose[0] = placePose[0];
        yield return StartCoroutine(MoveToPose(horizontalPose));

        Debug.Log("Place 위치로 하강...");
        yield return StartCoroutine(MoveToPose(placePose));

        switch (taskMode)
        {
            case UnityTaskMode.PickAndPlace:
                yield return StartCoroutine(CompletePickAndPlace(target, rb, msg.force));
                break;

            case UnityTaskMode.PickAndCarryOnly:
                yield return StartCoroutine(CompletePickAndCarryOnly(msg.force));
                break;

            case UnityTaskMode.PickMoveAndReturnObject:
                yield return StartCoroutine(CompletePickMoveAndReturnObject(
                    target,
                    rb,
                    commandOriginalParent,
                    commandOriginalPosition,
                    commandOriginalRotation,
                    msg.force
                ));
                break;
        }

        isBusy = false;
    }

    private IEnumerator CompletePickAndPlace(GameObject target, Rigidbody rb, float force)
    {
        Debug.Log("📦 모드: PickAndPlace - 목적지에 내려놓기");

        ReleaseObjectAtDestination(target, rb);

        yield return StartCoroutine(AnimateGripper(gripCloseX, gripOpenX));

        yield return StartCoroutine(ReturnHomeAfterPlace());

        EnableReleasedObjectPhysics(target, rb);

        PublishFeedback(true, force, $"success:pick_place:{currentMotionStyle}:{currentMoveSpeed}");

        Debug.Log("✅ PickAndPlace 완료");
    }

    private IEnumerator CompletePickAndCarryOnly(float force)
    {
        Debug.Log("📦 모드: PickAndCarryOnly - 물체를 내려놓지 않고 들고 있는 상태 유지");

        yield return new WaitForSeconds(GetScaledWaitTime(carryOnlyHoldSeconds));

        PublishFeedback(true, force, $"success:carry_only:{currentMotionStyle}:{currentMoveSpeed}");

        Debug.Log("✅ PickAndCarryOnly 완료");
        Debug.Log("ℹ️ 다음 명령이 들어오면 이전 부착 상태를 먼저 정리합니다.");
    }

    private IEnumerator CompletePickMoveAndReturnObject(
        GameObject target,
        Rigidbody rb,
        Transform commandOriginalParent,
        Vector3 commandOriginalPosition,
        Quaternion commandOriginalRotation,
        float force
    )
    {
        Debug.Log("📦 모드: PickMoveAndReturnObject - 목적지 이동 후 원래 위치로 복귀");

        yield return new WaitForSeconds(GetScaledWaitTime(returnModeWaitSeconds));

        Debug.Log("목적지에서 다시 들어올리기...");
        float[] liftAfterDestination = ClonePose(placePose);
        liftAfterDestination[1] = liftPose[1];
        yield return StartCoroutine(MoveToPose(liftAfterDestination));

        Debug.Log("원래 위치 방향으로 수평 이동...");
        float[] returnHorizontalPose = ClonePose(liftAfterDestination);
        returnHorizontalPose[0] = pickPose[0];
        yield return StartCoroutine(MoveToPose(returnHorizontalPose));

        Debug.Log("원래 위치로 하강...");
        yield return StartCoroutine(MoveToPose(pickPose));

        ReleaseObjectAtOriginalPose(
            target,
            rb,
            commandOriginalParent,
            commandOriginalPosition,
            commandOriginalRotation
        );

        yield return StartCoroutine(AnimateGripper(gripCloseX, gripOpenX));

        Debug.Log("원위치 배치 후 들어올리기...");
        yield return StartCoroutine(MoveToPose(liftPose));

        Debug.Log("Home 방향으로 수평 이동...");
        float[] returnPose = ClonePose(liftPose);
        returnPose[0] = homePose[0];
        yield return StartCoroutine(MoveToPose(returnPose));

        Debug.Log("Home으로 복귀...");
        yield return StartCoroutine(MoveToPose(homePose));

        EnableReleasedObjectPhysics(target, rb);

        PublishFeedback(true, force, $"success:return_original:{currentMotionStyle}:{currentMoveSpeed}");

        Debug.Log("✅ PickMoveAndReturnObject 완료");
    }

    private bool TryAttachObject(GameObject target, Rigidbody rb)
    {
        if (target == null || endEffector == null)
        {
            Debug.LogWarning("TryAttachObject 실패: target 또는 endEffector가 없습니다.");
            return false;
        }

        float distance = Vector3.Distance(endEffector.position, target.transform.position);

        Debug.Log($"📏 그리퍼-물체 거리: {distance:F3}, 허용 거리: {graspAttachDistance:F3}");

        if (requireGraspDistanceCheck && distance > graspAttachDistance)
        {
            Debug.LogWarning("❌ 물체가 그리퍼에서 너무 멀어서 잡지 않습니다.");
            return false;
        }

        currentAttachedObject = target;
        currentAttachedOriginalParent = target.transform.parent;

        FreezeRigidbody(rb);

        target.transform.SetParent(endEffector, true);
        target.transform.localPosition = attachLocalPosition;
        target.transform.localRotation = Quaternion.identity;

        Debug.Log($"🔗 물체 잡기 성공: localPosition={attachLocalPosition}");

        return true;
    }

    private void ReleaseObjectAtDestination(GameObject target, Rigidbody rb)
    {
        if (target == null)
        {
            Debug.LogWarning("ReleaseObjectAtDestination 실패: target이 없습니다.");
            return;
        }

        target.transform.SetParent(originalParent, true);

        if (targetPlacement != null)
        {
            Vector3 placePos = targetPlacement.transform.position;

            target.transform.position = new Vector3(
                placePos.x,
                placePos.y + placeHeightOffset,
                placePos.z
            );

            target.transform.rotation = Quaternion.identity;

            Debug.Log("📍 TargetPlacement 위치로 물체를 내려놓았습니다.");
        }
        else
        {
            Debug.LogWarning("TargetPlacement가 없어서 현재 위치에 내려놓습니다.");
        }

        FreezeRigidbody(rb);
        ClearCurrentAttachmentIfTarget(target);

        Debug.Log("🧊 물체를 잠깐 고정했습니다. 팔이 빠져나간 뒤 물리를 다시 켭니다.");
    }

    private void ReleaseObjectAtOriginalPose(
        GameObject target,
        Rigidbody rb,
        Transform commandOriginalParent,
        Vector3 commandOriginalPosition,
        Quaternion commandOriginalRotation
    )
    {
        if (target == null)
        {
            Debug.LogWarning("ReleaseObjectAtOriginalPose 실패: target이 없습니다.");
            return;
        }

        target.transform.SetParent(commandOriginalParent, true);

        target.transform.position = commandOriginalPosition;
        target.transform.rotation = commandOriginalRotation;

        Debug.Log($"📍 원래 위치로 물체를 돌려놓았습니다: {commandOriginalPosition}");

        FreezeRigidbody(rb);
        ClearCurrentAttachmentIfTarget(target);

        Debug.Log("🧊 원위치 물체를 잠깐 고정했습니다. 팔이 빠져나간 뒤 물리를 다시 켭니다.");
    }

    private void FreezeRigidbody(Rigidbody rb)
    {
        if (rb == null)
            return;

        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void EnableReleasedObjectPhysics(GameObject target, Rigidbody rb)
    {
        if (target == null)
            return;

        if (rb == null)
            rb = target.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;

            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Debug.Log("✅ 팔이 빠져나간 뒤 물체 물리를 다시 켰습니다.");
    }

    private void CleanupPreviousAttachment()
    {
        GameObject attached = currentAttachedObject;

        if (attached == null && targetObject != null && endEffector != null)
        {
            if (targetObject.transform.IsChildOf(endEffector))
            {
                attached = targetObject;
            }
        }

        if (attached == null)
            return;

        Debug.LogWarning("🧹 이전 명령에서 팔에 붙어 있던 물체를 먼저 해제합니다.");

        Rigidbody rb = attached.GetComponent<Rigidbody>();

        Transform parentToRestore = currentAttachedOriginalParent;
        if (parentToRestore == null)
            parentToRestore = originalParent;

        attached.transform.SetParent(parentToRestore, true);

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        currentAttachedObject = null;
        currentAttachedOriginalParent = null;
    }

    private void ClearCurrentAttachmentIfTarget(GameObject target)
    {
        if (currentAttachedObject == target)
        {
            currentAttachedObject = null;
            currentAttachedOriginalParent = null;
        }
    }

    private IEnumerator ReturnHomeAfterPlace()
    {
        Debug.Log("올라오는 중...");
        float[] liftAfterPlace = ClonePose(placePose);
        liftAfterPlace[1] = liftPose[1];
        yield return StartCoroutine(MoveToPose(liftAfterPlace));

        Debug.Log("Home 방향으로 수평 이동...");
        float[] returnPose = ClonePose(liftAfterPlace);
        returnPose[0] = homePose[0];
        yield return StartCoroutine(MoveToPose(returnPose));

        Debug.Log("Home으로 복귀...");
        yield return StartCoroutine(MoveToPose(homePose));
    }

    private float[] ClonePose(float[] source)
    {
        float[] result = new float[joints.Length];

        for (int i = 0; i < result.Length; i++)
        {
            if (source != null && i < source.Length)
                result[i] = source[i];
            else
                result[i] = 0f;
        }

        return result;
    }

    private IEnumerator MoveToPose(float[] targetPose)
    {
        if (targetPose == null || targetPose.Length == 0)
        {
            Debug.LogWarning("targetPose가 비어 있습니다.");
            yield break;
        }

        if (currentTargets == null)
        {
            currentTargets = new float[joints.Length];

            for (int i = 0; i < joints.Length; i++)
            {
                if (joints[i] == null) continue;
                currentTargets[i] = joints[i].xDrive.target;
            }
        }

        float[] startPose = new float[joints.Length];

        for (int i = 0; i < joints.Length; i++)
        {
            startPose[i] = currentTargets[i];
        }

        float appliedMoveTime = GetCurrentMoveTime();
        float elapsed = 0f;

        Debug.Log($"⏱️ MoveToPose: baseTime={moveTime}, appliedTime={appliedMoveTime}, speed={currentMoveSpeed}, style={currentMotionStyle}");

        while (elapsed < appliedMoveTime)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / appliedMoveTime);
            t = Mathf.SmoothStep(0f, 1f, t);

            for (int i = 0; i < joints.Length && i < targetPose.Length; i++)
            {
                if (joints[i] == null) continue;

                currentTargets[i] = Mathf.Lerp(startPose[i], targetPose[i], t);

                ArticulationDrive drive = joints[i].xDrive;
                drive.target = currentTargets[i];
                joints[i].xDrive = drive;
            }

            yield return null;
        }

        for (int i = 0; i < joints.Length && i < targetPose.Length; i++)
        {
            currentTargets[i] = targetPose[i];

            if (joints[i] == null) continue;

            ArticulationDrive drive = joints[i].xDrive;
            drive.target = currentTargets[i];
            joints[i].xDrive = drive;
        }
    }

    private float GetCurrentMoveTime()
    {
        float safeSpeed = Mathf.Clamp(currentMoveSpeed, minCommandSpeed, maxCommandSpeed);
        return Mathf.Max(0.1f, moveTime / safeSpeed);
    }

    private float GetScaledWaitTime(float baseWait)
    {
        float safeSpeed = Mathf.Clamp(currentMoveSpeed, minCommandSpeed, maxCommandSpeed);
        return Mathf.Max(0.05f, baseWait / safeSpeed);
    }

    private IEnumerator AnimateGripper(float fromX, float toX)
    {
        if (gripperLeft == null || gripperRight == null)
        {
            Debug.LogWarning("그리퍼 Transform이 연결되지 않아 그리퍼 애니메이션을 생략합니다.");
            yield break;
        }

        float duration = 0.5f;

        if (currentMotionStyle == "gentle")
            duration *= gentleGripperMultiplier;
        else if (currentMotionStyle == "fast")
            duration *= fastGripperMultiplier;

        duration = Mathf.Max(0.1f, duration);

        float elapsed = 0f;

        Vector3 leftStart = gripperLeft.localPosition;
        Vector3 rightStart = gripperRight.localPosition;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float x = Mathf.Lerp(fromX, toX, t);

            gripperLeft.localPosition = new Vector3(
                -x,
                leftStart.y,
                leftStart.z
            );

            gripperRight.localPosition = new Vector3(
                x,
                rightStart.y,
                rightStart.z
            );

            yield return null;
        }

        gripperLeft.localPosition = new Vector3(
            -toX,
            leftStart.y,
            leftStart.z
        );

        gripperRight.localPosition = new Vector3(
            toX,
            rightStart.y,
            rightStart.z
        );
    }

    private void PublishFeedback(bool success, float force, string status)
    {
        GraspFeedbackMsg feedback = new GraspFeedbackMsg
        {
            success = success,
            actual_force = force * 0.95f,
            status = status
        };

        ros.Publish("/grasp_feedback", feedback);
    }

    [ContextMenu("Reset Target To Initial Pose")]
    public void ResetTargetToInitialPose()
    {
        GameObject target = targetObject;

        if (target == null)
            target = GameObject.Find("Target");

        if (target == null)
        {
            Debug.LogWarning("Reset 실패: Target을 찾을 수 없습니다.");
            return;
        }

        if (!hasInitialTargetPose)
        {
            Debug.LogWarning("Reset 실패: 초기 Target 위치가 저장되어 있지 않습니다.");
            return;
        }

        CleanupPreviousAttachment();

        Rigidbody rb = target.GetComponent<Rigidbody>();

        FreezeRigidbody(rb);

        target.transform.SetParent(originalParent, true);
        target.transform.position = initialTargetPosition;
        target.transform.rotation = initialTargetRotation;

        EnableReleasedObjectPhysics(target, rb);

        Debug.Log($"✅ Target Reset 완료: {initialTargetPosition}");
    }

    [ContextMenu("Save Current Target Pose As Initial")]
    public void SaveCurrentTargetPoseAsInitial()
    {
        GameObject target = targetObject;

        if (target == null)
            target = GameObject.Find("Target");

        if (target == null)
        {
            Debug.LogWarning("초기 위치 저장 실패: Target을 찾을 수 없습니다.");
            return;
        }

        initialTargetPosition = target.transform.position;
        initialTargetRotation = target.transform.rotation;
        originalParent = target.transform.parent;
        hasInitialTargetPose = true;

        Debug.Log($"✅ 현재 Target 위치를 초기 위치로 저장: {initialTargetPosition}");
    }
}