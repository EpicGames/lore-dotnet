# Lore C# SDK Examples

This directory contains example projects demonstrating how to use the Lore C#
SDK.

- [`example/`](example/) — uses the high-level fluent API (`LoreVcs.Lore`).
- [`example-native/`](example-native/) — uses the low-level native API
  (`LoreVcs.Interop.Native`), which mirrors the underlying FFI calls.

Both examples run the same basic workflow: create a repository, write a couple
of files, stage them, and commit a revision. In online mode they additionally
push the revision and clone the repository back.

## Offline vs. online runs

Each example accepts an optional remote URL as the first command-line
argument.

- **No argument** → fully offline run. The example creates a local repository
  and commits a file. Nothing is pushed; nothing is cloned.
- **With argument** (e.g. `lore://localhost`) → online run. The example also
  pushes the revision and clones the repository back.

```bash
# Offline runs
dotnet run --project examples/example
dotnet run --project examples/example-native

# Online runs against a local server
dotnet run --project examples/example -- lore://localhost
dotnet run --project examples/example-native -- lore://localhost
```

These examples do not perform authentication. If the remote requires it, run
`lore auth` from the CLI before invoking the example.

## Running a local Lore server

To exercise the online mode of these examples, you can run a Lore server
locally. The steps below build the server from source and configure it for
local development:

1. Clone the Lore repository and build the server in release mode:

   ```bash
   git clone https://github.com/EpicGames/lore.git
   cd lore
   cargo build --release
   ```

2. Create a local config file by copying the example:

   ```bash
   cp lore-server/config/local.toml.example lore-server/config/local.toml
   ```

3. Generate a random secret and set it as `presigned_url_hmac_key` in
   `lore-server/config/local.toml`:

   ```bash
   openssl rand -hex 32
   ```

4. Generate a self-signed TLS certificate (run from the directory where the
   server expects `cert.pem` and `key.pem`):

   ```bash
   openssl req \
     -subj '/CN=localhost:8443/O=Self signed/C=CH' \
     -new -newkey rsa:2048 -sha256 -days 365 -nodes -x509 \
     -keyout key.pem -out cert.pem
   ```

5. Start the server:

   ```bash
   RUST_LOG=info ./target/release/loreserver 2>&1 | tee /tmp/lore.log
   ```

The server is now reachable as `lore://localhost`, which you can pass to the
examples as the first argument.
