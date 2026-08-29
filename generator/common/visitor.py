"""
Copyright Epic Games, Inc. All Rights Reserved.

Traverse the AST for the Lore header and generate the types and function wrappers
"""

import operator

from pycparser import c_ast

def c_div(left, right):
    """Integer division that truncates toward zero, as C does

    Python's `//` floors instead, so it disagrees with C whenever exactly one
    operand is negative: `-3 / 2` is `-1` in C but `-2` in Python.
    """
    quotient = abs(left) // abs(right)
    return -quotient if (left < 0) != (right < 0) else quotient


def c_mod(left, right):
    """Remainder that takes the sign of the dividend, as C does

    C defines `%` from truncating division, so `-3 % 2` is `-1`, while Python's
    floor-based `%` gives `1`.
    """
    return left - c_div(left, right) * right


UNARY_OPS = {
    "+": operator.pos,
    "-": operator.neg,
    "~": operator.invert,
}

BINARY_OPS = {
    "+": operator.add,
    "-": operator.sub,
    "*": operator.mul,
    "/": c_div,
    "%": c_mod,
    "<<": operator.lshift,
    ">>": operator.rshift,
    "|": operator.or_,
    "&": operator.and_,
    "^": operator.xor,
}


class LoreVisitor(c_ast.NodeVisitor):
    """Visit the header ast and store arg types"""

    def __init__(self, line_comments):
        self.enums = {}
        self.args = {}
        self.events = {}
        self.types = {}
        self.functions = {}
        self.functions_comments = {}
        self.line_comments = line_comments

    def _extract_field_type(self, node):
        """Recursively extract the type name from the node"""
        if isinstance(node, c_ast.TypeDecl):
            return self._extract_field_type(node.type)
        elif isinstance(node, c_ast.IdentifierType):
            return (" ".join(node.names), 0)
        elif isinstance(node, c_ast.PtrDecl):
            pointer_type = self._extract_field_type(node.type)
            return (f"{pointer_type[0]}*", 0)
        elif isinstance(node, c_ast.ArrayDecl):
            array_type = self._extract_field_type(node.type)
            return (f"{array_type[0]}", node.dim.value)
        elif isinstance(node, c_ast.Enum):
            return (f"{node.name}", 0)
        elif isinstance(node, c_ast.Struct):
            if node.name:
                return (f"{node.name}", 0)
            else:
                return ("anonymous struct", 0)
        else:
            return (str(type(node)), 0)  # fallback for unexpected types

    def _extract_fields(self, node_fields):
        fields = []

        for node_field in node_fields:
            if isinstance(node_field.type, c_ast.Union):
                return self._extract_fields(node_field.type.decls)
            field_type = self._extract_field_type(node_field.type)
            field_name = node_field.name
            fields.append(
                (
                    field_type[0],
                    field_name,
                    field_type[1],
                    (
                        self.line_comments[node_field.coord.line]
                        if node_field.coord.line in self.line_comments
                        else None
                    ),
                )
            )

        return fields

    def _eval_enum_value(self, node, named_values):
        """Evaluate an enumerator's constant expression, e.g. `-1` or `1 << 2`"""
        if isinstance(node, c_ast.Constant):
            return int(node.value.rstrip("uUlL"), 0)
        elif isinstance(node, c_ast.UnaryOp):
            return UNARY_OPS[node.op](self._eval_enum_value(node.expr, named_values))
        elif isinstance(node, c_ast.BinaryOp):
            return BINARY_OPS[node.op](
                self._eval_enum_value(node.left, named_values),
                self._eval_enum_value(node.right, named_values),
            )
        elif isinstance(node, c_ast.ID):
            # A reference to an earlier enumerator of the same enum
            return named_values[node.name]
        else:
            raise ValueError(f"Unsupported enumerator value: {node}")

    # pylint: disable=invalid-name
    def visit_Enum(self, node):
        """Visit nodes that are enums"""
        enum_name = node.name if node.name != "lore_event_id_t" else "lore_event_tag_t"
        value_prefix = enum_name.removesuffix("_t").removesuffix("_tag").upper() + "_"

        values = []
        named_values = {}
        # An enumerator without an explicit value is the previous one plus one
        next_value = 0

        for e in node.values.enumerators:
            value = (
                self._eval_enum_value(e.value, named_values) if e.value else next_value
            )
            named_values[e.name] = value
            next_value = value + 1
            values.append((e.name.removeprefix(value_prefix), value))

        self.enums[enum_name] = values

    # pylint: disable=invalid-name
    def visit_Struct(self, node):
        """Visit nodes that are structs"""
        struct_name = node.name
        if not node.decls:
            return
        fields = self._extract_fields(node.decls)
        if node.name.endswith(("event_data_t", "event_data_array_t")):
            self.events[struct_name] = fields
        elif node.name.endswith("args_t"):
            self.args[struct_name] = fields
        else:
            self.types[struct_name] = fields

    # pylint: disable=invalid-name
    def visit_Decl(self, node):
        """Visit nodes that are declarations and process function declarations"""
        if isinstance(node.type, c_ast.FuncDecl):
            self.functions[node.name] = node.name + "_args_t"
            if node.coord.line in self.line_comments:
                self.functions_comments[node.name] = self.line_comments[node.coord.line]
        if isinstance(node.type, c_ast.Enum):
            self.visit_Enum(node.type)
