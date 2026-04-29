#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# flake8: noqa
#
# Copyright 2023 Herman Ye @Auromix
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
#
# Description:
# This example demonstrates simulating function calls for any robot,
# such as controlling velocity and other service commands.
# By modifying the content of this file,
# A calling interface can be created for the function calls of any robot.
# The Python script creates a ROS 2 Node
# that controls the movement of the TurtleSim
# by creating a publisher for cmd_vel messages and a client for the reset service.
# It also includes a ChatGPT function call server
# that can call various functions to control the TurtleSim
# and return the result of the function call as a string.
#
# Author: Herman Ye @Auromix

# ROS related
import rclpy
from rclpy.node import Node
from llm_interfaces.srv import ChatGPT
from llm_interfaces.msg import GraspCommand, GraspFeedback
from std_msgs.msg import Float64MultiArray, MultiArrayDimension, MultiArrayLayout
from std_srvs.srv import Empty

# LLM related
import json
import os
from llm_config.user_config import UserConfig

# Global Initialization
config = UserConfig()


class ArmRobot(Node):
    def __init__(self):
        super().__init__("arm_robot")

        # Publisher for target_pose
        self.target_pose_publisher = self.create_publisher(
            Float64MultiArray, "/target_pose", 10
        )
        self.grasp_feedback_publisher = self.create_publisher(
            GraspFeedback, "/grasp_feedback", 10
        )
        self.grasp_command_subscriber = self.create_subscription(
            GraspCommand, "/grasp_command", self.grasp_command_callback, 10
        )

        # Server for function call
        self.function_call_server = self.create_service(
            ChatGPT, "/ChatGPT_function_call_service", self.function_call_callback
        )
        # Node initialization log
        self.get_logger().info("ArmRobot node has been initialized")

    def function_call_callback(self, request, response):
        req = json.loads(request.request_text)
        function_name = req["name"]
        function_args = json.loads(req["arguments"])
        func_obj = getattr(self, function_name)
        try:
            function_execution_result = func_obj(**function_args)
        except Exception as error:
            self.get_logger().info(f"Failed to call function: {error}")
            response.response_text = str(error)
        else:
            response.response_text = str(function_execution_result)
        return response

    def grasp_command_callback(self, msg):
        feedback = self.execute_grasp_command(msg)
        self.grasp_feedback_publisher.publish(feedback)

    def execute_grasp_command(self, grasp_msg):
        """
        Execute a grasp command and return feedback.
        """
        feedback = GraspFeedback()
        force = float(grasp_msg.force)
        object_type = str(grasp_msg.object_type)
        is_fragile = bool(grasp_msg.is_fragile)

        if force <= 0.0:
            feedback.success = False
            feedback.actual_force = 0.0
            feedback.status = "Invalid force: force must be > 0."
            self.get_logger().info(feedback.status)
            return feedback

        recommended_force = min(force, 5.0 if is_fragile else 20.0)
        feedback.success = True
        feedback.actual_force = float(recommended_force)
        feedback.status = (
            f"Grasp executed: object_type={object_type}, "
            f"is_fragile={is_fragile}, applied_force={recommended_force:.2f}"
        )
        self.get_logger().info(feedback.status)
        return feedback

    def grasp(self, **kwargs):
        """
        Convert validator-passed JSON arguments to GraspCommand and execute.
        """
        grasp_command = GraspCommand()
        grasp_command.force = float(kwargs.get("force", 0.0))
        grasp_command.object_type = str(kwargs.get("object_type", "unknown"))
        grasp_command.is_fragile = bool(kwargs.get("is_fragile", False))

        feedback = self.execute_grasp_command(grasp_command)
        self.grasp_feedback_publisher.publish(feedback)
        return feedback.status

    def publish_target_pose(self, **kwargs):
        """
        Publishes target_pose message to control the movement of arx5_arm
        """

        x_value = kwargs.get("x", 0.2)
        y_value = kwargs.get("y", 0.2)
        z_value = kwargs.get("z", 0.2)

        roll_value = kwargs.get("roll", 0.2)
        pitch_value = kwargs.get("pitch", 0.2)
        yaw_value = kwargs.get("yaw", 0.2)

        pose = [x_value, y_value, z_value, roll_value, pitch_value, yaw_value]
        pose_str = ', '.join(map(str, pose))

        command=f"ros2 topic pub /target_pose std_msgs/msg/Float64MultiArray '{{data: [{pose_str}]}}' -1"
        os.system(command)
        self.get_logger().info(f"Published target message successfully: {pose}")
        return pose_str



def main():
    rclpy.init()
    arm_robot = ArmRobot()
    rclpy.spin(arm_robot)
    rclpy.shutdown()


if __name__ == "__main__":
    main()
