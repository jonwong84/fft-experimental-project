# Supplied US v3.0 BIOS RNG verification (ZOD-005 follow-up)

Date: 2026-10-22. Managed baseline: `90a747c`. Decomp HEAD rechecked read-only: `611b504d25d4a9b03fab8023d7dbea75fe381d5d`. This closes the missing BIOS algorithm evidence from [the initial RNG investigation](rng-state-and-ordering.md), for the exact local BIOS identified below. It does not establish a complete FFT incoming RNG state, all game call ordering, every BIOS revision or original-hardware timing.

## Input identity and local boundary

The user supplied `scratch/bios/SCPH-5501.bin`, 524,288 bytes. SHA-256: `11052b6499e466bbf0a709b1f9cb6834a9418e66680387912451e971cf8a1fef`. Identification MD5: `490f666e1afb15b7362b406ed1cea246`. The embedded ROM version identifies US v3.0, 1996-11-18. The MD5 matches the [DuckStation BIOS identification entry](https://github.com/stenzek/duckstation/blob/master/src/core/bios.cpp) for SCPH-5501/5503/7003; the hash identifies shared firmware, not which physical console produced this file. The algorithm evidence comes from the supplied binary, not the filename or that external table.

The BIOS, installed analysis dependencies, disassembly, independent-analysis notes and raw execution capture remain under ignored `scratch/`. No BIOS instruction bytes or disassembly are committed. The [committed vector metadata](../fixtures/rng/scph5501-rng-vectors.json) contains identity, addresses, tools, derived scalar input/output vectors and capture hashes only. This is a separate `fft.bios-rng-vectors` format, not a movement fixture with invented nonnull RNG fields; fixture v1 remains unchanged.

## Static linkage and arithmetic evidence

Two agents independently decoded the binary. ROM offsets are measured from the start of the 512 KiB file:

- `rand`: offset `0x2200`, virtual entry `0xBFC02200`; 12 instructions including its return delay slot, ending at `0xBFC0222C`.
- `srand`: offset `0x2230`, virtual entry `0xBFC02230`; three instructions including its return delay slot at `0xBFC02238`. A return value is not specified or used.
- The A0 pointer table begins at ROM offset `0x4300`. Entry `0x2f` at offset `0x43bc` points to rand; entry `0x30` at `0x43c0` points to srand.
- The copier at `0xBFC042D0` copies that table to RAM `0x200` (through ROM `0x4604`, end exclusive); corresponding RAM slots are `0x2bc` and `0x2c0`.
- Boot code copies ROM `[0x10000,0x18bf0)` to RAM starting at `0x500`. Its A0 stub is at RAM `0x510`; the copier at `0xBFC042A0` installs this stub at low RAM `0xa0`. The stub reaches RAM `0x5c4`, which indexes RAM `0x200` by selector register `t1 * 4`, loads the function pointer and jumps through it. These selectors agree with the pinned FFT `rand`/`srand` veneers and `include/psx/bios.h`.

Both routines address the same word at virtual `0xA0009010`, physical RAM `0x9010`. `srand` stores all 32 argument bits directly; there is no zero-seed substitution, sign rejection or 31-bit masking. `rand` loads the full word, performs an unsigned multiply, takes its low 32 bits, adds `0x3039` using non-trapping modular addition, extracts the return value with logical shift/mask, and stores the complete updated word. Both stores occur in the `jr` return delay slots; stopping analysis at the return instruction would miss the state transition.

The independently derived managed arithmetic is:

```csharp
uint nextState = unchecked(state * 0x41c64e6du + 0x3039u);
int value = (int)((nextState >> 16) & 0x7fffu);
```

The state is unsigned 32-bit modulo `2^32`; output is 0..32767 from the **updated** state. Multiply high bits are discarded; addition overflow wraps. Seed and state retain bit 31 even though the output masks it. For example, seeds 0 and `0x80000000` produce equal first output 0 while leaving distinct states `0x00003039` and `0x80003039`. Do not replace the complete saved state with a last output or assume identical outputs imply identical states.

## Actual instruction execution

The optional [oracle harness](../../tools/verification/verify_bios_rng.py) uses Unicorn **2.1.4**, little-endian MIPS32 4KC architectural execution, to execute the supplied ROM instructions. Capstone **5.0.9** was used for local disassembly only; it is not required by the harness. Python/runtime version and harness SHA-256 are recorded in the vector metadata and raw report.

The harness maps RAM and ROM, initializes the kernel RAM block from its ROM copy source, then **executes the actual vector and A0-table copier routines**. It reads back and checks the table destinations. Calls enter at low RAM `0xa0` with selectors `0x2f`/`0x30`, follow the copied stub and table, execute the BIOS routines and return to a controlled sentinel. Instruction hooks verify the return delay-slot stores execute; memory hooks require exactly one four-byte write at the RNG state word for each isolated call. A0 rand calls execute 23 instructions and seed calls 14, including stub/dispatch, excluding the stop sentinel. Those counts are harness-path instructions, not PS1 cycle counts or FFT draw counts.

Each isolated call uses a fresh CPU initialized from the same prepared RAM image, with the prior observed state word carried forward explicitly. This avoids emulator stop/translation-cache artifacts encountered when re-entering a stopped dispatch path on a reused CPU. Static analysis and the observed write footprint establish the routine's persistent state dependency; no full CPU/device snapshot or running game is claimed. The MIPS32 model executes the relevant MIPS-I subset with branch delay slots, but it is not a cycle-accurate R3000A model. Load delay timing does not affect this code path: instructions separate each load from its consumer. No full BIOS boot, device I/O or game execution occurs.

## Vectors and boundary checks

Eight seeds each have eight consecutive observed draws and full before/after states, plus replacement/repeated-seed checks. The seeds are 0, 1, `0x7fffffff`, `0x80000000`, `0xffffffff`, `0x12345678`, and two arithmetic boundary seeds. The complete 64-draw data is in the committed metadata; all observed values were compared with the separately expressed arithmetic above.

- Seed 1 first produces state `0x41c67ea6`, output 16838.
- Seed `0xffffffff` first produces state `0xbe39e1cc`, output 15929; multiplication exceeds 32 bits and is truncated as observed.
- Seed `0xfc77a683` produces state zero and output zero. Its low multiply result is exactly the value whose addition of `0x3039` wraps to zero.
- Seed `0x0dbdbb1e` produces state `0xffffffff` and output 32767, just below that additive wrap boundary.

The last two seeds were chosen by solving the modular update for desired next states 0 and `0xffffffff`; the results were then observed through ROM execution. Raw reports record multiplication and addition overflow separately. Re-seeding after intervening draws reproduces the first observed state/output. `srand` has a state write but consumes zero random outputs; each rand invocation produces one output and one updated state word.

Evidence classification: **executable-reference**, independently observed under the documented emulator/harness, supported by independent static binary analysis. This is stronger than a C# or Python transcription executed by itself. It is still not an original-hardware observation or a whole-game differential test.

## Reproduction

This is an optional developer-only verification tool. It introduces no Python, BIOS, emulator or native-library dependency into the managed application or ordinary test suite. This run used Python 3.12.14. From the repository root, with Python 3.10+ and the supplied BIOS present:

```text
python -m pip install --target scratch/bios-analysis/deps -r tools/verification/requirements-bios-rng.txt
python tools/verification/verify_bios_rng.py --bios scratch/bios/SCPH-5501.bin --dependencies scratch/bios-analysis/deps --output scratch/behavior/bios-rng/scph5501-oracle.json
```

The harness rejects a different BIOS hash/length, an unreviewed Unicorn version, unexpected targets/writes, a missed return/delay store or a mismatched state/value. Raw output is restricted to the checkout's `scratch/`. Changing Python, source line endings or harness source changes the capture metadata/hash even if scalar vectors agree; review tool identity and scalar results separately. The committed fixture records this run's exact capture/harness hashes. Installed wheels, raw captures, BIOS bytes and disassembly must not be staged.

Validation completed: eight seeds/64 draws and reseeding passed; a repeat run reproduced the same raw-report SHA-256; capture and harness hashes matched committed metadata; every state/output fit its declared width; a synthetic same-size wrong-hash input was rejected without changing the valid report. Documentation links and whitespace checks passed. The managed application/test code is unchanged, so its .NET suite was not rerun for this investigation.

## Consequences and remaining limits

ZOD-005's BIOS-state/overflow evidence gap is resolved for this pinned BIOS, so ZOD-013 can now implement the explicit neutral RNG transition seam and FFT-owned generator. Its state must preserve the full word and draw order must remain observable. The formula-level draw-order and preview findings in the initial note remain applicable; this investigation did not execute the FFT formula chain or prove a universal zero-draw rule for every rejected command.

The managed runtime will implement this arithmetic without invoking or loading a BIOS. How actual gameplay arrives at an initial state still depends on startup/reseeding and other consumers; rendering-driven or preview calls need an explicit compatibility/isolation decision. Other BIOS revisions, patched dispatch tables, full-command validation/reaction order, malformed signed range-conversion overflow and original hardware comparison remain outside this completed bounded investigation. These are separate from the now-verified generator update itself.
