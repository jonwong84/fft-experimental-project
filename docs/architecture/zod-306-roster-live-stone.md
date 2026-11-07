# ZOD-306: actual roster Throw Stone

Reference: read-only clean `11464943bbdbd2a0af022d90cb7f10455cf911df`;
managed base `2c4b2e0`. The source receipt and independent live vectors are
recorded alongside this study. No original/C execution or new presentation is claimed.

## Intended panel, actual hit and draws

`action_tiles_calculate` routes ability148's Direct Targeting through
`target_validate_for_weapon`. Its default menu selects direct flags independently
of the equipped weapon. The source low-byte ability indexing quirk is harmless
for148; no broader ability-flag translation is selected. A successful resolver
marks the returned unit's tile, and `action_resolve_strike` constructs its target
list from those flags. Therefore an intervening ally or third team can become
the actual formula target; enemy-only AI usefulness is not an execution guard.

`projectile_direct_check_to_target` excludes only the actor from obstacles, while
AI `projectile_direct_check_between_units` excludes actor and intended target.
Both share the traced SDK approximation, graphic heights and strict sampled boxes.
Live tracing returns the first hit unit, or intended target when no obstacle is
hit. The intended target's own box is a normal successful hit, not an unsupported
obstruction. Empty-panel/terrain misses and general terrain remain outside the
selected immediate occupied-panel command domain. Preserve the source-selected
interceptor; never silently damage the requested unit through it.

Admission uses the existing typed effect and real actor grant, exact complete
roster/ordinary-ground bindings, PA1..99, no initial corpses, neutral human/MP0
snapshot and source slot21 limit. Require explicit complete byte sprite heights.
The actual command uses current actor position and intended range1..4; this is
geometry/effect admission, not movement permission or AI compact16 admission.
The old pair command must retain exactly-two validation and its existing stream.

After resolving the actual unit, reuse the existing actual Stone kernel with its
current PA, target facing/evade/Brave and coordinates. Four equipment/class evades
are visited in source order, short-circuiting an actual miss; zero-valued evades
still draw. Admitted equipment evades are zero, so a class miss consumes4 draws.
A hit draws damage `(floor(raw*2/32768)+1)*PA`, then independent attacker and target
Brave rolls. Attempt a push only when floor(targetRaw*targetBrave/32768) is below
floor(actorRaw*actorBrave/32768). Hit uses7 draws even when KO/occupied/off-map
landing cancels displacement. AI midpoint/evasion/score outputs are never executed.

## Settled effect and authority

Knockback goes one panel along the dominant axis; Y wins equal magnitudes. Check
every actual occupant at the destination, not just the intended target or one
opponent. Same-height empty ordinary ground needs no fall damage. Source finalization
cancels displacement for lethal damage; preserve actual HP-derived status and
byte KO-count increment, all peers' CT/choices/facing and actor facing restoration.
Keep the existing turn-event/result/HP/status/KO/push event order; a new interception
diagnostic can identify intended versus resolved IDs before the resolved event.

Validate before ACT/draws. Formula or later draw exhaustion must return the exact
input state and empty events. Publish both RNG copies only after the whole actual
command succeeds. Roster session publication reuses revision/lineage/pending-facing
guards and the existing actual completion receipt. No AI cached plan is authority.

## Selected children

- **ZOD-307:** move the exact pure geometry/height owner into Framework, with
  between-units and to-target entry points. Preserve the AI facade and its extra
  compact16/legal-movement checks. A geometry origin alone must not authorize Move.
- **ZOD-308:** add actual roster Stone with exact target interception and the shared
  existing actual formula/choice/result kernel. Keep pair entry guards/behavior.
- **ZOD-309:** expose that actual command through the existing opaque roster session,
  proving both orders, actual receipts, late atomic failure and replay.
- **ZOD-310:** trace verified roster decision/RNG adoption and retained Move authority
  before selecting its transaction, dispatch and complete-turn children.

Full corpse scheduling, empty panels, other abilities/statuses/terrain/modifiers,
automatic facing, terminal/frame RNG, host/editor, owner drafts, broad replacement
interfaces and ZOD-106 remain separate/deferred.
