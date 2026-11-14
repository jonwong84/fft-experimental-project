# Configurable job movement limits

Move and Jump are managed gameplay values. `JobStats` no longer imposes the
original packed-record maxima of 255 and 127. By default it accepts nonnegative
Int32 values, including values above those original limits.

`JobStats` is sealed. Growth, multiplier and evade properties use `init` accessors
so they can be populated in an object initializer and stay immutable afterward.
Move and Jump remain get-only constructor arguments, preserving their validation.
Editing job content creates a replacement stats instance; battle effects belong
to unit state rather than these loaded definitions. The additional stats remain
authoring scaffolding: metadata package v1 still persists only Move and Jump,
and this change does not add their import mappings or gameplay formulas.

The application's per-game content configuration can supply immutable
`Zodiac.Content.Configuration.JobStatLimits`:

```csharp
var limits = new JobStatLimits(maxMove: 512, maxJump: 256);
var stats = new JobStats(baseMove: 300, baseJump: 150, limits: limits);
var package = ContentPackage.Load(path, limits);
var editor = new JobEditorDocument(package, limits);
```

Use the same settings instance when constructing definitions, loading packages
and opening editor documents. `ContentPackage.ReadJson(json, limits)` and
`JobEditorDocument.OpenSource(path, limits)` also accept it. Calls without
settings use `JobStatLimits.Default`, whose maxima are `int.MaxValue`. Negative
settings or negative stat values reject. Separate settings instances can coexist;
changing one game's validation does not alter another document or replay.

The settings object supports System.Text.Json constructor-based serialization,
so a host may load it from its application configuration. Definitions do not
read settings files or consult mutable global state. This change does not add a
Settings screen or choose a host-specific configuration-file location.

Editor validation, draft reopen and JSON/file loading enforce supplied limits
with the existing `$.jobs[n].baseMove` / `baseJump` error paths. Package JSON
retains its existing numeric fields; the chosen application policy is passed
explicitly rather than embedded in every content record.

Original import adapters still decode original bytes and the separate Jump/
SteppingStone bit. Rules that specifically emulate original movement/AI retain
their documented input domains. Raising authored-content limits does not imply
that every existing bounded rules implementation supports the extended domain.
