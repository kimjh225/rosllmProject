#!/usr/bin/env python3
# -*- coding: utf-8 -*-
#
# Unity 시뮬레이션 테스트용 launch 파일
# arx5_bringup(실제 로봇 하드웨어) 없이 실행 가능
#
# 사용법:
#   ros2 launch llm_bringup sim_with_arm_robot.launch.py
#
# 테스트:
#   ros2 topic pub /llm_input_audio_to_text std_msgs/msg/String \
#     "data: 'pick up the cup gently'" -1
#
# Unity 연동 시:
#   - state_feedback_node 제거하고 Unity에서 /grasp_feedback 직접 발행
#   - /grasp_command 토픽을 Unity에서 구독

from launch import LaunchDescription
from launch_ros.actions import Node


def generate_launch_description():
    return LaunchDescription(
        [
            # LLM 모델 노드
            Node(
                package="llm_model",
                executable="chatgpt",
                name="chatgpt",
                output="screen",
            ),
            # 로봇팔 인터페이스 노드 (/grasp_command 발행)
            Node(
                package="llm_robot",
                executable="arm_robot",
                name="arm_robot",
                output="screen",
            ),
            # 더미 피드백 노드 (Unity 연동 전 테스트용)
            # Unity 연동 시 이 노드 대신 Unity에서 /grasp_feedback 발행
            Node(
                package="basic_capstone",
                executable="state_feedback_node",
                name="state_feedback_node",
                output="screen",
            ),
        ]
    )
