#!/usr/bin/env python3
# -*- coding: utf-8 -*-
#
# Unity 시뮬레이션 연동용 launch 파일
# state_feedback_node 없이 실행 — Unity에서 /grasp_feedback 직접 발행
#
# 사전 준비:
#   sudo apt install ros-$ROS_DISTRO-ros-tcp-endpoint
#
# 사용법:
#   ros2 launch llm_bringup unity_with_arm_robot.launch.py
#   (선택) ROS_IP 지정: ros2 launch llm_bringup unity_with_arm_robot.launch.py ros_ip:=192.168.0.10
#
# Unity 설정:
#   ROS Settings → Protocol: ROS2
#   ROS Settings → ROS IP Address: 이 PC의 IP
#   ROS Settings → ROS Port: 10000

from launch import LaunchDescription
from launch.actions import DeclareLaunchArgument
from launch.substitutions import LaunchConfiguration
from launch_ros.actions import Node


def generate_launch_description():
    ros_ip_arg = DeclareLaunchArgument(
        "ros_ip",
        default_value="0.0.0.0",
        description="ROS2 서버 IP (Unity가 접속할 이 PC의 IP, 0.0.0.0은 모든 인터페이스 허용)",
    )
    ros_port_arg = DeclareLaunchArgument(
        "ros_port",
        default_value="10000",
        description="ROS-TCP-Endpoint 포트 (Unity ROS Settings의 Port와 일치해야 함)",
    )

    return LaunchDescription(
        [
            ros_ip_arg,
            ros_port_arg,

            # ROS-TCP-Endpoint: Unity ↔ ROS2 브릿지
            Node(
                package="ros_tcp_endpoint",
                executable="default_server_endpoint",
                name="ros_tcp_endpoint",
                output="screen",
                parameters=[{
                    "ROS_IP": LaunchConfiguration("ros_ip"),
                    "ROS_TCP_PORT": LaunchConfiguration("ros_port"),
                }],
            ),

            # LLM 모델 노드
            Node(
                package="llm_model",
                executable="chatgpt",
                name="chatgpt",
                output="screen",
            ),

            # 로봇팔 인터페이스 노드 (/grasp_command 발행)
            # state_feedback_node 없음 — Unity가 /grasp_feedback 직접 발행
            Node(
                package="llm_robot",
                executable="arm_robot",
                name="arm_robot",
                output="screen",
            ),
        ]
    )
