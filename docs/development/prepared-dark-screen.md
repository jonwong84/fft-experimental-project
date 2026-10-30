# ZOD-094 — Prepared dark-screen transition preview

The selected origin-style DarkScreen transition now runs from a separate
prepared package in the Windows host. Offline conversion preserves the original
approximate-root delays and Q12 rotation samples. The existing pure worker and
task ledger remain unchanged; rendering reads accepted submissions and cannot
acknowledge completion. This is an authored standalone scene over an explicit
checker background. It does not execute original events 4/401 or compose the
held worker with general/Ready banners yet.

Date: 2026-10-25. Managed base: `b30b715`. Read-only reference HEAD was freshly
checked as `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [source trace](../architecture/zod-092-dark-screen-transition.md) and
[pure worker](../behavior/dark-screen-worker.md) retain their historical findings.
[Validation metadata](../evidence/events/prepared-dark-screen-v1.json) binds this
implementation, prepared inputs, native runs and independent audits.
The owner approved the gallery on **2026-10-25**: "That looks good."
Approval covers this authored preview; original entry/release comparison remains open.
This approval update changes documentation/evidence only and reruns no implementation suites.

## Prepare, run and review

Use the already extracted, verified original executable beneath scratch. The
offline preparer checks its entire 356,352-byte SCUS-94221 identity, load header
and both math-table hashes. It reads no C code and executes no original instructions.

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
dotnet run --project src/Zodiac.AssetTool.Cli -c Release --no-build --no-restore -- prepare-dark-screen --executable scratch/all-data-extraction/SCUS_942.21 --output scratch/dark-screen-example-prepared
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --dark-screen --content-root scratch/dark-screen-example-prepared --id fft.ps1-us:dark-screen/origin-v1
```

**Space** submits one explicit logical tick; **Esc** closes the preview. A close
does not manufacture task completion. Add `--check scratch/dark-screen-example-check`
to capture every accepted tick and exit. Choose fresh output directories.
Preparation accepts `--hold-frames 0..255`, default 44; this is the authored wait
after the entry barrier, not the original loader/hold duration. The first release
is tick 56, default release 100, maximum release 311. Parent exits are 113/157/368.

Review `scratch/dark-screen-094-review/review.html`. It contains normal, earliest
release and longest hold playback, a logical-tick slider and checkpoint images.
Review playback uses twelve ticks per second for inspection. Look at the entering
tiles, full-screen hold, collapsing tiles and clean return to the checker. This
gallery uses actual C# native captures; original entry/release comparison remains
open. Its existing displayed PNGs are byte-identical to the final stricter-loader
captures under `scratch/dark-screen-094-final/`.

## Prepared data and readiness

`DarkScreenDecoder` lives only in AssetTool. It applies the source-traced
leading-zero/table sqrt approximation and unsigned positive delay arithmetic,
and selects two Q12 axes for each of nine angles. The preparer verifies the
whole original EXE and both tables before conversion. All 288 output delays and
all nine axes match an independent original-table audit. Exact sqrt would change
six delays; it is not substituted. Actual C# projection of all 117 angle/radius
combinations matches the original-table arithmetic, including signed truncation.

The two-file package contains `assets.manifest.json` and `dark-screen.json`.
The metadata has a stable ID/owner, exact selected parameters, 288 staggered
coordinates/delays, nine bounded rotation records, pinned math provenance and
an authored hold count. `authored-dark-screen-preview-v1` projects the fixed typed
Start → class-54 barrier → authored wait → Release → barrier → Exit proof.
It contains no copied original event bytecode. Verified-retail provenance applies
to math data; it does not make this authored scene an original event.

Content enforces a 64 KiB load budget, manifest size/hash/selected ID, complete
required fields, duplicate/unknown/null/fractional rejection, six explicit selected
parameter fields, explicit tile fields, layout/delay/axis bounds and provenance.
Authored-test fixtures have separate provenance without invented original hashes.
Existing Content formats reject this package. Conversion bounds input before
allocation, rejects reparse paths/existing outputs, validates a staged package
through Content, then publishes by rename. Failures preserve existing files.

The host preloads and validates the complete package before creating a scene.
It creates every GPU resource before calling `DarkScreenSession.Start` or creating
check output. DarkScreen needs no texture/palette artwork. BONUS/general/Ready
artwork readiness remains a later enclosing-scene requirement, not an implicit
side effect of this standalone preview. Runtime reads only prepared files and
has no original EXE/BIN/table, reference-checkout, C-toolchain or emulator dependency.

## Selected flat-quad policy

`DarkScreenPresentation` implements `untextured-rgb5-flat-quad-v1`: explicit opaque
256×240 zero-padded RGB5 background; selected RGB 48/40/16; average semi-transparency;
dither/window/mask disabled. Black source color is still a blend operation.
There is no texture lookup, zero-index transparency or STP eligibility test.
Each pass blends five-bit channels before expansion; overlapping quads remain
separate passes. The native row-major submissions are reversed for execution.
Geometry uses prepared Q12 axes and signed division by 16,384 toward zero;
the original x offset reduces normalized tile centers to their stored positions.
The flash spans (0,0)..(256,256), clipped to the 256×240 canvas.

Each quad splits into triangles 0/1/2 and 1/2/3. The bounded compositor normalizes
winding, rejects zero-area triangles, clips signed coordinates and samples integer
pixel positions with a top/left edge rule. Opposite shared edges belong to one
triangle only. This is the **selected provisional coverage policy**. The source
trace establishes packet geometry/material/order; original entry/release pixel
comparison must still validate precise edge coverage. CPU/GPU/model agreement
does not establish original PS1 raster equivalence.

The GPU displays the CPU canvas with opaque, point-sampled copying and integer
window scaling. It performs no second translucent pass. Native checks compare
actual target readback at every accepted tick. Resource recreation at tick 5
repaints the same accepted state; no simulation tick or ledger fact originates
from Draw, readback or resource ownership.

## Validation and limits

Locked restore and a zero-warning/error Release build pass. All **2,031 .NET**
tests pass: Engine 24, Framework 725, Content 546, AssetTool 202, CLI 202, Client 332.
The **64 new managed checks** cover offline math, strict metadata, safe preparation,
missing readiness, signed projection, draw order, diagonal/shared-edge coverage,
overlap blending, clipping/degenerates and render-independent full-state/trace replay.
All **240 Python tests** pass, including eleven new authored scanline, timeline
and receipt checks requiring no original media.

Five final native runs cover normal/repeated normal, zero hold, maximum hold and
an isolated prepared-only normal run. All **957 PNGs** match actual GPU readback,
CPU composition and an independently structured Python scanline model at zero
tolerance. The model solves integer edge half-planes into row intervals; it does
not invoke the C# compositor. Two preparations repeat both files; repeated and
isolated normal runs repeat all **159 artifacts**, including full-state/trace
digests. Two final audit reports repeat byte for byte. The isolated working
directory contains only the copied prepared package and generated output; host
libraries remain in the built application directory. This is not a clean-machine
deployment test.

Five real entry-point negatives cover missing package, corrupt payload, rehashed
unsupported profile, unavailable SDL graphics and existing output. They create no
new check output and preserve the owner's marker. Six historical native profiles
retain every output byte: RGB8 16, RGB5 16, ordered 17, Ready 72, combined sequence
164, introduction/battle 170 (**455 total**). The full managed suites also preserve
the old strict parsers and simulation rules.

DSC-001/022 now have bounded prepared/GPU readiness and resource-recreation checks;
DSC-025..028 execute the selected prepared/math/presentation layers. DSC-030 has
full managed and bounded native output regressions. DSC-029 and original edge/
entry/release scanout comparison remain open. Historical contract flags stay
unchanged; implementation evidence records the executed layers. Owner visual
approval was recorded on 2026-10-25. No new original capture/emulator execution, original duration,
combined startup/EventEnd, hardware backend, CRT/aspect or audio claim is made.

**Next: ZOD-095**, plan and bind finite original entry/
release references before claiming that comparison. Integrating this held worker
with general/Ready and the playable battle follows as separate work. Original
inputs, decoded/prepared data, captures, inventories and reports stay ignored
under scratch; only authored code/tests/docs and metadata identities are committed.

An independent audit command, with a fresh output, is:

```powershell
python tools/verification/verify_dark_screen_preview.py --prepared scratch/dark-screen-094-final/a-prepared --capture scratch/dark-screen-094-final/a --executable scratch/all-data-extraction/SCUS_942.21 --reference-root C:/repositories/fft_decomp --output scratch/dark-screen-audit-example
```
