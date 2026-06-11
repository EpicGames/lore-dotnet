<!-- Mirrors Lore CONTRIBUTING.md as of 2026-05 -->
# Contributing

The Lore C# SDK follows the Lore project contribution process. Issues and pull
requests are welcome in this repository.

## Prerequisites

- **[.NET 9 SDK](https://dotnet.microsoft.com/download)**
- **Python 3.10+** with [uv](https://github.com/astral-sh/uv) for dependency management
- **`LORE_BUILD_PATH`** — env var pointing to a local Lore native library build from source:
  ```sh
  export LORE_BUILD_PATH="/<path-to>/lore/target/release"
  ```

## Build and test

```sh
uv pip install --group dev
uv run python find_lorelib.py
uv run python generator/generate.py
dotnet pack -c Release LoreVcs.Runtime/LoreVcs.Runtime.csproj
uv run python generator/generate_runtime_json.py
dotnet pack -c Release LoreVcs/LoreVcs.csproj
```

Run the tests:

```sh
dotnet test LoreVcs.Tests --logger "console;verbosity=detailed"
```

## Formatting

Formatting is enforced by CI and must pass before any PR is merged. Install
CSharpier once:

```sh
dotnet new tool-manifest
dotnet tool install csharpier
```

Then run before committing:

```sh
dotnet csharpier .
```

## Before you code

For anything beyond a trivial fix, open a GitHub Issue and wait for a
maintainer to weigh in before investing significant effort. Changes to the
wire protocol or `lore-capi` belong in the
[Lore repository](https://github.com/EpicGames/lore), not here.

## Commit sign-off

Every commit must include a `Signed-off-by:` line. Add it with
`git commit -s`. The DCO, patent affirmation, copyright header rules, and
license compatibility policy are all defined in the canonical contributing
doc.

## Full contribution policy

The full PR process, review process, AI assistance policy, legal terms, and
community channels are published in the Lore repository:

→ [Lore CONTRIBUTING.md](https://github.com/EpicGames/lore/blob/main/CONTRIBUTING.md)
