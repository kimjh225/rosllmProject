#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# flake8: noqa

robot_functions_list_grasp = [
    {
        "name": "grasp",
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
        "parameters": {
            "type": "object",
            "properties": {
                "force": {
                    "type": "number",
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
                },
            },
            "required": [
                "force",
                "object_type",
                "is_fragile",
                "move_speed",
                "motion_style",
            ],
        },
    },
]


class RobotBehavior:
    """
    Robot behavior schema for LLM function calling.
    """

    def __init__(self):
        self.robot_functions_list = robot_functions_list_grasp


if __name__ == "__main__":
    pass
