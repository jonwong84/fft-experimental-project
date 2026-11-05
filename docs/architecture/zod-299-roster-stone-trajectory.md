# ZOD-299: exact flat roster direct trajectories

Read-only reference `C:/repositories/fft_decomp` is clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`; managed base `5df5cc0`.
This corrects ZOD-297's mistaken absence claim: the capitalized
`src/platform/psyq/libgte/SquareRoot12.c` **is present**. Its local symbol map
also identifies the resident table. No upstream, C or original execution is used.

## SDK rounding without shipping a table

For positive signed32 input, GTE LZCS counts leading zeros. Clear its low bit
to obtain even leading count L. Normalize to an integer N in64..255 by shifting
the input by L-24 (left when nonnegative, right otherwise). Read table[N-64],
then shift that signed16 factor by `(19-L)>>1`, left when nonnegative and
logical right otherwise. This produces the original approximate Q12 result,
not the integer square root of the original squared distance.

The pinned main symbol map gives table address `0x8002b8b8`, file mapping base
`0x8000f800`. The verified prepared executable is356352 bytes, SHA-256
`287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562`.
All192 signed16 entries independently equal `floor(sqrt(N<<18))` for N64..255;
the384-byte span hashes to
`45ca1b7b619b33961c03c82243b8baf59fc50c49b4d7eefc94b76edbd417a5c8`.
The offline audit checks every entry against integer arithmetic. Raw bytes remain
ignored. Runtime can compute the normalized integer root with a small exact
integer algorithm, avoiding copied asset tables, floating-point approximation,
file I/O and any C/PS1 SDK dependency. The normalization and final shifts still
preserve retail approximation; computing sqrt(originalInput) would change samples.

## Explicit sprite metadata and initial occupants

`sprite_sheet_data_t.graphic_height` is a byte. Add a copied immutable binding
for every unit's actual graphic height0..255; require exact roster membership.
This is explicit metadata, not a default human height, an AI grant, or a cached
clearance result. Current living ordinary snapshot/profile certifies no Jump,
mount, float, water, altered depth, crystal/treasure or other collision effects.
Missing metadata rejects. Live KO/corpse trajectory metadata stays separate.

Ground remains the existing open height10 rectangle, at most16x16 and one layer.
Tile centers use28*X+14 /28*Y+14, world baseY=-120; actor and target raises are
graphicHeight*2/3, target zero raise substitutes12. Actor occupies the supplied
legal initial movement origin, every other occupant retains its initial tile.
Exclude only actor/target from obstacle boxes. Visit peers in source order,
independent of team or usefulness. Height0 boxes cannot block; exactly touching
any X/Z +/-8 or vertical endpoint is clear because all box comparisons are strict.

Validate range1..4 and the initial legal origin before sampling. The full domain
fits signed32: horizontal squared length <=112^2, vertical difference <=170,
so `(dx^2+dy^2+dz^2)*4096 <=169754624`. Endpoint SVECTORs, progress<<8 and
fraction/delta products also fit. No signed overflow or zero-distance SDK quirks
are admitted. Direct sample count is floor(distanceQ12/8192); each progress is
8192*(index+1), interpolation fraction `(progress<<8)/(distanceQ12>>4)`, and
signed delta products shift right12. Negative-direction rounding must remain
arithmetic. Terrain checks precede peer boxes. The absent upper layer cannot
intersect these raised paths; lower solid column is worldY in[-120,0).

## Selected managed children

- **ZOD-300:** pure bounded computed direct trajectory query with exact copied
  height metadata, complete initial occupancy, source SDK normalization and
  samples. Return clear/blocked and diagnostics, no RNG, candidate, effect or
  authority. Reuse flat roster/movement admission. Retain ZOD-298's supplied
  clear certificate API and public pair guards.
- **ZOD-301:** fresh isolated roster Stone occurrence, owning one general prefix,
  known ranked-origin priorities and computed geometry. Preserve target-map
  order, blocked retry without draws, clear evaluated-aim consumption including
  zero accuracy, final D1 substitution, whole-query rollback and all peers.
  Decide exact occurrence selection and all initial key admission before coding.

Fixtures independently describe source rounding, strict height/box boundaries,
all-team occupants, unequal sprite heights, diagonal clear/blocked samples and
negative-direction asymmetry. They do not establish full-game trajectory
equivalence beyond this explicitly admitted flat living domain. Full-list
ranking/lottery, roster command/turn integration and all other documented
ability/policy/status/map, corpse, facing, terminal/frame RNG, host/editor,
owner-draft, replacement-interface and ZOD-106 deferrals remain.
