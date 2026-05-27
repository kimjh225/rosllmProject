using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.BasicCapstone;
using System.Collections;

public class RobotArmController : MonoBehaviour
{
    [Header("관절 순서: shoulder → arm → elbow → forearm → wrist → hand")]
    public ArticulationBody[] joints = new ArticulationBody[6];

    [Header("End Effector")]
    public Transform endEffector;

    [Header("Target Objects")]
    public GameObject targetObject;
    public GameObject targetPlacement;

    [Header("Gripper")]
    public Transform gripperLeft;
    public Transform gripperRight;

    [Tooltip("그리퍼가 열린 상태의 local X 거리")]
    public float gripOpenX = 0.03f;

    [Tooltip("그리퍼가 닫힌 상태의 local X 거리")]
    public float gripCloseX = 0.003f;

    [Header("Object Attach Offset")]
    [Tooltip("물체가 tool_link 기준으로 붙을 위치입니다. 물체가 떠 있거나 어긋나면 이 값을 조정하세요.")]
    public Vector3 attachLocalPosition = new Vector3(0f, 0f, 0.08f);

    [Tooltip("물체를 놓을 때 TargetPlacement보다 얼마나 위에 둘지 정합니다.")]
    public float placeHeightOffset = 0.03f;

    [Header("Pose 설정")]
    public float[] homePose = new float[6];
    public float[] pickPose = new float[6];
    public float[] liftPose = new float[6];
    public float[] placePose = new float[6];

    [Header("Move Settings")]
    [Tooltip("기본 이동 시간입니다. 실제 이동 시간은 moveTime / move_speed 로 계산됩니다.")]
    public float moveTime = 2.0f;

    [Tooltip("ROS 명령 속도 최솟값")]
    public float minCommandSpeed = 0.3f;

    [Tooltip("ROS 명령 속도 최댓값")]
    public float maxCommandSpeed = 3.0f;

    [Tooltip("조심스럽게 움직일 때 그리퍼 동작 시간 배율")]
    public float gentleGripperMultiplier = 1.5f;

    [Tooltip("빠르게 움직일 때 그리퍼 동작 시간 배율")]
    public float fastGripperMultiplier = 0.7f;

    [Header("Joint Lock")]
    public bool lockJoints = true;

    [Header("Reset Settings")]
    [Tooltip("명령을 받을 때마다 Target을 처음 위치로 되돌린 후 Pick & Place를 시작합니다.")]
    public bool resetTargetBeforeCommand = false;

    [Tooltip("명령을 받을 때마다 로봇을 Home Pose로 먼저 보정한 뒤 시작합니다.")]
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
                Debug.LogWarning("Target Object가 비어 있습니다. 명령 수신 시 object_type 이름으로 다시 찾습니다.");
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
                Debug.LogWarning("TargetPlacement가 연결되지 않았습니다. 놓을 때 위치 보정이 제한됩니다.");
            }
        }

        if (gripperLeft == null)
        {
            Debug.LogWarning("Gripper Left가 비어 있습니다. 그리퍼 애니메이션은 생략될 수 있습니다.");
        }

        if (gripperRight == null)
        {
            Debug.LogWarning("Gripper Right가 비어 있습니다. 그리퍼 애니메이션은 생략될 수 있습니다.");
        }

        return true;
    }

    private void SaveInitialTargetPose()
    {
        GameObject target = targetObject;

        if (target == null)
        {
            target = GameObject.Find("Target");
        }

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

        Debug.Log(
            $"✅ 명령 수신: object_type={msg.object_type}, " +
            $"force={msg.force}, is_fragile={msg.is_fragile}, " +
            $"move_speed={msg.move_speed}, motion_style={msg.motion_style}, " +
            $"applied_speed={currentMoveSpeed}"
        );

        GameObject target = ResolveTargetObject(msg.object_type);

        if (target == null)
        {
            Debug.LogWarning($"잡을 물체를 찾을 수 없음: {msg.object_type}");
            PublishFeedback(false, msg.force, "object_not_found");
            return;
        }

        if (targetPlacement == null)
        {
            targetPlacement = GameObject.Find("TargetPlacement");
        }

        Debug.Log($"Target 위치: {target.transform.position}");

        if (targetPlacement != null)
        {
            Debug.Log($"TargetPlacement 위치: {targetPlacement.transform.position}");
        }
        else
        {
            Debug.LogWarning("TargetPlacement를 찾을 수 없습니다. place 위치 보정 없이 진행합니다.");
        }

        StartCoroutine(PickAndPlace(target, msg));
    }

    private void ApplyMotionCommand(GraspCommandMsg msg)
    {
        string style = string.IsNullOrEmpty(msg.motion_style)
            ? "normal"
            : msg.motion_style.ToLower().Trim();

        float speed = msg.move_speed;

        if (speed <= 0f)
        {
            speed = 1.0f;
        }

        if (style != "gentle" && style != "normal" && style != "fast")
        {
            style = "normal";
        }

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
        {
            return targetObject;
        }

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

    private IEnumerator PickAndPlace(GameObject target, GraspCommandMsg msg)
    {
        isBusy = true;

        if (resetTargetBeforeCommand)
        {
            Debug.Log("명령 시작 전 Target Reset...");
            ResetTargetToInitialPose();
            yield return new WaitForSeconds(GetScaledWaitTime(0.2f));
        }

        if (moveHomeBeforeCommand)
        {
            Debug.Log("명령 시작 전 Home Pose 이동...");
            yield return StartCoroutine(MoveToPose(homePose));
        }

        Rigidbody rb = target.GetComponent<Rigidbody>();
        originalParent = target.transform.parent;

        Debug.Log($"🚀 Pick & Place 시작: motion_style={currentMotionStyle}, move_speed={currentMoveSpeed}");

        // 1. Pick 방향으로 먼저 회전
        Debug.Log("Pick 방향으로 수평 이동...");
        float[] approachPose = ClonePose(homePose);
        approachPose[0] = pickPose[0];
        yield return StartCoroutine(MoveToPose(approachPose));

        // 2. Pick 위치로 하강
        Debug.Log("Pick 위치로 하강...");
        yield return StartCoroutine(MoveToPose(pickPose));

        // 3. 그리퍼 닫기
        Debug.Log("집는 중...");
        yield return StartCoroutine(AnimateGripper(gripOpenX, gripCloseX));

        // 4. 물체 부착
        Debug.Log("물체 부착 위치 보정...");

        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        target.transform.SetParent(endEffector, false);
        target.transform.localPosition = attachLocalPosition;
        target.transform.localRotation = Quaternion.identity;

        // 5. 들어올리기
        Debug.Log("들어올리는 중...");
        yield return StartCoroutine(MoveToPose(liftPose));

        // 6. Place 방향으로 수평 이동
        Debug.Log("Place 방향으로 수평 이동...");
        float[] horizontalPose = ClonePose(liftPose);
        horizontalPose[0] = placePose[0];
        yield return StartCoroutine(MoveToPose(horizontalPose));

        // 7. Place 위치로 하강
        Debug.Log("Place 위치로 하강...");
        yield return StartCoroutine(MoveToPose(placePose));

        // 8. 놓기
        Debug.Log("놓는 중...");

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
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        yield return StartCoroutine(AnimateGripper(gripCloseX, gripOpenX));

        // 9. 다시 들어올리기
        Debug.Log("올라오는 중...");
        float[] liftAfterPlace = ClonePose(placePose);
        liftAfterPlace[1] = liftPose[1];
        yield return StartCoroutine(MoveToPose(liftAfterPlace));

        // 10. Home 방향으로 수평 이동
        Debug.Log("Home 방향으로 수평 이동...");
        float[] returnPose = ClonePose(liftAfterPlace);
        returnPose[0] = homePose[0];
        yield return StartCoroutine(MoveToPose(returnPose));

        // 11. Home 복귀
        Debug.Log("Home으로 복귀...");
        yield return StartCoroutine(MoveToPose(homePose));

        PublishFeedback(true, msg.force, $"success:{currentMotionStyle}:{currentMoveSpeed}");

        Debug.Log("✅ Pick & Place 완료");
        isBusy = false;
    }

    private float[] ClonePose(float[] source)
    {
        float[] result = new float[joints.Length];

        for (int i = 0; i < result.Length; i++)
        {
            if (source != null && i < source.Length)
            {
                result[i] = source[i];
            }
            else
            {
                result[i] = 0f;
            }
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
        {
            duration *= gentleGripperMultiplier;
        }
        else if (currentMotionStyle == "fast")
        {
            duration *= fastGripperMultiplier;
        }

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
        {
            target = GameObject.Find("Target");
        }

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

        Rigidbody rb = target.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        target.transform.SetParent(originalParent, true);
        target.transform.position = initialTargetPosition;
        target.transform.rotation = initialTargetRotation;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        Debug.Log($"✅ Target Reset 완료: {initialTargetPosition}");
    }

    [ContextMenu("Save Current Target Pose As Initial")]
    public void SaveCurrentTargetPoseAsInitial()
    {
        GameObject target = targetObject;

        if (target == null)
        {
            target = GameObject.Find("Target");
        }

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
