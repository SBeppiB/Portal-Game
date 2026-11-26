# Portal System (foundational implementation)

Unity 6000.3.2f1. Runtime code lives in `Assets/Portals/Scripts/Runtime` (assembly `Portals.Runtime`).
Tests live in `Assets/Portals/Tests/{EditMode,PlayMode}`.

No portal gun models, visuals, shaders or input bindings are included by design. This is the
underlying technology only.

## 1. What was removed

All afterimage functionality was deleted:

- `AfterImages` usage from `Player Movement.cs` and `Enemy Movement1.cs` (the plural type was
  undefined and was an existing compile error).
- The `Afterimage` component previously declared at the bottom of `DashController.cs`.
- All afterimage fields, timers, coroutines, spawn calls and renderer caches.

`HealthSystem.cs` was added to the player folder because `Pain.cs` referenced an undefined
`HealthSystem` type (another pre-existing compile error). Replace it with your own health
component if you already have one; `Pain.cs` only needs `TakeDamage(float)`.

## 2. Components

| Component | Purpose |
|---|---|
| `PortalEndpoint` (abstract) | Shared ellipse geometry, transform tracking, frame maths, gizmos |
| `LocaversalPortal` | One endpoint of a same-universe pair |
| `InterversalPortal` | One endpoint of a cross-universe link; has mapping modes |
| `LocaversalPortalSystem` | Registry + pair integrity warnings (optional scene object) |
| `InterversalPortalSystem` | Universe registry, states, physics profiles, entity universe switching |
| `UniverseBody` | Marks an entity's universe; applies per-universe gravity/damping to a Rigidbody |
| `PortalPlacer` | Raycast surface placement + validation + energy gating (call from your gun) |
| `PortalTraverser` | Swept-plane crossing detection, teleportation, momentum/velocity transform |
| `EnergyStorage` | Capacity, current energy, spending, regeneration, events |
| `PortalEnergyCosts` | Serializable cost configuration |
| `PortalEnergySystem` | Creation authorization, maintenance upkeep, events |

`IPortalMomentumReceiver` lets a movement controller rotate its own cached vectors after a
teleport. It is implemented by `PlayerMovement`, `BasicMovementController` and `DashController`.

`PortalVisual` renders each endpoint as an opaque, solid-colour elliptical disc plus a thin
frame, using a standard URP/Standard material with weak emission. It has no colliders, sits
on the `Ignore Raycast` layer, and its children are parented to `PortalEndpoint.Opening` so
they follow surface anchors on moving surfaces. Closing a portal hides its visuals via
`ActiveChanged`; pooled endpoints are reconfigured with the correct kind colour and aperture.
`PortalVisualColours` supplies the default Locaversal (blue) and Interversal (magenta) palettes;
each component's colours are Inspector-configurable. The runtime debug LineRenderers are
disabled in favour of the real visuals (Scene-view gizmos remain).

`PortalLifecycleManager` owns multiple independent `PortalPair` records. Each pair has a
unique id, its own entrance/exit, a 30-second lifetime (started when both endpoints are
placed) and persistence state. The `Default Lifetime` and `Max Active Pairs` fields are
Inspector-configurable. Closing a pair detaches surface anchors, releases energy upkeep,
resets traversal re-entry state and pools the endpoint objects for reuse.

## 3. Scene setup (SampleScene)

Setup is automatic. `PortalSceneBootstrap` runs after SampleScene loads in Play Mode and
calls `PortalSceneSetup.EnsureSetup`, which creates (idempotently):

- `Portal Systems` with `LocaversalPortalSystem`, `InterversalPortalSystem`,
  `PortalEnergySystem` and `EnergyStorage`.
- `Portal A` and `Portal B`, paired via `SetPair`, both initially inactive, each with a
  `PortalDebugVisualizer`.
- On the Player: `PortalPlacer`, `PortalFiringController` and `PortalTraverser`.
- A `SpawnPoint` at the player's initial transform.
- Parents the Main Camera under the Player at local `(0, 1.6, 0)` so mouse look follows.

To author these objects permanently in the scene file (and see them before pressing Play),
use **Tools > Portal > Setup Portals in Active Scene** and then save the scene. The command
is idempotent and marks the scene dirty; it does not auto-save.

For manual setup instead: create two `LocaversalPortal` objects and call `SetPair`, add
`PortalTraverser` to the player, add `EnergyStorage` + `PortalEnergySystem` to a systems
object, add `PortalPlacer` + `PortalFiringController` to the player, and call
`placer.SetIgnoreRoot(player.transform)` so the placement ray ignores the player's own
colliders.

### Firing controls

- **T** — equip / unequip the portal gun (high jump moved to **F** to remove the conflict).
- **M** — toggle travel mode: LOCAVERSAL / INTERVERSAL.
- **1-9** — select dimension / universe (updates `InterversalPortalSystem.SelectedUniverse`).
- **Left mouse** — fire the selected portal; A/B alternate after each successful shot.
- **N** — toggle portal persistence (pauses/resumes pair lifetimes without closing portals).
- **B** — close every active portal pair.
- **/** — cycle to the next portal pair (GUI buttons are also provided).

Firing uses the camera aim ray via `PortalPlacer.TryPlaceFromShot` (20-unit shot range): a
surface hit uses the normal-based surface validation, while no hit creates a free-floating
portal at the endpoint facing back along the shot. A short-lived magenta ray shows where
each shot was aimed (green on success, red on rejection). Rejections are logged with the
`PortalPlacementFailure` reason and a human-readable detail. The IMGUI `PortalHUD` shows
travel mode, selected dimension, gun state and the next portal.

Interversal setup: add `InterversalPortalSystem`, define universes in `Initial Universes`
(id + physics profile), add `InterversalPortal` endpoints, configure each with
`Configure(universeId, linkedPortal, mappingMode, worldOffset)`, and add `UniverseBody` to
travellers that need per-universe physics.

## 4. Inspector configuration notes

- Portal opening: `Opening Half Size` (metres, ellipse radii), `Exit Offset` (metres in front
  of the exit plane), `Is Active`.
- `PortalTraverser`: `Max Portal Distance` (broad-phase; 0 = unlimited), `Max Traversal Speed`
  (optional safety cap; 0 = unlimited), `Reentry Grace Distance`, `Ignore Timeout`,
  `Switch Universe On Interversal Travel`, `Notify Momentum Receivers`.
- `PortalPlacer`: `Placeable Layers`, `Max Placement Distance`, `Surface Offset`,
  `Portal Overlap Radius`, `Require Linked Portal`, `Energy System`, `Ignore Root`,
  `Additional Exclusions`, `Orientation Reference`.
- Opening fit validation: `Validate Opening Fit`, `Fit Probe Depth`, `Fit Normal Dot`,
  `Fit Plane Tolerance`, `Fit Sample Count`.
- `EnergyStorage`: capacity, starting energy, regeneration rate and delay, allow overcharge.
- `PortalEnergyCosts`: creation costs per portal kind, per-portal per-second upkeep,
  reactivation and universe-switch costs.

## 5. Layers and tags

No new layers or tags are required. `PortalPlacer` uses a LayerMask you can narrow in the
Inspector and ignores colliders under its `Ignore Root` (the Player, when auto-set up), so a
first-person camera cannot place a portal on itself. Nothing in this system depends on the
existing `Player` tag beyond the pre-existing `Pain`/enemy code.

The project's active input handler is now set to **Both** (`activeInputHandler: 2`), so all
existing legacy `Input.*` movement controls keep working alongside the New Input System.
Restart the Editor once if legacy input still appears disabled in an already-open session.

## 5b. Placement rules

Portals can be placed on any collider surface: floors, walls, ceilings, slopes, arbitrarily
rotated objects, primitives and MeshColliders, static or moving. There is no tag restriction
and no surface-angle restriction. Placement uses the raycast hit point and surface normal
directly; the portal's local +Z is aligned to that normal, with world-up (or the player's
facing on floors/ceilings) used only to decide the opening's roll.

Validation is geometric rather than categorical:

- The ray must hit a non-excluded collider within range (`Placeable Layers`, `Ignore Root`,
  `Additional Exclusions`).
- The hit normal and point must be finite.
- `Validate Opening Fit` probes the centre and rim of the ellipse from just in front of the
  surface. Every probe must find a collider with a compatible normal (`Fit Normal Dot`) at a
  compatible plane distance (`Fit Plane Tolerance`). This rejects openings that hang over
  edges or span disconnected geometry instead of disabling validation entirely.
- The opening must not overlap the partner portal (`Portal Overlap Radius`).
- Energy must be available for creation.

`PortalFiringController` logs a detailed human-readable reason for every rejection (hit
collider, normal, failed rim sample, etc.) via the 4-argument `TryPlace` overload.

Portals placed on moving/rotating surfaces are tracked by `PortalSurfaceAnchor`, which stores
the placement pose in the surface's local frame and refreshes the world pose each
`LateUpdate`. This avoids inheriting non-uniform scale and feeds correct velocity into portal
traversal.

## 5b. Test Chamber Generator

**Tools → Portal → Generate Test Chamber** builds a greybox chamber under a parent GameObject
named `TestChamber`. If one already exists, a confirmation dialog offers replacement;
regeneration deletes the old root first so geometry is never duplicated. Two neutral-grey
material assets are created/reused in `Assets/Portals/Materials`.

The chamber contains a start platform + `SpawnPoint`, a 20 m gap with tall placement walls,
floor/ceiling surfaces, elevated platforms at several heights, an open mid-air placement
section (no surfaces within the 20 m shot range), a Locaversal/Interversal puzzle area, and
eight movable physics props (cubes/capsules, varied sizes including one too large for the
default aperture, plus angled objects).

The generator reuses existing portal systems via `PortalSceneSetup.EnsureSetup` when needed,
never creates a second player or portal manager, and assigns the generated `SpawnPoint` to
the existing `PlayerMovement.spawnPoint` field. The reusable layout code lives in
`TestChamberBuilder` (Portals.Runtime); the editor script only handles materials,
confirmation and scene dirty state.

## 6. Maths summary

Entrance frame E, exit frame X, Ry(180) flip F:

- `R = X.rotation * F * inverse(E.rotation)`
- `p' = X.position + R * (p - E.position)`
- `v' = R * (v - vEntrance) + vExit`
- `w' = R * (w - wEntrance) + wExit`
- `q' = R * q`

Crossing uses a swept segment against the local z = 0 plane plus an elliptical opening test.
For grounded `CharacterController` players on floor portals, where the origin stays in front
of the plane, a collider-body intersection test is used as a fallback (it requires the body
to approach/straddle the plane and its centre to lie inside the aperture, preventing false
positives). Entry from either side is supported: front entry uses the Ry(180) flip; back
entry omits it so travel still exits along the exit portal's outward normal. Generic objects (Rigidbody,
CharacterController or plain transform) are transported without needing to implement
`IPortalMomentumReceiver`; that interface remains an optional hook for controllers with
cached momentum vectors. Collider extents are measured in portal-local space and objects
larger than the aperture are rejected.

## 7. Known limitations

- Portal visuals are deliberately simple: opaque solid disc + frame, no animation, particles,
  transparency, custom shaders or portal-window rendering. The gun model remains yours to design.
- Frustum/visibility culling was not added; debug LineRenderers are always updated for the two
  portals. Object pooling is unnecessary because firing spawns no transient effect objects.
- Moving-portal motion uses additive frame velocities; angular motion is exact for aligned
  spin axes and an approximation otherwise.
- Per-universe physics is applied to `UniverseBody` Rigidbodies; it does not mutate global
  `Physics.gravity`.
- Scale is assumed to be 1 on portal and traveller transforms (rotation-only transforms).
- Aperture validation uses world-space collider bounds (exact for BoxCollider, conservative
  for sphere/capsule/mesh), so a rotated non-box object may be rejected slightly early.
  For objects with a `CharacterController`, only that authoritative movement collider is
  measured; decorative child colliders (e.g. the Player's visual Capsule) are ignored so
  they cannot falsely inflate the player's aperture footprint.
- Objects without any enabled collider cannot be size-validated and are allowed through.
- Mid-air placement skips surface-fit probing (there is no surface) but checks that the
  endpoint is not inside another collider and does not overlap the partner portal.
- Runtime setup targets scenes named `SampleScene` (or any scene with a `Player` object).
  Other scenes need the Editor menu command or manual wiring.
- The existing PORTAL GUN FBX in SampleScene is untouched; it is only shown/hidden by the
  equip toggle.

## 8. Tests

Run in the Editor Test Runner, or via CLI (without `-quit`; the test runner exits Unity):

```
Unity.exe -batchmode -nographics -projectPath <project> -runTests -testPlatform EditMode -testResults <path>
Unity.exe -batchmode -nographics -projectPath <project> -runTests -testPlatform PlayMode -testResults <path>
```

### Core traversal fix

`PortalTraverser` now treats the collider **front face reaching the portal plane** as entry
(`Body Contact Tolerance`, default 0.6 m). A `CharacterController` is stopped by the host
wall before its origin can cross the plane, so origin-only swept detection never fired in
real gameplay. Exit placement is now anchored at the entrance plane (lateral aperture offset
only) and pushed out along the exit normal by the object's own collider depth, so objects no
longer emerge inside/behind the exit wall.

Last verified result: **32/32 EditMode passed, 85/88 PlayMode passed** on Unity 6000.3.2f1 (3 pre-existing failures unrelated to this change).

## 10. Omniversal Portal Mechanics (OPM)

### Architecture
Namespace: `Portals.Runtime.Mechanics`.

Classes:
- `OmniversalCoordinates`: destination coordinate P=(L,R,A,θ,ρ,x,y,z).
- `PortalDisplacementCalculator`: circular AU distance, angular distance, displacement values, DT.
- `PortalStructuralAnalyzer`: structural divergence VD.
- `PortalEnergyCalculator`: base/structural/instability/layer/rift/total energy.
- `PortalStabilityCalculator`: SP, compatibility, integrity, eligibility.
- `PortalAccuracyCalculator`: coordinate error εP and accuracy AP.
- `UniverseStructuralProfile`: destination environmental conditions.
- `OmniversalScanner`, `UniverseSignature`, `UniverseSignatureDatabase`, `OmniversalScanResult`.
- `OPMConfiguration` (ScriptableObject, alias `PortalMechanicsConfiguration`).
- `PortalFormationEvaluator` / `PortalFormationService`.
- `PortalDiagnosticData`, `PortalFormationResult`, `PortalFormationFailureReason`.
- `PortalTravelMode` (Uniport/Altiport).

### Coordinate conventions
- Layers: 1..50 (max displacement 49).
- Rings: 0..9 (max displacement 9).
- AU positions: 0..179 circular (AU distance wraps).
- Prime AUs represented via `IsPrimeAu`.
- θ in degrees [0,360), angular distance min(d,360-d).
- ρ non-negative, local spatial (x,y,z).

### Equations
q = wL(DL/49)^2 + wR(DR/9)^2 + wA(DA/90)^2 + wρ(Dρ/Sρ)^2 + wθ(Dθ/Sθ)^2 + wx(DS/Sx)^2
DT = sqrt(q)
VD = min(1, wR*VR + wA*VA + wL*VL)
Eportal = K*DT^α*(1+βVD)*(1+γU)*(1+λDL+μVR)*(1+ηF/(1+B))
SP = Eavailable/(Eportal*(1+U))
AP = 1/(1+εP), εP = sqrt(εL²+εR²+εA²+ερ²+εx²)
CP = 1-VD, IP = SP*CP.

### Integration
OPM calculates required energy and formation eligibility; existing EnergyStorage/PortalEnergySystem remain authority for consuming energy. Failed formation does not consume energy. Uniport/Altiport travel modes reuse existing Locaversal/Interversal portals.

### Known limitations
- Scanner is synchronous with caching; simulated frequency only.
- Visual feedback and editor inspectors not fully implemented.
- OPMConfiguration asset must be created in project for runtime tuning (defaults available in code).
- Characteristic scales use defaults that may need tuning.

Last verified result: **102/102 EditMode passed, 95/95 PlayMode passed** on Unity 6000.3.2f1.

## 9. New features in this overhaul

- Procedural portal visual: `Assets/Portals/Shaders/PortalSwirl.shader` provides polar-coordinate swirl, layered noise, radial movement, transparency layering, rim energy, and optional portal-view texture sampling. `PortalVisual` now uses it with configurable swirl speed/intensity/noise/colours/alpha.
- Portal window rendering: `PortalWindowRenderer` creates a secondary camera + RenderTexture and transforms it through the portal pair with an oblique clip plane, assigning the texture to the portal surface material.
- Split-object transitions: `PortalSplitTransition` creates a synchronized kinematic proxy on the destination side, clips geometry against portal planes, and keeps physics driven by the original body. Limitation documented in code.
- Performance: Profiler markers added to `PortalTraverser.TickDetection` and `PortalSplitTransition.Tick`.
`CorePortalFunctionalityTests` moves objects with real `CharacterController.Move` and Rigidbody
motion into wall portals and verifies both directions, Rigidbody cubes, unpaired/closed portals,
oversized objects and aperture fit.
Covers universal traversal, grounded floor-portal traversal, mid-air placement, portal
lifetime/persistence/multi-pair shutdown, and visual rendering (opaque colours, closure
visibility, pooled endpoint reconfiguration, moving-surface following, independent pairs).
Includes placement tests for floors, walls, ceilings, slopes, rotated objects, MeshColliders,
moving surfaces, edge rejection, exclusions, pairing, and an end-to-end firing test.
