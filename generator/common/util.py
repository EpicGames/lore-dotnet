"""
Copyright Epic Games, Inc. All Rights Reserved.

Code generation helpers
"""

from pycparser import c_parser

MODULE_HEADER = (
    '"""This file is generated, do not modify it here, it may be overwritten"""'
)


STUBS = [
    "typedef unsigned char uint8_t;",
    "typedef unsigned int uint32_t;",
    "typedef unsigned long uintptr_t;",
    "typedef unsigned long uint64_t;",
    "typedef int int32_t;",
    "typedef unsigned short uint16_t;",
    "typedef long int64_t;",
]


def load_and_clean_header(header_path):
    """
    Loads lore header, cleans preprocess macros and comments, and adds stdint stubs,
    returns an ast. Gathers comments and attaches comments to the following non-empty
    non-comment line.
    """
    lines = []
    comments = []
    line_comments = {}
    row = 0
    lines.extend(STUBS)
    with open(header_path, "r", encoding="utf-8") as header_file:
        for line in header_file:
            row += 1
            if line.startswith("#"):
                lines.append("\n")
                if line.strip() != "" and len(comments) > 0:
                    line_comments[row] = comments
                    comments = []
                continue
            if line.startswith("/*"):
                comments.append(
                    line.strip().removeprefix("/*").removesuffix("*/").strip()
                )
                lines.append("\n")
                continue
            if line.strip().startswith("//"):
                comments.append(line.strip().removeprefix("//").strip())
                lines.append("\n")
                continue
            lines.append(line)
            if line.strip() != "" and len(comments) > 0:
                line_comments[row] = comments
                comments = []
    lines = "".join(lines)
    parser = c_parser.CParser()
    ast = parser.parse(lines)
    return (ast, line_comments)


def pascal_case(s):
    """Returns the string s in PascalCase"""
    pascal_case_name = "".join(word.capitalize() for word in s.split("_"))
    return pascal_case_name


def camel_case(s):
    """Returns the string s in camelCase"""
    pascal_case_name = pascal_case(s)
    return pascal_case_name[:1].lower() + pascal_case_name[1:]
