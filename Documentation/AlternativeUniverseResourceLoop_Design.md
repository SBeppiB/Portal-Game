# Alternative Universe Resource Loop — Design Proposal

> Design only. No runtime code changes. Portal engine (`Portals.Runtime`) is treated as finished
> and is not modified. All differences between universes are content/state-based; physics stays
> identical across universes (default `UniversePhysicsProfile`, no per-universe gravity/time scaling).

## 1. Design Principles

1. Reuse the finished portal engine unchanged (`PortalTraverser`, `PortalPlacer`, `PortalPair`,
   `InterversalPortalSystem`, `UniverseBody`, `PortalVisual`, energy system).
2. Physics is identical in every universe. Only gameplay tuning (enemy HP/damage multipliers,
   spawn counts, loot tables) varies — never gravity, mass, or time scale.
3. Differences are content/state-driven and data-driven via ScriptableObjects.
4. Deterministic generation from a coordinate seed; no runtime mesh/level generation.
5. Single-player scope; no networking, no new portal rendering.

## 2. Universe Coordinate System

Format: `L{layer:00}-R{ring:00}-P{position:000}` (example: `L03-R02-P044`).

| Field | Range | Meaning |
|---|---|---|
| Layer (L) | 0..N | Base template family ("stratum"). `0` = Prime (home). Higher layers = alternate base realities (different facility layouts / factions). |
| Ring (R) | 1..10 | Divergence magnitude. `1` = nearly identical, `10` = heavily divergent. |
| Position (P) | 0..999 | Variant index within `(layer, ring)`. Deterministic seed = hash of all three fields. |

**Access gating (gameplay, not physics):**
- A universe is reachable only if the player's `Portal Tuner Tier >= ring` AND `Layer Key >= layer`.
- Otherwise the portal travel attempt is rejected with a UI message; no portal engine change required.
- Prime universe is fixed at `L00-R00-P000` (home base: crafting station, storage, spawn).

## 3. Core Gameplay Loop

`Explore -> Fight -> Gather -> Return -> Craft -> Access Higher Rings`

1. **Explore:** Fire portal to a coordinate; `InterversalPortal` travel switches the player's
   `UniverseBody` to that universe's registered state (physics profile unchanged).
2. **Fight:** Defeat enemies scaled to the ring; earn materials/XP.
3. **Gather:** Harvest resource nodes, open loot containers, scavenge structures.
4. **Return:** Portal back to Prime; deposit inventory.
5. **Craft:** Use crafting station to build upgrades, tuner tiers, layer keys, gear.
6. **Access Higher Rings:** Crafted tuners/keys unlock new coordinate ranges.

## 4. Resource Tiers

| Tier | Rings | Examples | Role |
|---|---|---|---|
| 0 | 1-2 | Scrap, Basic Ore, Wood | Always available; base recipes |
| 1 | 1-2 | Tempered Ore | Minor stat bonuses |
| 2 | 3-4 | Crystal, Alloy | Mid-tier recipes, new nodes |
| 3 | 5-6 | Resonant Core, Bio Sample | Unique enemy drops, facility upgrades |
| 4 | 7-8 | Quantum Filament, Exotic Alloy | Advanced gear |
| 5 | 9-10 | Omnium, Void Shard | Top-tier / endgame |

`ResourceDefinition` (ScriptableObject): `id`, `displayName`, `tier`, `icon`, `stackSize`,
`baseValue`, `spawnWeightByRing` (AnimationCurve), `allowedLayers` (bitmask).

## 5. Enemy Progression

`EnemyDefinition` (ScriptableObject): `id`, `prefab`, `tier`, `healthMultiplier`,
`damageMultiplier`, `lootTable`, `spawnWeightByRing`, `allowedLayers`, `faction`.

- Ring scaling is gameplay-only: HP/damage scale gently (e.g. +8% per ring).
- New enemy types introduced at ring thresholds: ring 3 (faction variants), ring 5 (unique types),
  ring 8 (exotic/dangerous).
- Factions vary by Layer; enemies are spawned from `EnemySpawnTable` weighted by ring + layer.
- Loot drops use `LootTable` (ScriptableObject) keyed by ring tier.

## 6. Universe Generation / Content Variation Rules

A single `UniverseContentGenerator` (MonoBehaviour, one per scene) reads the current coordinate
and deterministically spawns content using a seeded `System.Random`:

1. **Base template:** selected by Layer (prefab set / additive scene). For MVP, one base arena
   scene + variation rules.
2. **Enemies:** count = `f(ring)`; types from `EnemySpawnTable` weighted by ring + layer.
3. **Resource nodes:** from `ResourceSpawnTable`; new node types appear at ring 3+.
4. **Structures:** each structure slot picks a `StructureVariant` (intact / destroyed / repaired)
   based on ring thresholds + seed.
5. **Loot containers:** `LootTable` by ring tier.
6. **Visual variation:** material swaps and post-processing volume weights by ring (no physics).

Variation thresholds live in `UniverseVariationProfile` (ScriptableObject, one per Layer).

## 7. Required Unity Systems & Data Structures

ScriptableObjects (under `Assets/Game/Definitions`):
- `ResourceDefinition`
- `EnemyDefinition`
- `LootTable`
- `CraftingRecipe` (inputs: `ResourceDefinition`+counts; outputs: item/upgrade; `requiredRing`, `requiredLayer`)
- `UniverseVariationProfile`
- `SpawnTable<T>` (generic weighted entries)
- `UniverseCoordinate` (serializable struct, not an asset)

Runtime services:
- `UniverseManager` (singleton): current coordinate, travel requests, content lifecycle.
- `UniverseContentGenerator`: spawns content for a coordinate.
- `InventoryService`, `CraftingService`.
- `PortalUniverseBridge`: hooks `InterversalPortal` travel completion -> `UniverseManager.EnterUniverse(coordinate)`.
- `ProgressionState` (runtime, serialized for save): unlocked rings/layers, inventory, crafted upgrades.

Save: extend existing `SaveGameData` with a `ProgressionState` snapshot.

## 8. Reusable Unchanged (Portal Engine)

- `PortalTraverser`, `PortalPlacer`, `PortalPair`, `PortalLifecycleManager`
- `InterversalPortalSystem` (universe registry/states) — register each visited universe as a `UniverseState`
- `UniverseBody` (keep default physics profile)
- `PortalVisual`, `PortalWindowRenderer`, `PortalSwirl.shader`
- Energy system (optional gating of portal creation)
- Diagnostics, save system (extended), input, scene bootstrap (`PortalSceneBootstrap`)

## 9. Implementation Roadmap (MVP -> Full)

| Milestone | Scope | Complexity | Est. |
|---|---|---|---|
| 0 — Data foundation | ScriptableObjects, coordinate struct, enums, asset folders | Low | 1 wk |
| 1 — MVP loop | Prime + 3 universes (ring 1-2, layer 0), 2 enemy types, 2 resources, 3 recipes, portal hooks | Medium | 2 wk |
| 2 — Content breadth | Rings 1-5, 2 layers, enemy variants, resource nodes, loot, structure variants | Medium | 2-3 wk |
| 3 — Progression gating | Tuner tiers, layer keys, unlock UI, save/load | Medium | 1-2 wk |
| 4 — High rings | Rings 6-10, exotic resources/enemies, visual variation, danger scaling | Medium-High | 2 wk |
| 5 — Polish | UI, audio, balance, performance, save robustness | Medium | 2 wk |

**Total: ~10-12 weeks for one developer.**

## 10. C# Architecture Suggestions

- Namespaces: `Game.Universes`, `Game.Inventory`, `Game.Crafting`, `Game.Progression`.
- Assets under `Assets/Game/Definitions`; prefabs under `Assets/Game/Prefabs`.
- Event-driven: `UniverseEvents.OnUniverseEntered`, `InventoryEvents.OnChanged` (C# `event` or a tiny event bus).
- For MVP, a `UniverseManager` singleton is acceptable; refactor to a service locator later.
- Deterministic seeding: `new System.Random(coordinate.GetHashCode())`; use for spawn positions/variants.
- Keep portal engine untouched: integrate only through `InterversalPortalSystem` registration and
  `UniverseBody` assignment; never alter physics profiles or portal math.

## 11. Avoiding Feature Creep

- No new physics profiles, gravity, or time scaling.
- No procedural mesh/level generation; use prefab variants + seeded selection.
- No multiplayer.
- No new portal rendering; reuse `PortalVisual` / `PortalWindowRenderer`.
- Cap rings at 10 and layers at ~4 for v1; expand only after playtesting.
- Defer the "Architecture C" contact-aware portal physics (documented, not implemented) — it is
  unrelated to this gameplay loop.
