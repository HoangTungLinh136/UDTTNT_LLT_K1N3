from __future__ import annotations

import ast
import math
import operator
import re
from datetime import datetime
from zoneinfo import ZoneInfo


class SafeCalculator:
    """
    Calculator local an toàn.
    Không eval() chuỗi Python tùy ý.
    """

    _BIN_OPS = {
        ast.Add: operator.add,
        ast.Sub: operator.sub,
        ast.Mult: operator.mul,
        ast.Div: operator.truediv,
        ast.FloorDiv: operator.floordiv,
        ast.Mod: operator.mod,
        ast.Pow: operator.pow,
    }

    _UNARY_OPS = {
        ast.UAdd: operator.pos,
        ast.USub: operator.neg,
    }

    @classmethod
    def calculate(cls, expression: str) -> float | int:
        tree = ast.parse(
            expression,
            mode="eval",
        )
        return cls._eval_node(tree.body)

    @classmethod
    def _eval_node(cls, node):
        if isinstance(node, ast.Constant):
            if isinstance(node.value, (int, float)):
                return node.value
            raise ValueError("UNSUPPORTED_LITERAL")

        if isinstance(node, ast.BinOp):
            op_type = type(node.op)
            if op_type not in cls._BIN_OPS:
                raise ValueError("UNSUPPORTED_OPERATOR")

            left = cls._eval_node(node.left)
            right = cls._eval_node(node.right)

            if op_type is ast.Pow and abs(right) > 12:
                raise ValueError("POWER_TOO_LARGE")

            return cls._BIN_OPS[op_type](
                left,
                right,
            )

        if isinstance(node, ast.UnaryOp):
            op_type = type(node.op)
            if op_type not in cls._UNARY_OPS:
                raise ValueError("UNSUPPORTED_UNARY")

            return cls._UNARY_OPS[op_type](
                cls._eval_node(node.operand)
            )

        raise ValueError("UNSUPPORTED_EXPRESSION")


class LocalUtilityTools:

    @staticmethod
    def current_time_vn() -> str:
        now = datetime.now(
            ZoneInfo("Asia/Ho_Chi_Minh")
        )

        return (
            f"Bây giờ là {now:%H:%M}, "
            f"ngày {now:%d/%m/%Y} "
            "(giờ Việt Nam)."
        )

    @staticmethod
    def current_date_vn() -> str:
        now = datetime.now(
            ZoneInfo("Asia/Ho_Chi_Minh")
        )

        return (
            f"Hôm nay là ngày "
            f"{now:%d/%m/%Y} "
            "(giờ Việt Nam)."
        )

    @staticmethod
    def extract_calculation(
        question: str,
    ) -> str | None:
        q = (
            question
            or ""
        ).strip()

        # Chỉ nhận biểu thức toán học đơn giản.
        candidates = re.findall(
            r"(?<!\w)"
            r"[-+]?"
            r"(?:\d+(?:\.\d+)?)"
            r"(?:\s*[\+\-\*\/%\(\)\.]\s*"
            r"(?:\d+(?:\.\d+)?))*"
            r"(?!\w)",
            q,
        )

        if not candidates:
            return None

        longest = max(
            candidates,
            key=len,
        ).strip()

        if not any(
            op in longest
            for op in (
                "+",
                "-",
                "*",
                "/",
                "%",
            )
        ):
            return None

        return longest
