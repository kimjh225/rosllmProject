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
from std_msgs.msg import Float64MultiArray, MultiArrayDimension, MultiArrayLayout
from std_srvs.srv import Empty
from basic_capstone.msg import GraspCommand

# LLM related
import json
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
        # Server for function call
        self.function_call_server = self.create_service(
            ChatGPT, "/ChatGPT_function_call_service", self.function_call_callback
        )
        # Publisher for grasp_command (→ state_feedback_node or Unity)
        self.grasp_publisher = self.create_publisher(
            GraspCommand, "/grasp_command", 10
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

    def grasp(self, **kwargs):
        """
        Publish GraspCommand to /grasp_command.
        Feedback arrives asynchronously via /grasp_feedback (state_feedback_node or Unity).
        """
        grasp_command = GraspCommand()
        grasp_command.force = float(kwargs.get("force", 0.0))
        grasp_command.object_type = str(kwargs.get("object_type", "unknown"))
        grasp_command.is_fragile = bool(kwargs.get("is_fragile", False))
        self.grasp_publisher.publish(grasp_command)
        self.get_logger().info(
            f"GraspCommand published: object={grasp_command.object_type}, "
            f"force={grasp_command.force}, is_fragile={grasp_command.is_fragile}"
        )
        return f"grasp command sent: object={grasp_command.object_type}, force={grasp_command.force}"

    def publish_target_pose(self, **kwargs):
        """
        Publishes target_pose message to control the movement of arx5_arm
        """
        pose = [
            kwargs.get("x", 0.2),
            kwargs.get("y", 0.2),
            kwargs.get("z", 0.2),
            kwargs.get("roll", 0.2),
            kwargs.get("pitch", 0.2),
            kwargs.get("yaw", 0.2),
        ]
        msg = Float64MultiArray()
        msg.data = [float(v) for v in pose]
        self.target_pose_publisher.publish(msg)
        self.get_logger().info(f"Published target message successfully: {pose}")
        return ', '.join(map(str, pose))



def main():
    rclpy.init()
    arm_robot = ArmRobot()
    rclpy.spin(arm_robot)
    rclpy.shutdown()


if __name__ == "__main__":
    main()
