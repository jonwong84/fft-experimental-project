# ZOD-031 — Fixed MAP003 and Chocobo scene

Date: 2026-10-23. Target: SCUS-94221. Reference HEAD checked for this work:
`11464943bbdbd2a0af022d90cb7f10455cf911df`.

The Windows client renders the prepared MAP003 day/dry/default scene and selected
six-piece Chocobo pose together. The view is a frozen inspection scene, with an
authored camera and explicit rendering policy. No battle, camera, animation or RNG
state advances. The existing synthetic window and headless battle commands remain
available independently.

## Launch and capture

Use the pinned SDK and existing locked restore/Release build instructions. Prepare
the map and pose using the [map](prepared-map-slice.md) and
[sprite-pose](prepared-sprite-pose.md) guides, then run from the repository root:

```powershell
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --prepared-scene --map-root scratch/prepared-map003-030-b --pose-root scratch/prepared-chocobo-idle-028-a
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --prepared-scene --map-root scratch/prepared-map003-030-b --pose-root scratch/prepared-chocobo-idle-028-a --check scratch/prepared-scene-check-001
```

Each root contains only its prepared manifest, metadata and texture. The selected
IDs are `base:map/003/day-dry-frozen` and `base:battle/cyoko/idle-front-pose`.
Manifest size/hash verification and Content's profile validation precede GPU creation.
No BIN, ISO, original GNS/mesh/SPR/SHP/SEQ, AssetTool, reference checkout or C runtime
is involved. Original media and all captures remain ignored under `scratch/`.

The optional check requires a fresh directory. After 120 draws it compares the first
and final frame hashes, verifies unchanged prepared inputs, compares the combined
view with a map-only pass to establish a visible sprite contribution, restores the
combined frame and writes `scene.png` plus `check.json`. It exits 0 on success, 1 on
file/device/check failure or premature close, 2 on invalid syntax. An interrupted run
may leave an incomplete directory; choose a fresh name. Normal mode stays open until
Escape or window close. Headless CI tests never open a window.

The whole 960x640 frame fits the backbuffer with point sampling and centered black
bars. Resize changes only this fit. This is not the widescreen camera, higher render
resolution or revised keyboard/mouse UI work in ZOD-059/060/061. The prepared host
shares the existing buffered Escape and device-resource lifecycle approach; new live
resize, mixed-DPI movement, physical-key and forced device-loss checks remain manual.

## Fixed scene contract

- Source coordinates `(X,Y,Z)` become managed `(X,-Y,Z)`. The orthographic camera uses
  managed target `(112,84,182)`, eye `(-208,444,-138)`, world up `(0,1,0)`, width 630,
  height 420, near 1, far 2000. These values frame the map; they are not an encounter
  camera recovered from events. A horizontal reflection after the right-handed
  look-at compensates for the handedness change from source Y conversion.
- Visibility uses native yaw `0xe00`. The pinned `battle_map_mesh.h` documents band
  mask `4 << (((yaw-1)&0xe00)>>9)` and resting-diagonal mask
  `0x400 << (yaw>>10)` when `((yaw+0x200)&0x3ff)==0`. At this yaw the combined mask
  is `0x2100`. It hides 75 of 536 polygons, leaving 461. This is pinned header/source
  evidence; the queue bodies are not available as readable C or an executing oracle.
- `camera_matrices_update` calls `RotMatrix`; its zero-roll horizontal camera row
  is `(cos(yaw),0,sin(yaw))` and depth row is
  `(-sin(yaw)*cos(pitch),sin(pitch),cos(yaw)*cos(pitch))`. This supports an eye toward
  negative X/Z at yaw `0xe00`. The managed view preserves that horizontal row's signs.
  Authored pitch, scale, projection and viewport still differ from PS1 GTE projection.
- The unit stands at lower-layer tile **(3,2)** in the traced four-cell rug checkpoint:
  source `(98,-12,70)`, managed `(98,12,70)`, using unchanged terrain height 1.
  Quad 217's strip corners enclose that exact center. Placement uses the normalized
  Core map, not polygon bounds or reparsed source bytes. The helper rejects slopes,
  water, half-height, blocked/untargetable/cannot-stop tiles and out-of-map positions;
  it is not a general original unit-height rule.
- One prepared pixel equals one world unit on the screen-aligned billboard, or
  `960/630 = 640/420` canvas pixels. Signed part offsets are relative to the projected
  unit origin. The pose is never centered by its combined bounding box. Part flips
  affect UVs independently. Pieces paint in prepared order **5,4,3,2,1,0**.
- Frame 9, sequence 6's first pose, palette 0, wait 6 are held. This is the prepared
  unmirrored front pose, independent of live unit-facing/camera selection. No SEQ
  player, direction change or automatic mirror is introduced.

## Palette, sampling, lighting and ordering policy

Map palette lookup remains per polygon. Only used banks are expanded to separate
1024x256 RGBA8 textures; source pages and UVs retain their original association.
Five-bit channels expand by `(c<<3)|(c>>2)`. A full palette word of zero discards;
every other word is opaque, including STP black `0x8000`. Index zero is not inherently
transparent when its palette word is nonzero. Native map primitives initialize as
opaque GT3/GT4 in `render_buffers_init`; STP alone does not request half-alpha blending.
The sprite uses the already prepared straight RGBA8 alpha 0/255 atlas.

Point/clamp sampling and no mipmaps preserve nearest texel selection. Map UVs address
texel centers `(page*256+U+0.5)/1024, (V+0.5)/256`. Sprite UVs address source rectangle
edges, with explicit independent flips. Native strip quads become triangles `(0,1,2)`
and `(2,1,3)`, preserving coverage and winding. No perimeter fan is substituted.

Lighting is deliberately **neutral, unmodulated texture color** for this inspection
policy. Source `0x8000` lighting flags, normals, matrices and ambient data remain in
the prepared model, but GTE lighting is bypassed. `polygon_flags_dispatch` normally
copies that source bit to runtime active bit 0; this preview does not claim that path.
Untextured polygons use the source initializer's `(0,0,1)`, not guessed colors decoded
from opaque material words (`mesh_render_records_init`).

MonoGame's [AlphaTestEffect](https://docs.monogame.net/api/Microsoft.Xna.Framework.Graphics.AlphaTestEffect.html)
discards alpha zero before depth writes. Map triangles use an opaque blend state,
24-bit depth writes and no backface culling; native yaw flags select hidden surfaces.
The single sprite reads map depth but does not write depth, so its part painter order
is preserved without self-occlusion from floating-point rounding. No additional unit
ordering, PS1 ordering-table buckets, native depth bias, semitransparent passes or
interleaved effect packets are implemented. This is a modern depth-buffer approximation.

## Reference comparison and confidence

The comparison available for this ticket is source-derived, not an original screenshot:

- **Geometry/terrain:** native quad 217 and tile (3,2) agree on the height-1 center.
  Synthetic source-number projection tests verify equal screen center after axis
  conversion and correct strip coverage. Confidence is high for the recorded numeric
  relation; original screen-origin calibration remains unmeasured.
- **Palette/pose:** pinned palette/primitive setup and piece offsets/order agree with
  the chosen cutout and placement policy. Synthetic tests cover all five-bit channel
  values, bank changes, word-zero versus STP black, signed offsets and all flip pairs.
  Hardware texture-edge and color-rounding equivalence remains unmeasured.
- **Camera/visibility:** pinned matrix/header checks fix the eye direction, horizontal
  axis signs and mask arithmetic. Camera framing is authored. No original pixel camera
  match is implied, and the documented queue semantics have not been independently run.
- **Known differences:** neutral lighting replaces GTE modulation; orthographic modern
  projection/depth replaces native projection/ordering/rasterization. The frozen base
  also omits the original map's palette loops. Visibility of the pose around the entry
  pillar follows modern depth and has not been compared against original ordering.

No edition/palette/frame/camera/phase-matched independent original or emulator capture
exists in the current workflow: ZOD-026 is still Ready and no capture oracle was supplied.
That is the exact missing comparison input. Source-coordinate checks, synthetic tests
and repeat captures are the bounded fallback, not substitutes labeled as independent
evidence. ZOD-026/033 retain the original comparison gate; no original-rendering
equivalence or complete M2 milestone is claimed.

## Local validation

Locked restore and Release build pass with zero warnings. All **833 tests** pass:
Core 293, Content 213, AssetTool 151, AssetTool CLI 91, Engine 24, Client 61.
The 37 added cases remain headless and require no original assets or GPU. The existing
headless battle command still returns `FirstUnitWon`, HP 25/0, ticks 20, RNG `2473755763/22`.

Final captures use ignored `scratch/prepared-scene-031-e` and `scratch/prepared-scene-031-f`.
Each run rendered 120 frames, compared frames 1 and 120 and verified unchanged inputs;
the separate runs also have identical raw RGBA and PNG hashes. Both render 461 map polygons, hide 75 and
draw six pose pieces. The pose contributes 1431 pixels versus the map-only frame.
Source anchor `(98,-12,70)` projects to approximately `(585.595,490.380)` on the canvas.
The repeat run launched from `scratch/` with only prepared content paths supplied;
original files were not renamed or removed. Runtime does not require them.

```text
canonical prepared inputs  c2ce6d0e5dea63809c893e9ad217a9adedd119626a313ef5a4ae53e325a982e2
scene raw RGBA             8ed314ee7bf9583e5c901f9a7674e02e7cbff084300355f020f75559fa732187
scene PNG                  d8ff82f212572962481eccc437e8e69ad319e59d9894211fbaba803813d3d0ff
```

Environment: MonoGame DesktopGL 3.8.6/Reach, AMD Radeon RX 6600,
Windows 10.0.26200, .NET 10.0.13, 960x640 canvas/backbuffer. These hashes are local
same-environment observations, not a cross-driver determinism promise or CI golden fixture.
The local receipt records source identities, canonical prepared-input and raw RGBA
hashes, camera, tile/anchor, pose selection, visible counts, GPU, OS, runtime and frame
count. The complete map, rug, banners, water and Chocobo were visually inspected.
The synthetic battle window remains a separate launch path; no original media is committed.

Unsupported: live gameplay/input/picking, full map-height movement/combat integration,
animation/weather/alternate layouts, GTE fixed-point lighting/projection, native draw
ordering/bias, affine edge/raster rules, dithering, semitransparency, effects, shadows,
original camera events and complete multi-unit occlusion. Native map height is unchanged;
MAP003 is not made compatible with M1 combat's height-10 restriction by rendering it.
