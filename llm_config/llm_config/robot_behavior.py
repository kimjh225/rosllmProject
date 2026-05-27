#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# flake8: noqa
<<<<<<< HEAD
=======
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
# This file contains the behavior of the robot.
# It includes a list of functions for the robot to perform,
# such as publishing a cmd_vel message to control the movement of the robot.
# To customize the robot's behavior,
# modify the functions in this file to customize the behavior of your robot
# and don't forget to modify the corresponding real functions in llm_robot/turtle_robot.py
#
# Author: Herman Ye @Auromix

# Example robot functions list for the TurtleSim
# The user can add, remove, or modify the functions in this list
robot_functions_list_1 = [
    {
        "name": "publish_cmd_vel",
        "description": "Publish cmd_vel message to control the movement of turtlesim, including rotation and movement,only used for turtlesim,not for robotic arm",
        "parameters": {
            "type": "object",
            "properties": {
                "linear_x": {
                    "type": "number",
                    "description": "The linear velocity along the x-axis",
                },
                "linear_y": {
                    "type": "number",
                    "description": "The linear velocity along the y-axis",
                },
                "linear_z": {
                    "type": "number",
                    "description": "The linear velocity along the z-axis",
                },
                "angular_x": {
                    "type": "number",
                    "description": "The angular velocity around the x-axis",
                },
                "angular_y": {
                    "type": "number",
                    "description": "The angular velocity around the y-axis",
                },
                "angular_z": {
                    "type": "number",
                    "description": "The angular velocity around the z-axis",
                },
            },
            "required": [
                "linear_x",
                "linear_y",
                "linear_z",
                "angular_x",
                "angular_y",
                "angular_z",
            ],
        },
    },
    {
        "name": "reset_turtlesim",
        "description": "Resets the turtlesim to its initial state and clears the screen,only used for turtlesim,not for robotic arm",
        "parameters": {
            "type": "object",
            "properties": {},
            "required": [],
        },
    },
    {
        "name": "publish_target_pose",
        "description": "Publish target pose message to control the movement of arm robot, including x, y, z, roll, pitch, yaw",
        "parameters": {
            "type": "object",
            "properties": {
                "x": {
                    "type": "number",
                    "description": "The x position of the target pose",
                },
                "y": {
                    "type": "number",
                    "description": "The y position of the target pose",
                },
                "z": {
                    "type": "number",
                    "description": "The z position of the target pose",
                },
                "roll": {
                    "type": "number",
                    "description": "The roll of the target pose",
                },
                "pitch": {
                    "type": "number",
                    "description": "The pitch of the target pose",
                },
                "yaw": {
                    "type": "number",
                    "description": "The yaw of the target pose",
                },
            },
            "required": [
                "x",
                "y",
                "z",
                "roll",
                "pitch",
                "yaw",
            ],
        },
    },
    {
        "name": "publish_target_pose",
        "description": "Publish target pose message to control the movement of arm robot, including x, y, z, roll, pitch, yaw. For example,[0.2, 0.2, 0.2, 0.2, 0.2, 0.2] is a valid target pose.",
        "parameters": {
            "type": "object",
            "properties": {
                "x": {
                    "type": "number",
                    "description": "The x position of the target pose",
                },
                "y": {
                    "type": "number",
                    "description": "The y position of the target pose",
                },
                "z": {
                    "type": "number",
                    "description": "The z position of the target pose",
                },
                "roll": {
                    "type": "number",
                    "description": "The roll of the target pose in radians",
                },
                "pitch": {
                    "type": "number",
                    "description": "The pitch of the target pose in radians",
                },
                "yaw": {
                    "type": "number",
                    "description": "The yaw of the target pose in radians",
                },
            },
            "required": [
                "x",
                "y",
                "z",
                "roll",
                "pitch",
                "yaw",
            ],
        },
    },
]
>>>>>>> origin/ros2-humble

robot_functions_list_grasp = [
    {
        "name": "grasp",
<<<<<<< HEAD
        "description": (
            "Execute a grasp command for robot arm manipulation. "
            "Use this function when the user asks the robot arm to move, pick, grasp, carry, or place an object. "
            "If the user says 조심스럽게, 천천히, 부드럽게, carefully, gently, slowly, "
            "set motion_style='gentle', move_speed=0.5, is_fragile=true, and use lower force. "
            "If the user says 빠르게, 빨리, 신속하게, fast, quickly, "
            "set motion_style='fast', move_speed=2.0, and use stronger force if the object is not fragile. "
            "If no speed is specified, set motion_style='normal' and move_speed=1.0. "
            "In the current Unity demo, object_type should usually be 'Target'."
        ),
=======
        "description": "Execute a grasp command for arm manipulation.",
>>>>>>> origin/ros2-humble
        "parameters": {
            "type": "object",
            "properties": {
                "force": {
                    "type": "number",
<<<<<<< HEAD
                    "description": (
                        "Target grasp force. "
                        "Use 2.0 for gentle or fragile handling, "
                        "3.0 for normal handling, "
                        "and 4.5 for fast or firm grasping."
                    ),
                },
                "object_type": {
                    "type": "string",
                    "description": "Target object name. In the Unity demo, use 'Target'.",
                },
                "is_fragile": {
                    "type": "boolean",
                    "description": (
                        "Whether the object should be handled carefully. "
                        "Set true when the user says 조심스럽게, 천천히, fragile, gentle, or carefully."
                    ),
                },
                "move_speed": {
                    "type": "number",
                    "description": (
                        "Movement speed multiplier for Unity robot motion. "
                        "gentle=0.5, normal=1.0, fast=2.0."
                    ),
                },
                "motion_style": {
                    "type": "string",
                    "description": "Motion style. Allowed values: gentle, normal, fast.",
                    "enum": ["gentle", "normal", "fast"],
=======
                    "description": "Target grasp force.",
                },
                "object_type": {
                    "type": "string",
                    "description": "Target object type for grasping.",
                },
                "is_fragile": {
                    "type": "boolean",
                    "description": "Whether the target object is fragile.",
>>>>>>> origin/ros2-humble
                },
            },
            "required": [
                "force",
                "object_type",
                "is_fragile",
<<<<<<< HEAD
                "move_speed",
                "motion_style",
=======
>>>>>>> origin/ros2-humble
            ],
        },
    },
]


class RobotBehavior:
    """
<<<<<<< HEAD
    Robot behavior schema for LLM function calling.
=======
    This class contains the behavior of the robot.
    It is used in llm_config/user_config.py to customize the behavior of the robot.
>>>>>>> origin/ros2-humble
    """

    def __init__(self):
        self.robot_functions_list = robot_functions_list_grasp


if __name__ == "__main__":
    pass
