# Alternative Universe Resource Loop — Implementation Notes

## 1. Folder Structure

```
Assets/
  Game/
    Core/
      UniverseCoordinate.cs
      UniverseModifier.cs
      UniverseContentGenerator.cs
    Resources/
      ResourceDefinition.cs
      ResourceStack.cs
      ResourceInventory.cs
      ResourceNode.cs
      ResourceDatabase.cs
    Loot/
      LootTable.cs
    Enemies/
      EnemyDefinition.cs
      EnemyLootDropper.cs
    Progression/
      PortalTuner.cs
    Home/
      HomeBaseManager.cs
    Universe/
      UniverseManager.cs
      PortalUniverseBridge.cs
```

Scripts compile into the default `Assembly-CSharp` assembly and can reference `Portals.Runtime` types.
No portal engine files were modified (only `SaveGameData.cs` gained a `ProgressionStateData` field).

## 2. Class List

| Class | Type | Purpose |
|---|---|---|
| `UniverseCoordinate` | struct | Serializable L-R-P address with parse/equality/hash |
| `UniverseModifier` | enum | Deterministic content modifier |
| `UniverseContentGenerator` | MonoBehaviour | Seeded spawn of resources/enemies |
| `ResourceDefinition` | ScriptableObject | Resource data (id, name, tier, icon, desc) |
| `ResourceStack` | serializable class | Resource + amount |
| `ResourceInventory` | MonoBehaviour | Add/remove/contains/save-load (IGameplaySaveState) |
| `ResourceDatabase` | ScriptableObject | Id -> ResourceDefinition lookup |
| `ResourceNode` | MonoBehaviour | Harvestable node with respawn |
| `LootTable` | ScriptableObject | Weighted loot entries |
| `EnemyDefinition` | ScriptableObject | Enemy prefab, stats, loot, ring gating |
| `EnemyLootDropper` | MonoBehaviour | Drops loot from table into inventory |
| `PortalTuner` | MonoBehaviour | Ring/layer access, persists (IGameplaySaveState) |
| `HomeBaseManager` | MonoBehaviour | Storage, discovery, persists (IGameplaySaveState) |
| `UniverseManager` | MonoBehaviour | Activates/deactivates universe content roots |
| `PortalUniverseBridge` | MonoBehaviour | Hooks portal traversal -> UniverseManager |

## 3. Integration Points with Existing Systems

- **Portal traversal:** `PortalUniverseBridge` subscribes to `PortalTraverser.Traversed`. On an
  `InterversalPortal` exit it calls `UniverseManager.EnterUniverse(exit.Universe.ToString())`.
  The portal engine still performs the actual universe switch via `InterversalPortalSystem.SwitchEntityUniverse`.
- **Universe identity:** A coordinate is stored as the `UniverseId` string (e.g. "L03-R02-P044").
  `InterversalPortal.Universe` can be set to `new UniverseId(coord.ToString())`; physics profile stays default.
- **Save system:** `ResourceInventory`, `PortalTuner`, `HomeBaseManager` implement
  `IGameplaySaveState` and register with `SaveStateRegistry` (OnEnable/OnDisable), so the existing
  `GameplaySaveBridge` captures/applies them automatically. `SaveGameData` gained `ProgressionStateData`.
- **Interaction:** `ResourceNode` can be paired with the existing `InteractableObject` component
  for scanning/pickup metadata; harvest via `ResourceNode.Harvest(inventory)`.
- **Energy:** unchanged; portal creation still gated by `PortalEnergySystem`.

## 4. Inspector Setup

1. Create a `ResourceDatabase` asset (Create > Game > Resource Database) and assign all
   `ResourceDefinition` assets to it.
2. Create `ResourceDefinition` assets (Create > Game > Resource Definition) for each resource.
3. Create `LootTable` assets (Create > Game > Loot Table) and assign to `EnemyDefinition`.
4. Create `EnemyDefinition` assets (Create > Game > Enemy Definition) referencing enemy prefabs.
5. Add `ResourceInventory` to the Player (assign `ResourceDatabase`).
6. Add `PortalTuner` to the Player.
7. Add `HomeBaseManager` to the home base object (assign storage inventory + tuner).
8. Add `UniverseManager` to a scene manager object (assign generator + home base + content parent).
9. Add `PortalUniverseBridge` to the Player or a manager (assign `UniverseManager`).
10. Add `UniverseContentGenerator` (assign resource/enemy prefabs) to the same object as
    `UniverseManager` or a child.
11. Place `ResourceNode` components on harvestable objects in hand-authored scenes.

## 5. Required ScriptableObjects

- `ResourceDefinition` (one per resource)
- `ResourceDatabase` (one, aggregates resources)
- `LootTable` (one per enemy tier / biome)
- `EnemyDefinition` (one per enemy type)

## 6. Example Prefabs

- `ResourceNode_Scrap` — cube with `ResourceNode` (resource=Scrap, min=1, max=3, respawn=true).
- `Enemy_Basic` — capsule with `EnemyLootDropper` (lootTable=basic table) and health component.
- `HomeBase` — empty with `HomeBaseManager`, storage inventory, crafting station placeholder.

## 7. Example Testing Plan

1. Create two `ResourceDefinition` assets (Scrap, Copper Wire) and a `ResourceDatabase`.
2. Create a `LootTable` dropping Scrap 1-2 (weight 1).
3. Create an `EnemyDefinition` with a cube prefab, baseHealth 50, minimumRing 1, loot table assigned.
4. Place a `ResourceNode` and an enemy in a test scene with `ResourceInventory` on Player.
5. Harvest node -> verify inventory count increases.
6. Kill enemy -> call `EnemyLootDropper.DropLoot(1)` -> verify inventory gains loot.
7. Place `PortalTuner` on Player; verify `CanAccess(1,0)` true, `CanAccess(5,0)` false.
8. Call `UnlockRingTier(2)` -> verify `CanAccess(4,0)` true.
9. Save/load via existing `SaveSystem` -> verify inventory, tuner, discoveries restore.
10. Place `UniverseContentGenerator` with enemy/resource prefabs; call `Generate(L01-R03-P001, root)` twice -> verify identical spawn counts (determinism).
11. Verify different coordinates produce different modifiers/counts.

## 8. MVP Priority (already implemented in this order)

1. UniverseCoordinate
2. UniverseContentGenerator
3. ResourceDefinition
4. ResourceInventory
5. ResourceNode
6. LootTable
7. EnemyDefinition
8. PortalTuner
9. HomeBaseManager
10. Save integration (IGameplaySaveState + ProgressionStateData)
Plus UniverseManager + PortalUniverseBridge for content lifecycle.
