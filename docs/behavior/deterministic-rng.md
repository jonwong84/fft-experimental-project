# Explicit deterministic RNG (ZOD-013)

`Zodiac.Engine.IDeterministicRandom<TState>` defines a pure transition from explicit
state to an output and updated state. It imposes no seed layout or game algorithm.
`Fft.Core.FftBiosRandom` implements the FFT-owned generator verified in
[BIOS verification](bios-rng-verification.md). Reference checkout revision:
`611b504d25d4a9b03fab8023d7dbea75fe381d5d`.

Seeding preserves all 32 bits. Each draw computes
`unchecked(state * 0x41c64e6d + 0x3039)` as UInt32 and returns updated bits 16..30.
The UInt64 draw counter is diagnostic metadata, not an original BIOS memory field.
It increments once per raw draw; counter exhaustion throws before returning a
transition. There is no implicit seed, clock input, shared mutable generator,
runtime BIOS access, or reference C dependency. Range conversion remains game-owned
and is not implemented by this ticket.

`BattleState.RngState` is optional: existing movement scenarios have no selected
stream, while callers can supply an explicit state and count. Accepted movement
preserves that snapshot; rejected movement preserves the original battle state.
All three current movement policies consume zero draws. This is their documented
prototype behavior, not a claim about every retail rejected action. Combat draw
ordering must follow the separate [source trace](rng-state-and-ordering.md) when
that action slice is implemented.

The test-only `RandomReplayRunner` compares ordered updated state words, outputs,
and absolute draw counts, reporting the first mismatch. It replays all 64 committed
[observations](../fixtures/rng/scph5501-rng-vectors.json); deliberately corrupted
state, output, and count expectations verify its diagnostics. Additional tests
cover full-width seeds, reseeding, overflow boundaries, immutable interleaved
streams, and movement preservation. The existing movement fixture schema remains
version 1 with null RNG; RNG replay uses the existing separate BIOS-vector format.
Normal tests use numeric observations and require neither BIOS nor emulator.

This establishes generator transitions and current movement preservation. It does
not establish complete battle RNG ordering, visual/gameplay stream separation,
attack execution, or whole-game equivalence.
