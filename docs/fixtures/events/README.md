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
