using System;
using System.Collections.Generic;
using UnityEngine;

namespace Portals.Save
{
    /// <summary>
    /// Serializable save-game payload. Only plain serialisable classes, primitives
    /// and lists - never Unity objects, GameObjects, Components or scene references.
    /// Cross-scene identity uses stable string identifiers (scene object names).
    /// </summary>
    [Serializable]
    public class SaveGameData
    {
        /// <summary>Save format version; bump when the schema changes (see SaveSystem.Migrate).</summary>
        public int version = SaveSystem.CurrentVersion;

        public int slot;
        public string saveName = "";
        public string sceneName = "";
        /// <summary>ISO-8601 round-trip timestamp of the save.</summary>
        public string savedAtIso = "";

        public PlayerStateData player = new PlayerStateData();
        public List<InventoryItemData> inventory = new List<InventoryItemData>();
        public PortalGunStateData portalGun = new PortalGunStateData();
        public EnergyStateData energy = new EnergyStateData();
        public WorldStateData world = new WorldStateData();
        public ProgressionStateData progression = new ProgressionStateData();
    }

    [Serializable]
    public class PlayerStateData
    {
        public bool hasTransform;
        public float px, py, pz;
        public float rx, ry, rz, rw;
        /// <summary>Camera pitch (local X euler) so first-person view restores exactly.</summary>
        public float cameraPitch;
        public bool hasCameraPitch;

        public bool hasHealth;
        public float health;
        public float maxHealth;

        public float stamina;
        public float maxStamina;

        public List<AbilityCooldownData> cooldowns = new List<AbilityCooldownData>();
        /// <summary>Unlocked abilities/upgrades (stable string ids).</summary>
        public List<string> unlockedAbilities = new List<string>();
        /// <summary>Movement upgrades (stable string ids).</summary>
        public List<string> upgrades = new List<string>();
    }

    [Serializable]
    public class AbilityCooldownData
    {
        public string ability;
        public float remaining;
    }

    [Serializable]
    public class InventoryItemData
    {
        /// <summary>Stable identifier: scene object name (or InteractableObject display name).</summary>
        public string itemId;
        public int slot;
        public int quantity = 1;
    }

    [Serializable]
    public class PortalGunStateData
    {
        public bool equipped;
        /// <summary>0 = Locaversal, 1 = Interversal (PortalKind cast).</summary>
        public int travelMode;
        public bool selectedPortalA = true;
        public int dimensionIndex;
        public bool present;
    }

    [Serializable]
    public class EnergyStateData
    {
        public bool present;
        public float current;
        public float capacity;
    }

    [Serializable]
    public class WorldStateData
    {
        /// <summary>Ids of world objects that were collected/consumed and must not reappear.</summary>
        public List<string> collectedIds = new List<string>();
        /// <summary>Poses of world objects (props etc.) so dropped items stay where they were.</summary>
        public List<WorldObjectData> objects = new List<WorldObjectData>();
        /// <summary>Portal pair persistence flag at save time.</summary>
        public bool portalPersistence;
        /// <summary>Stable keys of harvested resource nodes, so they stay harvested across loads.</summary>
        public List<string> harvestedNodes = new List<string>();
    }

    [Serializable]
    public class WorldObjectData
    {
        public string id;
        public float px, py, pz;
        public float rx, ry, rz, rw;
        public bool active;
    }

    [Serializable]
    public class ProgressionStateData
    {
        public int portalTunerMaxRing = 2;
        public int portalTunerMaxLayer = 0;
        public List<string> discoveredCoordinates = new List<string>();
        public List<string> craftedUnlocks = new List<string>();
        /// <summary>Universe the player was in when the save was taken (coordinate string).</summary>
        public string currentUniverse = "";
    }

    /// <summary>
    /// Implemented by gameplay components that own persistent state. Components
    /// register themselves in <see cref="SaveStateRegistry"/> so the bridge can
    /// capture/apply without the save layer having to reference concrete gameplay
    /// types (several of which live in Assembly-CSharp).
    /// </summary>
    public interface IGameplaySaveState
    {
        /// <summary>Write this component's state into the save payload.</summary>
        void CaptureInto(SaveGameData data);

        /// <summary>
        /// Restore state from the payload. Returns false only when restoration
        /// genuinely failed (missing data is a no-op and returns true).
        /// </summary>
        bool ApplyFrom(SaveGameData data);
    }

    /// <summary>Live registry of save-state components (registered OnEnable, removed OnDisable).</summary>
    public static class SaveStateRegistry
    {
        private static readonly List<IGameplaySaveState> s_All = new List<IGameplaySaveState>();

        public static IReadOnlyList<IGameplaySaveState> All => s_All;

        public static void Register(IGameplaySaveState state)
        {
            if (state != null && !s_All.Contains(state)) s_All.Add(state);
        }

        public static void Unregister(IGameplaySaveState state)
        {
            s_All.Remove(state);
        }

        /// <summary>Test hook: drop stale entries (components destroyed without OnDisable).</summary>
        public static void Clear() => s_All.Clear();
    }
}
