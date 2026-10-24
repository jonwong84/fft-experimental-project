# Project guidance

## Mission and baseline

- Build a complete, playable Windows reimplementation of the North American PS1 release `SCUS-94221` using managed C#/.NET.
- Treat original gameplay and campaign behavior as the default compatibility target. Record uncertainty and deviations; do not silently fix observable quirks.
- Keep new mechanics and custom content separate from the original compatibility mode.
- See `PROJECT_PLAN.md` for the current scope, architecture, phases, and unresolved decisions.

## Reference and runtime boundary

- Use `C:\repositories\fft_decomp` as the local code and behavior reference. Treat that checkout as read-only unless the user explicitly asks to modify it.
- The upstream decompilation changes frequently. Record `git rev-parse HEAD` from the reference checkout in source analyses, behavior fixtures, and review notes; do not assume the local checkout matches upstream `main` or cite only moving line numbers.
- The C decompilation is for source study and validation only. The application must not link, invoke, or depend on C reference code at runtime.
- Use the user-provided BIN only as local import input. Verify its edition/hash, extract files, and prepare assets on disk. The running game must load prepared files and must not require or read the BIN.
- Keep the BIN, extracted files, decoded assets, and generated inventories under the ignored `/scratch/` directory. Never stage or commit those files.

## Design and workflow

- Implement incrementally. Before translating a rule, trace its source, callers, data, arithmetic widths, ordering, and relevant quirks. Avoid bulk code-generation or broad ports without independently checkable milestones.
- Keep deterministic simulation independent of rendering, audio, input, wall-clock time, and file I/O. Separate immutable content definitions from battle, campaign, and save state.
- Prefer validated data-driven content and stable IDs. Do not copy original fixed table sizes unless they are part of observed compatibility behavior.
- Add automated tests from the first code milestone. Use source-traced vectors, boundary cases, deterministic replays, and scenario regressions; preserve manual presentation checks separately.
- Document significant architecture choices and behavior differences in `docs/` or `PROJECT_PLAN.md`.
- Keep builds and test instructions reproducible on Windows. Do not add a runtime dependency on the C toolchain, Docker, or PS1-specific libraries.
