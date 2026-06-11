"""
Copyright Epic Games, Inc. All Rights Reserved.

Generates LoreVcs/runtime.json for the portable LoreVcs meta package.

runtime.json maps each supported runtime identifier (RID) to the
LoreVcs.runtime.<rid> package that carries that platform's native library.
NuGet reads this file during a RID-qualified restore and pulls only the
matching runtime package, so consumers download just the native lib they need.

The dependency version must match the published runtime packages, so it is
stamped from the LORE_VERSION / LORE_REVISION environment variables at pack
time, composed the same way as the `-p:Version` passed to `dotnet pack`:

    # Release:
    LORE_VERSION=0.1.2 uv run python generator/generate_runtime_json.py
    #   -> runtime packages referenced at "0.1.2"

    # Prerelease (nightly): set LORE_REVISION too
    LORE_VERSION=0.1.2 LORE_REVISION=345 uv run python generator/generate_runtime_json.py
    #   -> runtime packages referenced at "0.1.2-nightly.345"

When LORE_REVISION is set, "-nightly.<revision>" is appended to LORE_VERSION,
matching `-p:Version=$LORE_VERSION-nightly.$LORE_REVISION`. LORE_VERSION is
required; the script exits with an error if it is unset, so the version baked
into runtime.json is never a silent placeholder.

The package name is overridable for custom builds via LORE_PACKAGE_NAME
(optional; defaults to "LoreVcs"). It must match the LORE_PACKAGE_NAME used when
packing the meta and runtime packages so runtime.json references this flavor's
own runtime packages, e.g.:

    # Private flavor:
    LORE_PACKAGE_NAME=LoreVcsPrivate LORE_VERSION=0.1.2 \
        uv run python generator/generate_runtime_json.py
    #   -> LoreVcsPrivate maps to LoreVcsPrivate.runtime.<rid> at "0.1.2"
"""

import json
import os
import sys

# RIDs that ship a LoreVcs.runtime.<rid> package. Keep in sync with the
# runtime packages produced by LoreVcs.Runtime/LoreVcs.Runtime.csproj.
SUPPORTED_RIDS = ["win-x64", "osx-arm64", "linux-x64"]

# Package name is overridable for custom builds (e.g. LoreVcsPrivate) via the
# LORE_PACKAGE_NAME env var; defaults to the open-source name. Must match the
# LorePackageName resolved in LoreVcs.csproj / LoreVcs.Runtime.csproj so the
# generated runtime.json points the meta package at its own flavor's runtime
# packages (never a cross-flavor reference).
PACKAGE_NAME = os.environ.get("LORE_PACKAGE_NAME", "LoreVcs")
META_PACKAGE_ID = PACKAGE_NAME
RUNTIME_PACKAGE_PREFIX = f"{PACKAGE_NAME}.runtime."

OUTPUT_PATH = os.path.join("LoreVcs", "runtime.json")


def main():
    version = os.environ.get("LORE_VERSION")
    if not version:
        sys.exit(
            "LORE_VERSION must be set (e.g. export LORE_VERSION=0.1.2). "
            "For a nightly prerelease, also set LORE_REVISION "
            "(e.g. export LORE_REVISION=345 -> 0.1.2-nightly.345)."
        )

    revision = os.environ.get("LORE_REVISION")
    if revision:
        version = f"{version}-nightly.{revision}"

    runtimes = {
        rid: {META_PACKAGE_ID: {f"{RUNTIME_PACKAGE_PREFIX}{rid}": version}}
        for rid in SUPPORTED_RIDS
    }

    document = {"runtimes": runtimes}

    with open(OUTPUT_PATH, "w", encoding="utf-8") as handle:
        json.dump(document, handle, indent=2)
        handle.write("\n")

    print(f"Wrote {OUTPUT_PATH} (version {version})")


if __name__ == "__main__":
    main()
