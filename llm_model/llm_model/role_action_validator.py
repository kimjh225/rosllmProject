#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import json
import re


class RoleActionValidator:
    """Validate role/action JSON responses and drive correction retries."""

    def __init__(self, max_retries=3):
        self.max_retries = max_retries
        self.allowed_roles = ["MANIPULATION"]
        self.allowed_actions = {"MANIPULATION": ["grasp"]}
        self.required_parameters = [
            "force",
            "object_type",
            "is_fragile",
            "move_speed",
            "motion_style",
        ]
        self.allowed_motion_styles = ["gentle", "normal", "fast"]

    def _extract_json_text(self, content):
        if not isinstance(content, str):
            return None

        cleaned = re.sub(r"```(?:json)?", "", content, flags=re.IGNORECASE).strip()
        cleaned = cleaned.strip("`").strip()

        if cleaned.startswith("{") and cleaned.endswith("}"):
            return cleaned

        start = cleaned.find("{")
        end = cleaned.rfind("}")
        if start == -1 or end == -1 or end <= start:
            return None

        return cleaned[start : end + 1]

    def _normalize_parameters(self, parameters):
        """
        Normalize and clamp parameters before execution.
        This prevents unsafe or malformed LLM output from reaching Unity.
        """
        normalized = dict(parameters)

        normalized["object_type"] = str(normalized.get("object_type", "Target"))
        if normalized["object_type"].strip() == "":
            normalized["object_type"] = "Target"

        value = normalized.get("is_fragile", False)
        if isinstance(value, str):
            normalized["is_fragile"] = value.lower() in [
                "true",
                "yes",
                "1",
                "fragile",
                "careful",
            ]
        else:
            normalized["is_fragile"] = bool(value)

        motion_style = str(normalized.get("motion_style", "normal")).lower().strip()
        if motion_style not in self.allowed_motion_styles:
            motion_style = "normal"
        normalized["motion_style"] = motion_style

        try:
            move_speed = float(normalized.get("move_speed", 1.0))
        except (TypeError, ValueError):
            move_speed = 1.0

        if motion_style == "gentle":
            move_speed = min(move_speed, 0.7)
            move_speed = max(move_speed, 0.3)
        elif motion_style == "fast":
            move_speed = max(move_speed, 1.5)
            move_speed = min(move_speed, 3.0)
        else:
            move_speed = max(0.8, min(move_speed, 1.2))

        normalized["move_speed"] = move_speed

        try:
            force = float(normalized.get("force", 3.0))
        except (TypeError, ValueError):
            force = 3.0

        if normalized["is_fragile"] or motion_style == "gentle":
            force = min(force, 3.0)
            force = max(force, 1.0)
        else:
            force = max(1.0, min(force, 6.0))

        normalized["force"] = force

        return normalized

    def validate_once(self, content):
        json_text = self._extract_json_text(content)
        if json_text is None:
            return False, None, "Response must contain one JSON object."

        try:
            command = json.loads(json_text)
        except json.JSONDecodeError as error:
            return False, None, f"Invalid JSON format: {error}"

        if not isinstance(command, dict):
            return False, None, "Top-level JSON must be an object."

        for key in ["role", "action", "parameters"]:
            if key not in command:
                return False, None, f"Missing required field: {key}"

        role = command["role"]
        action = command["action"]
        parameters = command["parameters"]

        if role not in self.allowed_roles:
            return False, None, f"Invalid role: {role}. Allowed roles: {self.allowed_roles}"

        if action not in self.allowed_actions.get(role, []):
            return (
                False,
                None,
                f"Invalid action '{action}' for role '{role}'. "
                f"Allowed actions: {self.allowed_actions.get(role, [])}",
            )

        if not isinstance(parameters, dict):
            return False, None, "'parameters' must be a JSON object."

        missing_parameters = [
            item for item in self.required_parameters if item not in parameters
        ]
        if missing_parameters:
            return (
                False,
                None,
                f"Missing required parameters: {', '.join(missing_parameters)}",
            )

        parameters = self._normalize_parameters(parameters)
        command["parameters"] = parameters

        return True, command, ""

    def build_retry_messages(self, user_prompt, last_response, validation_error):
        correction_prompt = (
            "Your previous response failed schema validation. "
            f"Validation error: {validation_error} "
            "Return ONLY one JSON object with this exact schema: "
            '{"role":"MANIPULATION","action":"grasp","parameters":'
            '{"force":float,"object_type":"Target","is_fragile":bool,'
            '"move_speed":float,"motion_style":"gentle|normal|fast"}}. '
            "Rules: "
            "Korean words 조심스럽게/천천히 mean motion_style gentle, move_speed 0.5, lower force. "
            "Korean words 빠르게/빨리 mean motion_style fast, move_speed 2.0. "
            "No extra text."
        )

        return [
            {"role": "user", "content": user_prompt},
            {"role": "assistant", "content": last_response},
            {"role": "user", "content": correction_prompt},
        ]

    def validate_with_retry(self, user_prompt, initial_content, response_fn, logger_fn):
        current_content = initial_content

        for attempt in range(self.max_retries + 1):
            is_valid, command, error_text = self.validate_once(current_content)
            if is_valid:
                logger_fn(f"Validator passed on attempt {attempt + 1}: {command}")
                return True, command, current_content

            logger_fn(
                f"Validator failed on attempt {attempt + 1}/{self.max_retries + 1}: {error_text}"
            )

            if attempt >= self.max_retries:
                break

            retry_messages = self.build_retry_messages(
                user_prompt=user_prompt,
                last_response=current_content,
                validation_error=error_text,
            )
            current_content = response_fn(retry_messages)

        return False, None, current_content
