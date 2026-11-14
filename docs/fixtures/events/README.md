# Synthetic prepared event fixtures

`conditional-event-v1.json` is authored metadata, not an original event dump. It
describes the conditional-outcome example from the
[ZOD-040 source investigation](../../architecture/zod-040-event-script-investigation.md)
at read-only decomp HEAD `11464943bbdbd2a0af022d90cb7f10455cf911df`.
Its expected records were written independently of the managed decoder/serializer.
The evidence is source-derived plus synthetic expectation, with no independent
original/emulator semantic observation. No original text, media or block is included.

To reconstruct the synthetic input: allocate 8,192 zero bytes, write little-endian
u32 value 48 at offset 0, then this authored code at offset 4:

```text
BE 00 00 B1 00 00 02 00 BE 01 00 B0 01 00 01 00 A2 D0 01
BE 03 00 B0 03 00 01 00 D1 02 D2 01 BE 03 00 B0 03 00 02 00
D2 02 D4 03 DB
```

Write `FE` at offset 48; leave the remainder zero. This is one empty packed text
entry, following `src/lib/event/dialog/entries.c::text_entry_skip` (FE/FF terminators).
It is **not** a full text bank with a 32-entry offset header. Text is retained as
opaque input and never decoded/executed by this profile. The complete authored
block SHA-256 is
`7cb7271dcaf8ccc99a5507ac3b5b431c6c69663eadb1d6fad522cd1d9c0b7f2f`.

The 15 semantic records occupy 4..47, with D0 at 21 landing at 35 and D1 at 31
landing at 45. D4 at 45 exits sequentially; DB at 47 is structural and unreachable.
Variables 0..3 and runtime branch expectations are documented by ZOD-040; this
fixture records preparation/loading metadata. ZOD-065 now projects it into the
[bounded executor](../../behavior/conditional-event-execution.md), with independent
expected semantic traces in Core tests. Tests reconstruct the bytes from a literal,
check the independent hash/offsets and load the committed JSON separately.

Processed input is a separate inspection variant: replace the first four bytes
with `F2 F2 F2 F2` and supply preserved text offset 48 explicitly. Its hash differs;
do not relabel it with the raw hash. All generated blocks/prepared inventories or
future original inspection outputs belong under ignored `scratch/`.

## Timed wait sequence

`timed-wait-sequence-v1.json` is a second independently authored synthetic fixture
at the same pinned reference SHA. It explicitly selects
`sample:event-profile/non-tutorial-speed1-timed-v1`. It is not a retail script.
Build its invented input by allocating 8,192 zero bytes, writing little-endian u32
42 at offset 0, this code at offset 4, and one packed-text terminator FE at 42:

```text
BE 03 00 B0 03 00 01 00 F1 01 00 B0 03 00 02 00 F1 00 00
F1 3C 00 B0 03 00 04 00 F1 01 00 B0 03 00 08 00 D4 01 DB
```

Full invented block SHA-256:
`6240a68745c1414f57f1dc08fad7aa44519103035d195b4eacbc92e3cf199b91`.
There are 11 structural records including DB at 41, and 10 normalized operations.
Waits at 12/20/23/31 have counts 1/0/60/1; Exit is at 39. Literal expected traces
and tick/state boundaries are recorded in
[the wait contract](../../behavior/timed-event-waits.md) and its Content tests,
independently of decoding/execution. Initial variable 3 = 9 is cleared, then adds
1, 2, 4 and 8 produce 15 after exactly 62 supplied scheduler ticks.

The same conditional-profile fixtures remain unchanged. No original block or
decoded script is committed; generated copies stay ignored.

## Task barrier sequence

`task-barrier-sequence-v1.json` is a third independently authored fixture at
reference HEAD `11464943bbdbd2a0af022d90cb7f10455cf911df`, selecting
`sample:event-profile/non-tutorial-speed1-task-barrier-v1`. It contains no original
script/text/media. Allocate 8,192 zero bytes, write little-endian u32 value 51 at
offset 0, this literal invented code at offset 4, and FE at offset 51:

```text
BE 03 00 E5 04 00 B0 03 00 01 00 F1 01 00 E5 36 00
B0 03 00 02 00 E5 37 00 B0 03 00 04 00 E5 36 00 F1 00 00
E5 38 00 B0 03 00 08 00 D4 01 DB
```

Full invented block SHA-256:
`a9618ad699ad4d101a60fc3a8a6809e8dc04309a34d86a9d2a0a989e75e83310`.
The 14 structural offsets are 4/7/10/15/18/21/26/29/34/37/40/43/48/50.
E5 selects Camera 4, transition 54, hold 55, transition 54 and ConditionDisplay 56;
F1 waits at 15/37 count 1/0. D4 at 48 exits; DB at 50 is unreachable.

The [combined execution contract](../../behavior/scene-event-barriers.md#independent-fixture)
records all seven tick boundaries and explicit authored task facts. Content tests
independently assert full states/ordered traces and fresh-definition replay; CLI
tests reconstruct the literal block and compare its hash/offsets before conversion.
These are source-derived predicates plus declared native ordering policies, not
an original/emulator semantic oracle. Processed input replaces the header with
F2 F2 F2 F2 and explicitly preserves text offset 51; its hash differs.
