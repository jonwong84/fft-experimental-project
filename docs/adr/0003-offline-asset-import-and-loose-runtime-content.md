# ADR 0003: Import assets offline; load prepared content at runtime

- **Status:** Accepted; implemented in the current asset/content proof
- **Date:** 2026-10-20

## Context

The user's local BIN is a source for extraction, but the application must be a native managed program and should not depend on the BIN, decompilation, Docker, or original PS1 libraries at runtime. Content also needs stable logical identifiers and validation.

## Decision

Use `Fft.AssetTool` for offline inventory, extraction, and format conversion. Store prepared files and local game-derived artifacts under ignored `/scratch/`. Use a versioned manifest and logical asset IDs for runtime access through `Fft.Content`. The game client reads prepared files from disk and does not read the BIN.

## Consequences

- Runtime asset access can be tested without mounting the disc image.
- Import steps can verify expected edition, paths, sizes, and hashes before the client uses content.
- Decoding is incremental by format; extracting a file does not imply that it has been decoded into a runtime-ready form.
- The current loose-file store is an initial implementation, not a final package/archive format.
- Game-derived assets remain local and untracked.

## Alternatives considered

- Read sectors directly from the BIN in the game: rejected because it couples the application to disc layout and violates the chosen runtime boundary.
- Bundle extracted game assets in the repository or application distribution: rejected; keep the user's data local.
