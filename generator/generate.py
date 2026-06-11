"""
Copyright Epic Games, Inc. All Rights Reserved.

Generate lorelib wrappers based on Jinja2 template.
"""

import os
import subprocess

from common.generate import generate_templates
import common.visitor
from registry import build_augmented

SCRIPT_DIR = os.path.dirname(__file__)
LORE_HEADER_FILE = os.path.join(SCRIPT_DIR, "../lore/include/lore.h")
TEMPLATES_DIR = os.path.join(SCRIPT_DIR, "templates")
SDK_DIR = os.path.join(SCRIPT_DIR, "../LoreVcs")
SDK_TYPES_DIR = os.path.join(SDK_DIR, "types")
SDK_INTEROP_DIR = os.path.join(SDK_DIR, "interop")

GENERATE_TARGETS = [
    ("enum_types.ji", SDK_TYPES_DIR, "enums.cs"),
    ("args_types.ji", SDK_TYPES_DIR, "args.cs"),
    ("events_types.ji", SDK_TYPES_DIR, "events.cs"),
    ("types.ji", SDK_TYPES_DIR, "types.cs"),
    ("functions.ji", SDK_INTEROP_DIR, "native.cs"),
    ("fluent.ji", SDK_DIR, "lore.cs"),
]


def pretty_print_files(generate_targets):
    """Pretty prints the given C# files and updates them in place"""
    for _, directory, file_name in generate_targets:
        content_filename = os.path.join(directory, file_name)
        subprocess.run(["dotnet", "csharpier", "format", content_filename], check=True)


generate_templates(
    LORE_HEADER_FILE,
    TEMPLATES_DIR,
    GENERATE_TARGETS,
    common.visitor.LoreVisitor,
    build_augmented,
)

print("Applying code formatting", end=" ")
pretty_print_files(GENERATE_TARGETS)
print("done.")
