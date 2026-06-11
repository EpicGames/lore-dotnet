"""
Copyright Epic Games, Inc. All Rights Reserved.

Gets the Lore header and lib

LORE_BUILD_PATH: Use a locally build lorelib or a pre-built lorelib
"""

import os
import platform
import re
import shutil
import sys

LORE_BUILD_PATH = os.environ.get("LORE_BUILD_PATH")

SYSTEM = platform.system().lower()
MACHINE = platform.machine().lower().replace("x86_64", "amd64")

# Destination names use a uniform base ("lorelib") so a single managed assembly
# is portable: DllImport("lorelib") resolves to these per-OS filenames.
WINDOWS_LIB_NAME = "lorelib.dll"
DARWIN_LIB_NAME = "liblorelib.dylib"
LINUX_LIB_NAME = "liblorelib.so"

HEADER_NAME_RE = re.compile(r"^lore(-v\d+\.\d+\.\d+(-nightly-\d+(-[\w.-]+)?)?)?\.h$")

INC_DIR = os.path.join("lore", "include")
LIB_DIR = os.path.join("lore", "lib")


def _copy_header():
    matches = [
        name for name in os.listdir(LORE_BUILD_PATH) if HEADER_NAME_RE.match(name)
    ]
    if not matches:
        sys.exit(
            f"No Lore header found in {LORE_BUILD_PATH} "
            f"(expected lore.h, lore-vX.Y.Z.h, or lore-vX.Y.Z-nightly-REVISION[-NAME].h)"
        )
    if len(matches) > 1:
        sys.exit(
            f"Multiple Lore headers found in {LORE_BUILD_PATH}: "
            f"{sorted(matches)}. Remove all but one before building."
        )
    header_path = os.path.join(LORE_BUILD_PATH, matches[0])
    shutil.copy(header_path, os.path.join(INC_DIR, "lore.h"))


def _wheel_lib_name():
    if SYSTEM == "windows":
        return WINDOWS_LIB_NAME
    elif SYSTEM == "linux":
        return LINUX_LIB_NAME
    elif SYSTEM == "darwin":
        return DARWIN_LIB_NAME
    else:
        sys.exit(f"unsupported platform found: {SYSTEM}")


def _copy_lib():
    if SYSTEM == "windows":
        prefix, ext, tag = "lore", "dll", ""
    elif SYSTEM == "linux":
        prefix, ext = "liblore", "so"
        tag = "linux-arm64" if MACHINE in ("arm64", "aarch64") else "linux-x86_64"
    elif SYSTEM == "darwin":
        prefix, ext, tag = "liblore", "dylib", "macos-arm64"
    else:
        sys.exit(f"unsupported platform found: {SYSTEM}")

    tag_suffix = f"-{tag}" if tag else ""
    pattern = re.compile(
        rf"^{prefix}(-v\d+\.\d+\.\d+(-nightly-\d+(-[\w.-]+?)?)?{tag_suffix})?\.{ext}$"
    )

    matches = [name for name in os.listdir(LORE_BUILD_PATH) if pattern.match(name)]
    if not matches:
        sys.exit(
            f"No Lore library found in {LORE_BUILD_PATH} for {SYSTEM}/{MACHINE} "
            f"(expected {prefix}.{ext} or "
            f"{prefix}-vX.Y.Z[-nightly-REVISION[-NAME]]{tag_suffix}.{ext})"
        )
    if len(matches) > 1:
        sys.exit(
            f"Multiple Lore libraries found in {LORE_BUILD_PATH}: "
            f"{sorted(matches)}. Remove all but one before building."
        )

    src_lib = os.path.join(LORE_BUILD_PATH, matches[0])
    dst_lib = os.path.join(LIB_DIR, _wheel_lib_name())
    shutil.copy(src_lib, dst_lib)


def _copy_licenses():
    # Optional: fetch-lorelib drops Lore license files (Lore_Licenses.txt,
    # liblore.THIRD-PARTY-NOTICES.txt) into LORE_BUILD_PATH when Lore published
    # them. Ship them beside the library. Absence is fine -> nothing copied.
    for name in os.listdir(LORE_BUILD_PATH):
        if name.endswith(".txt"):
            shutil.copy(
                os.path.join(LORE_BUILD_PATH, name), os.path.join(LIB_DIR, name)
            )


def main():
    """Gets the Lore header and library from a pre-built binary or a local build"""

    if not LORE_BUILD_PATH:
        sys.exit("LORE_BUILD_PATH must be set")

    print(f"Creating include folder: {INC_DIR}")
    os.makedirs(INC_DIR, exist_ok=True)

    print(f"Creating include folder: {LIB_DIR}")
    os.makedirs(LIB_DIR, exist_ok=True)

    _copy_header()
    _copy_lib()
    _copy_licenses()


if __name__ == "__main__":
    main()
