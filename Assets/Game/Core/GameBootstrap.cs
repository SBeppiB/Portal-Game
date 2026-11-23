using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Portals.Core;

namespace Game
{
    /// <summary>Auto-wires gameplay references at runtime so the loop is playable without manual inspector wiring.</summary>
    public class GameBootstrap : MonoBehaviour
    {
        // The gameplay scene has no authored GameBootstrap instance, so create one
        // whenever MAINSCENE finishes loading. Gating on the scene name keeps menu
        // and PlayMode test scenes untouched.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterAutoRun()
        {
            SceneManager.sceneLoaded -= OnAnySceneLoaded;
            SceneManager.sceneLoaded += OnAnySceneLoaded;
        }

        private static void OnAnySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "MAINSCENE") return;
            if (FindFirstObjectByType<GameBootstrap>() != null) return;
            var go = new GameObject("Game Bootstrap");
            go.AddComponent<GameBootstrap>();
        }

        private void Awake()
        {
            GameObject systems = GameObject.Find("Gameplay Systems");
            if (systems == null) systems = new GameObject("Gameplay Systems");

            var tuner = FindFirstObjectByType<PortalTuner>();
            if (tuner == null) tuner = systems.AddComponent<PortalTuner>();

            var inventory = FindFirstObjectByType<ResourceInventory>();
            // Home storage inventory (distinct from player carry inventory).
            ResourceInventory storageInventory = null;
            var home = FindFirstObjectByType<HomeBaseManager>();
            if (home == null)
            {
                var homeGo = new GameObject("Home Base");
                homeGo.transform.position = Vector3.zero;
                var col = homeGo.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(6f, 3f, 6f);
                home = homeGo.AddComponent<HomeBaseManager>();
            }

            // A kinematic body guarantees the home trigger receives OnTriggerEnter
            // from the player's CharacterController (a trigger with no Rigidbody at
            // all can miss CharacterController overlaps).
            if (home.GetComponent<Rigidbody>() == null)
            {
                var homeBody = home.gameObject.AddComponent<Rigidbody>();
                homeBody.isKinematic = true;
                homeBody.useGravity = false;
            }

            // Player carry inventory.
            var player = GameObject.Find("Player");
            if (player == null)
            {
                var cc = FindFirstObjectByType<CharacterController>();
                if (cc != null) player = cc.gameObject;
            }
            ResourceInventory playerInventory = null;
            if (player != null)
            {
                playerInventory = player.GetComponent<ResourceInventory>();
                if (playerInventory == null) playerInventory = player.AddComponent<ResourceInventory>();
            }
            if (playerInventory == null)
            {
                playerInventory = systems.AddComponent<ResourceInventory>();
            }

            // Persist the player's position/rotation across save/load.
            if (player != null && player.GetComponent<PlayerSaveState>() == null)
                player.AddComponent<PlayerSaveState>();

            // Universe manager + content generator.
            var universe = FindFirstObjectByType<UniverseManager>();
            if (universe == null)
            {
                var ugo = new GameObject("Universe Manager");
                ugo.transform.SetParent(systems.transform, false);
                universe = ugo.AddComponent<UniverseManager>();
            }
            var generator = universe.GetComponent<UniverseContentGenerator>();
            if (generator == null) generator = universe.gameObject.AddComponent<UniverseContentGenerator>();

            // Bridge.
            var bridge = FindFirstObjectByType<PortalUniverseBridge>();
            if (bridge == null)
            {
                bridge = systems.AddComponent<PortalUniverseBridge>();
            }

            // Wire HomeBaseManager.
            if (home.Storage == null) SetField(home, "m_Storage", home.GetComponent<ResourceInventory>() ?? storageInventory);
            // Ensure a storage inventory exists on home.
            var homeStorage = home.GetComponent<ResourceInventory>();
            if (homeStorage == null) homeStorage = home.gameObject.AddComponent<ResourceInventory>();
            SetField(home, "m_Storage", homeStorage);
            if (home.Tuner == null) SetField(home, "m_Tuner", tuner);

            // Wire UniverseManager.
            SetField(universe, "m_Generator", generator);
            SetField(universe, "m_HomeBase", home);
            if (universe.GetComponent<UniverseContentGenerator>() == null)
                universe.gameObject.AddComponent<UniverseContentGenerator>();

            // Wire bridge.
            if (bridge.GetComponent<UniverseManager>() == null)
                SetField(bridge, "m_UniverseManager", universe);

            // Load generated assets.
            var db = Resources.Load<ResourceDatabase>("Game/Generated/ResourceDatabase");
            if (db != null)
            {
                if (playerInventory != null) SetField(playerInventory, "m_Database", db);
                if (homeStorage != null) SetField(homeStorage, "m_Database", db);
            }

            // Build resource node prefabs for generator.
            if (db != null && db.All.Count > 0)
            {
                var prefabs = new List<GameObject>();
                foreach (var res in db.All)
                {
                    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    go.name = "ResourceNode_" + res.Id;
                    go.transform.localScale = Vector3.one * 0.6f;
                    var node = go.AddComponent<ResourceNode>();
                    SetField(node, "m_Resource", res);
                    SetField(node, "m_MinAmount", 3);
                    SetField(node, "m_MaxAmount", 6);
                    var col = go.GetComponent<SphereCollider>();
                    if (col != null) col.isTrigger = true;
                    var rb = go.AddComponent<Rigidbody>();
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    // Keep the template ACTIVE (parked far from play). The generator
                    // Instantiates it; an inactive template would spawn inactive,
                    // unharvestable resource nodes. It has no player nearby so it is
                    // never triggered, and at (10000,10000,10000) it is never seen.
                    go.transform.position = new Vector3(10000f, 10000f, 10000f);
                    prefabs.Add(go);
                }
                SetField(generator, "m_ResourceNodePrefabs", prefabs.ToArray());
            }

            // Crafting station.
            var station = FindFirstObjectByType<CraftingStation>();
            if (station == null)
            {
                station = systems.AddComponent<CraftingStation>();
            }
            SetField(station, "m_Inventory", homeStorage);
            SetField(station, "m_Tuner", tuner);
            var recipes = Resources.LoadAll<CraftingRecipe>("Game/Generated");
            if (recipes != null && recipes.Length > 0)
            {
                var listField = station.GetType().GetField("m_Recipes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (listField != null)
                {
                    var list = listField.GetValue(station) as List<CraftingRecipe>;
                    if (list == null) { list = new List<CraftingRecipe>(); listField.SetValue(station, list); }
                    list.Clear();
                    list.AddRange(recipes);
                }
            }

            // Crafting UI (player-facing, toggled with R).
            var craftingUI = FindFirstObjectByType<CraftingUI>();
            if (craftingUI == null)
            {
                var craftGo = new GameObject("Crafting UI");
                craftGo.transform.SetParent(systems.transform, false);
                craftingUI = craftGo.AddComponent<CraftingUI>();
            }
            SetField(craftingUI, "m_Station", station);

            // Home base readout (self-instantiating).
            var homeUI = FindFirstObjectByType<HomeBaseUI>();
            if (homeUI == null)
            {
                var homeUiGo = new GameObject("Home Base UI");
                homeUiGo.transform.SetParent(systems.transform, false);
                homeUI = homeUiGo.AddComponent<HomeBaseUI>();
            }
            SetField(homeUI, "m_HomeBase", home);

            // Universe selection UI (builds its own Canvas, toggled with U).
            var selectionUI = FindFirstObjectByType<UniverseSelectionUI>();
            if (selectionUI == null)
            {
                var selectionGo = new GameObject("Universe Selection UI");
                selectionGo.transform.SetParent(systems.transform, false);
                selectionUI = selectionGo.AddComponent<UniverseSelectionUI>();
            }
            SetField(selectionUI, "m_Tuner", tuner);
            SetField(selectionUI, "m_UniverseManager", universe);
            var targetPortal = FindFirstObjectByType<InterversalPortal>();
            if (targetPortal != null) SetField(selectionUI, "m_TargetPortal", targetPortal);

            // Persistent menu host: Escape pause menu, F5 quick save, F9 quick load.
            GameMenuController.EnsureAttached();

            // Content parent for universe roots.
            if (universe.GetComponent<Transform>() != null)
            {
                var parentField = universe.GetType().GetField("m_ContentParent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (parentField != null && parentField.GetValue(universe) == null)
                    parentField.SetValue(universe, systems.transform);
            }

            // Populate the starting universe so resources are available immediately.
            // If a save is pending, UniverseManager.ApplyFrom replaces this content.
            universe.EnterUniverse(UniverseCoordinate.Prime.ToString());
        }

        private static void SetField(object target, string fieldName, object value)
        {
            if (target == null || value == null) return;
            var f = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(target, value);
        }
    }
}