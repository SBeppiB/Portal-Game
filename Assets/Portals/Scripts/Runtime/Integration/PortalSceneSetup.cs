using Portals.Core;
using Portals.Energy;
using Portals.Interaction;
using Portals.Traversal;
using UnityEngine;

namespace Portals.Integration
{
    /// <summary>
    /// Idempotent scene wiring for the portal systems. Safe to call from Play Mode or
    /// from an Editor menu item. It never touches the portal maths or traversal code.
    /// </summary>
    public static class PortalSceneSetup
    {
        public const string PortalAName = "Portal A";
        public const string PortalBName = "Portal B";

        /// <summary>
        /// Returns true when anything was created. Returns false when the portals are
        /// already present, so repeated calls cannot duplicate objects.
        /// </summary>
        public static bool EnsureSetup(out string message)
        {
            if (Object.FindFirstObjectByType<LocaversalPortal>() != null &&
                GameObject.Find(PortalAName) != null)
            {
                // Authored portals exist (SampleScene). They may predate the lifecycle
                // manager and PortalVisual, so repair wiring instead of skipping it.
                return RepairExistingSetup(out message);
            }

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                CharacterController controller = Object.FindFirstObjectByType<CharacterController>();
                if (controller != null) player = controller.gameObject;
            }

            if (player == null)
            {
                message = "No Player GameObject (or CharacterController) found; portal setup skipped.";
                Debug.LogWarning($"[PortalSceneSetup] {message}");
                return false;
            }

            Camera camera = Camera.main;
            if (camera != null && camera.transform.parent != player.transform)
            {
                // PlayerMovement writes localRotation for pitch and rotates the player
                // for yaw. The camera must be a child or it will not follow the player.
                camera.transform.SetParent(player.transform, false);
                camera.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                camera.transform.localRotation = Quaternion.identity;
                Debug.Log("[PortalSceneSetup] Parented the main camera to the Player for mouse look.");
            }

            GameObject systems = new GameObject("Portal Systems");
            systems.AddComponent<LocaversalPortalSystem>();
            InterversalPortalSystem universeSystem = systems.AddComponent<InterversalPortalSystem>();
            universeSystem.Initialize();

            EnergyStorage storage = systems.AddComponent<EnergyStorage>();
            PortalEnergySystem energy = systems.AddComponent<PortalEnergySystem>();
            energy.SetStorage(storage);
            energy.Initialize();

            LocaversalPortal portalA = CreatePortal(PortalAName, new Vector3(-4f, 1.5f, 2f), new Color(0.2f, 0.8f, 1f));
            LocaversalPortal portalB = CreatePortal(PortalBName, new Vector3(4f, 1.5f, 2f), new Color(1f, 0.55f, 0.15f));
            portalA.SetPair(portalB);
            portalA.SetActive(false);
            portalB.SetActive(false);

            // Interversal endpoints use the same placer/traversal pipeline. Their universe
            // follows the dimension selected by the portal gun.
            InterversalPortal interversalA = CreateInterversalPortal("Interversal Portal A",
                new Vector3(-6f, 1.5f, 2f), new Color(0.4f, 1f, 0.6f));
            InterversalPortal interversalB = CreateInterversalPortal("Interversal Portal B",
                new Vector3(6f, 1.5f, 2f), new Color(1f, 0.4f, 0.8f));
            interversalA.Configure(universeSystem.SelectedUniverse, interversalB,
                InterversalMappingMode.ViaPortalFrame, Vector3.zero);
            interversalB.Configure(universeSystem.SelectedUniverse, interversalA,
                InterversalMappingMode.ViaPortalFrame, Vector3.zero);
            interversalA.SetActive(false);
            interversalB.SetActive(false);

            PortalPlacer placer = player.GetComponent<PortalPlacer>();
            if (placer == null) placer = player.AddComponent<PortalPlacer>();
            placer.SetEnergySystem(energy);

            PortalLifecycleManager lifecycle = systems.AddComponent<PortalLifecycleManager>();
            lifecycle.Initialize();
            lifecycle.Configure(placer, universeSystem);
            lifecycle.RegisterPair(PortalKind.Locaversal, portalA, portalB);
            lifecycle.RegisterPair(PortalKind.Interversal, interversalA, interversalB);

            PortalFiringController firing = player.GetComponent<PortalFiringController>();
            if (firing == null) firing = player.AddComponent<PortalFiringController>();
            firing.Configure(camera, placer, portalA, portalB);
            firing.ConfigureInterversal(interversalA, interversalB, universeSystem,
                GameObject.Find("PORTAL GUN"));
            firing.ConfigureLifecycle(lifecycle);

            if (player.GetComponent<PortalHUD>() == null)
                player.AddComponent<PortalHUD>();

            if (player.GetComponent<PortalTraverser>() == null)
                player.AddComponent<PortalTraverser>();

            EnsureGameplayComponents(player, camera);

            if (GameObject.Find("SpawnPoint") == null)
            {
                GameObject spawn = new GameObject("SpawnPoint");
                spawn.transform.position = player.transform.position;
                spawn.transform.rotation = player.transform.rotation;
            }

            message = "Portal systems, paired portals, placer, firing input and traverser created.";
            Debug.Log($"[PortalSceneSetup] {message}");
            return true;
        }

        /// <summary>
        /// Repairs scenes that already contain authored portals but were created before
        /// lifecycle/visual support. Adds only missing components; never duplicates systems.
        /// </summary>
        private static bool RepairExistingSetup(out string message)
        {
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                CharacterController controller = Object.FindFirstObjectByType<CharacterController>();
                if (controller != null) player = controller.gameObject;
            }

            if (player == null)
            {
                message = "No Player found; cannot repair existing portal setup.";
                return false;
            }

            Camera camera = Camera.main;
            if (camera != null && camera.transform.parent != player.transform)
            {
                camera.transform.SetParent(player.transform, false);
                camera.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                camera.transform.localRotation = Quaternion.identity;
            }

            GameObject systems = GameObject.Find("Portal Systems");
            if (systems == null) systems = new GameObject("Portal Systems");

            LocaversalPortalSystem localSystem = systems.GetComponent<LocaversalPortalSystem>();
            if (localSystem == null) localSystem = systems.AddComponent<LocaversalPortalSystem>();

            InterversalPortalSystem universeSystem = systems.GetComponent<InterversalPortalSystem>();
            if (universeSystem == null) universeSystem = systems.AddComponent<InterversalPortalSystem>();
            universeSystem.Initialize();

            EnergyStorage storage = systems.GetComponent<EnergyStorage>();
            if (storage == null) storage = systems.AddComponent<EnergyStorage>();

            PortalEnergySystem energy = systems.GetComponent<PortalEnergySystem>();
            if (energy == null) energy = systems.AddComponent<PortalEnergySystem>();
            energy.SetStorage(storage);
            energy.Initialize();

            PortalLifecycleManager lifecycle = Object.FindFirstObjectByType<PortalLifecycleManager>();
            if (lifecycle == null)
            {
                lifecycle = systems.AddComponent<PortalLifecycleManager>();
                lifecycle.Initialize();
            }

            LocaversalPortal[] localPortals = Object.FindObjectsByType<LocaversalPortal>(FindObjectsSortMode.None);
            InterversalPortal[] interversalPortals = Object.FindObjectsByType<InterversalPortal>(FindObjectsSortMode.None);

            EnsureVisuals(localPortals, PortalKind.Locaversal);
            EnsureVisuals(interversalPortals, PortalKind.Interversal);

            PortalPlacer placer = player.GetComponent<PortalPlacer>();
            if (placer == null) placer = player.AddComponent<PortalPlacer>();
            placer.SetEnergySystem(energy);

            lifecycle.Configure(placer, universeSystem);

            LocaversalPortal portalA = localPortals.Length > 0 ? localPortals[0] : null;
            LocaversalPortal portalB = localPortals.Length > 1 ? localPortals[1] : null;
            InterversalPortal interversalA = interversalPortals.Length > 0 ? interversalPortals[0] : null;
            InterversalPortal interversalB = interversalPortals.Length > 1 ? interversalPortals[1] : null;

            if (portalA != null && portalB != null && !IsRegistered(lifecycle, portalA))
                lifecycle.RegisterPair(PortalKind.Locaversal, portalA, portalB);
            if (interversalA != null && interversalB != null && !IsRegistered(lifecycle, interversalA))
            {
                interversalA.Configure(universeSystem.SelectedUniverse, interversalB,
                    InterversalMappingMode.ViaPortalFrame, Vector3.zero);
                interversalB.Configure(universeSystem.SelectedUniverse, interversalA,
                    InterversalMappingMode.ViaPortalFrame, Vector3.zero);
                lifecycle.RegisterPair(PortalKind.Interversal, interversalA, interversalB);
            }

            PortalFiringController firing = player.GetComponent<PortalFiringController>();
            if (firing == null) firing = player.AddComponent<PortalFiringController>();
            firing.Configure(camera, placer, portalA, portalB);
            if (interversalA != null)
                firing.ConfigureInterversal(interversalA, interversalB, universeSystem,
                    GameObject.Find("PORTAL GUN"));
            firing.ConfigureLifecycle(lifecycle);

            if (player.GetComponent<PortalHUD>() == null) player.AddComponent<PortalHUD>();
            if (player.GetComponent<PortalTraverser>() == null) player.AddComponent<PortalTraverser>();

            EnsureGameplayComponents(player, camera);

            if (GameObject.Find("SpawnPoint") == null)
            {
                GameObject spawn = new GameObject("SpawnPoint");
                spawn.transform.position = player.transform.position;
                spawn.transform.rotation = player.transform.rotation;
            }

            message = "Existing portals repaired with lifecycle, visuals and input wiring.";
            Debug.Log($"[PortalSceneSetup] {message}");
            return true;
        }

        /// <summary>
        /// Attaches the gameplay (crosshair/highlight/interaction/inventory/wobble)
        /// components to the actual player exactly once and wires their references.
        /// Must run after PortalTraverser is present so the interaction and wobble
        /// components can subscribe to traversal events in Awake.
        /// </summary>
        private static void EnsureGameplayComponents(GameObject player, Camera camera)
        {
            if (player.GetComponent<PortalCrosshair>() == null)
                player.AddComponent<PortalCrosshair>();

            if (player.GetComponent<PortalHighlighter>() == null)
                player.AddComponent<PortalHighlighter>();

            PortalObjectInteractor interactor = player.GetComponent<PortalObjectInteractor>();
            if (interactor == null) interactor = player.AddComponent<PortalObjectInteractor>();

            PortalInventory inventory = player.GetComponent<PortalInventory>();
            if (inventory == null) inventory = player.AddComponent<PortalInventory>();
            inventory.Configure(interactor, camera);

            if (player.GetComponent<PortalScreenWobble>() == null)
                player.AddComponent<PortalScreenWobble>();
        }

        private static bool IsRegistered(PortalLifecycleManager lifecycle, PortalEndpoint endpoint)
        {
            if (lifecycle == null || endpoint == null) return false;
            PortalPair active = lifecycle.ActivePair;
            return active != null && active.Contains(endpoint);
        }

        private static void EnsureVisuals(PortalEndpoint[] endpoints, PortalKind kind)
        {
            for (int i = 0; i < endpoints.Length; i++)
            {
                PortalEndpoint endpoint = endpoints[i];
                if (endpoint == null) continue;

                PortalVisual visual = endpoint.GetComponent<PortalVisual>();
                if (visual == null) visual = endpoint.gameObject.AddComponent<PortalVisual>();
                PortalVisualColours.Apply(visual, kind, endpoint.OpeningHalfSize);

                PortalDebugVisualizer debug = endpoint.GetComponent<PortalDebugVisualizer>();
                if (debug != null) debug.SetRuntimeLinesVisible(false);
            }
        }

        private static LocaversalPortal CreatePortal(string name, Vector3 position, Color debugColor)
        {
            GameObject portalObject = new GameObject(name);
            portalObject.transform.position = position;
            portalObject.transform.rotation = Quaternion.identity;

            LocaversalPortal portal = portalObject.AddComponent<LocaversalPortal>();

            PortalDebugVisualizer visualizer = portalObject.AddComponent<PortalDebugVisualizer>();
            visualizer.Configure(portal, debugColor);
            visualizer.SetRuntimeLinesVisible(false);

            PortalVisualColours.Apply(portalObject.AddComponent<PortalVisual>(),
                PortalKind.Locaversal, portal.OpeningHalfSize);

            return portal;
        }

        private static InterversalPortal CreateInterversalPortal(string name, Vector3 position, Color debugColor)
        {
            GameObject portalObject = new GameObject(name);
            portalObject.transform.position = position;
            portalObject.transform.rotation = Quaternion.identity;

            InterversalPortal portal = portalObject.AddComponent<InterversalPortal>();

            PortalDebugVisualizer visualizer = portalObject.AddComponent<PortalDebugVisualizer>();
            visualizer.Configure(portal, debugColor);
            visualizer.SetRuntimeLinesVisible(false);

            PortalVisualColours.Apply(portalObject.AddComponent<PortalVisual>(),
                PortalKind.Interversal, portal.OpeningHalfSize);

            return portal;
        }
    }
}
