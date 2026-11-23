using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Portals.Core;
using Portals.Energy;
using Portals.Game;
using Portals.Interaction;

namespace Portals.Save
{
    /// <summary>
    /// Captures and restores the live gameplay scene from/to a
    /// <see cref="SaveGameData"/>. Runs only on explicit save/load (never per frame).
    ///
    /// Restore order matters and is fixed here:
    /// 1. registered save-state components (player pose/stamina/health, portal gun,
    ///    inventory claims, ...),
    /// 2. energy storage,
    /// 3. world object poses (skipping anything already claimed by the inventory),
    /// 4. collected-item suppression (ids that must not reappear).
    /// </summary>
    public static class GameplaySaveBridge
    {
        /// <summary>Captures the running gameplay scene. Null when not in gameplay.</summary>
        public static SaveGameData Capture(int slot)
        {
            if (!GameFlow.InGameplay) return null;

            var data = new SaveGameData
            {
                slot = slot,
                saveName = $"Save {slot + 1}",
                sceneName = SceneManager.GetActiveScene().name,
                savedAtIso = System.DateTime.Now.ToString("o")
            };

            // Player, gun, inventory, ... (registered components).
            foreach (IGameplaySaveState state in SaveStateRegistry.All)
            {
                if (IsDead(state)) continue;
                try { state.CaptureInto(data); }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[SaveLoad] Capture failed on {state.GetType().Name}: {e.Message}");
                }
            }

            // Energy pool (a Portals.Runtime type, so the bridge handles it directly).
            EnergyStorage energy = Object.FindFirstObjectByType<EnergyStorage>();
            if (energy != null)
            {
                data.energy.present = true;
                data.energy.current = energy.Current;
                data.energy.capacity = energy.Capacity;
            }

            // Portal pair persistence flag.
            PortalLifecycleManager lifecycle = Object.FindFirstObjectByType<PortalLifecycleManager>();
            data.world.portalPersistence = lifecycle != null && lifecycle.PersistenceEnabled;

            CaptureWorldObjects(data);
            return data;
        }

        /// <summary>Applies saved state to the current scene. False only on real failure.</summary>
        public static bool Apply(SaveGameData data)
        {
            if (data == null) return false;

            foreach (IGameplaySaveState state in SaveStateRegistry.All)
            {
                if (IsDead(state)) continue;
                try
                {
                    if (!state.ApplyFrom(data))
                        Debug.LogWarning($"[SaveLoad] {state.GetType().Name} could not fully restore state.");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[SaveLoad] Apply failed on {state.GetType().Name}: {e.Message}");
                }
            }

            if (data.energy.present)
            {
                EnergyStorage energy = Object.FindFirstObjectByType<EnergyStorage>();
                if (energy != null)
                {
                    if (!Mathf.Approximately(energy.Capacity, data.energy.capacity))
                        energy.SetCapacity(data.energy.capacity, false);
                    energy.DrainAll();
                    energy.Recharge(Mathf.Clamp(data.energy.current, 0f, data.energy.capacity));
                }
            }

            if (data.world.portalPersistence)
            {
                PortalLifecycleManager lifecycle = Object.FindFirstObjectByType<PortalLifecycleManager>();
                if (lifecycle != null && !lifecycle.PersistenceEnabled) lifecycle.TogglePersistence();
            }

            ApplyWorldObjects(data);
            return true;
        }

        private static bool IsDead(IGameplaySaveState state) =>
            state is Object unityObject && unityObject == null;

        // ------------------------------------------------------------------
        // World object poses (props, dropped items) keyed by stable names.
        // ------------------------------------------------------------------

        private static void CaptureWorldObjects(SaveGameData data)
        {
            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (InteractableObject interactable in
                         root.GetComponentsInChildren<InteractableObject>(true))
                {
                    // Objects parented into the player/inventory are owned by the
                    // inventory list, not the world pose list.
                    if (!IsSceneRooted(interactable.transform, scene)) continue;

                    data.world.objects.Add(new WorldObjectData
                    {
                        id = interactable.gameObject.name,
                        px = interactable.transform.position.x,
                        py = interactable.transform.position.y,
                        pz = interactable.transform.position.z,
                        rx = interactable.transform.rotation.x,
                        ry = interactable.transform.rotation.y,
                        rz = interactable.transform.rotation.z,
                        rw = interactable.transform.rotation.w,
                        active = interactable.gameObject.activeSelf
                    });
                }
            }
        }

        private static void ApplyWorldObjects(SaveGameData data)
        {
            if (data.world.objects.Count == 0 && data.world.collectedIds.Count == 0) return;

            Scene scene = SceneManager.GetActiveScene();
            var claimed = new Dictionary<string, Transform>(data.world.objects.Count);
            foreach (WorldObjectData saved in data.world.objects)
                if (!claimed.ContainsKey(saved.id)) claimed[saved.id] = null;

            var seen = new HashSet<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    string id = t.gameObject.name;
                    if (!claimed.ContainsKey(id) || seen.Contains(id)) continue;

                    // Inventory-owned objects are inactive or parented to the player;
                    // leave them alone (the inventory list owns them).
                    if (!IsSceneRooted(t, scene)) continue;

                    seen.Add(id);
                    claimed[id] = t;

                    // Collected ids must not reappear.
                    if (data.world.collectedIds.Contains(id))
                    {
                        t.gameObject.SetActive(false);
                        continue;
                    }

                    foreach (WorldObjectData saved in data.world.objects)
                    {
                        if (saved.id != id) continue;
                        t.SetPositionAndRotation(
                            new Vector3(saved.px, saved.py, saved.pz),
                            new Quaternion(saved.rx, saved.ry, saved.rz, saved.rw));
                        if (t.gameObject.activeSelf != saved.active)
                            t.gameObject.SetActive(saved.active);
                        break;
                    }
                }
            }
        }

        private static bool IsSceneRooted(Transform t, Scene scene)
        {
            Transform current = t;
            while (current != null)
            {
                if (current.gameObject.scene != scene) return false;
                if (current.parent == null) return true;
                current = current.parent;
            }
            return false;
        }
    }
}
