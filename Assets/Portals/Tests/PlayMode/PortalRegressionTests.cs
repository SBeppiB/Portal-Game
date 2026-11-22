using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Portals.Core;
using Portals.Integration;
using Portals.Traversal;
using UnityEngine;
using UnityEngine.TestTools;

namespace Portals.Tests
{
    public class PortalRegressionTests
    {
        private readonly List<GameObject> m_Objects = new List<GameObject>();

        private GameObject Track(GameObject gameObject)
        {
            m_Objects.Add(gameObject);
            return gameObject;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in m_Objects)
                if (gameObject != null) Object.DestroyImmediate(gameObject);
            m_Objects.Clear();
        }

        private static FieldInfo Field(object target, string name) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

        [UnityTest]
        public IEnumerator RestoredControls_UseTPairSlashAndNoGEquip()
        {
            PortalFiringController firing = Track(new GameObject("Firing"))
                .AddComponent<PortalFiringController>();

            Assert.That(Field(firing, "m_EquipKey").GetValue(firing), Is.EqualTo(KeyCode.T));
            Assert.That((int)Field(firing, "m_PersistenceKey").GetValue(firing), Is.EqualTo((int)KeyCode.N));
            Assert.That((int)Field(firing, "m_CloseAllKey").GetValue(firing), Is.EqualTo((int)KeyCode.B));

            yield break;
        }

        private LocaversalPortal CreateWallPortal(string name, Vector3 position)
        {
            GameObject wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.name = name + " Wall";
            wall.transform.position = position + Vector3.back * 0.25f;
            wall.transform.localScale = new Vector3(8f, 8f, 0.5f);

            GameObject portalObject = Track(new GameObject(name));
            portalObject.transform.SetPositionAndRotation(position, Quaternion.identity);
            LocaversalPortal portal = portalObject.AddComponent<LocaversalPortal>();
            portal.SetActive(true);
            return portal;
        }

        [UnityTest]
        public IEnumerator PitchedExit_LeavesCharacterControllerUpright()
        {
            LocaversalPortal a = CreateWallPortal("Entrance", new Vector3(0f, 2f, 0.05f));
            LocaversalPortal b = CreateWallPortal("Exit", new Vector3(10f, 2f, 0.05f));
            a.SetPair(b);
            // A deliberately tilted exit portal (as could be hit on a sloped surface).
            b.transform.rotation = Quaternion.Euler(60f, 90f, 0f);

            GameObject player = Track(new GameObject("Player"));
            player.transform.position = new Vector3(0f, 0.1f, 3f);
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = new Vector3(0f, 1f, 0f);

            GameObject cameraObject = Track(new GameObject("Camera"));
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.SetParent(player.transform, false);

            PortalTraverser traverser = player.AddComponent<PortalTraverser>();
            traverser.AutoDetect = false;
            traverser.SetPortals(a, b);
            traverser.TickDetection();

            int traversals = 0;
            traverser.Traversed += _ => traversals++;
            for (int i = 0; i < 30; i++)
            {
                controller.Move(Vector3.back * 0.1f);
                if (traverser.TickDetection()) break;
            }

            Assert.That(traversals, Is.EqualTo(1));
            Assert.That(Vector3.Angle(player.transform.up, Vector3.up), Is.LessThan(1f),
                "Player transform must stay upright.");
            Assert.That(Vector3.Dot(camera.transform.up, Vector3.up), Is.GreaterThan(0.9f),
                "Camera must not be rolled or upside down.");

            yield break;
        }

        [UnityTest]
        public IEnumerator CloseAll_ClosesBothEndpointsAndVisuals()
        {
            GameObject systemsObject = Track(new GameObject("Portal Systems"));
            PortalLifecycleManager lifecycle = systemsObject.AddComponent<PortalLifecycleManager>();
            lifecycle.Initialize();

            PortalPlacer placer = Track(new GameObject("Placer")).AddComponent<PortalPlacer>();
            placer.SetEnergySystem(null);
            lifecycle.Configure(placer, null);

            LocaversalPortal a = CreateWallPortal("A", new Vector3(0f, 2f, 0.05f));
            LocaversalPortal b = CreateWallPortal("B", new Vector3(10f, 2f, 0.05f));
            a.SetPair(b);
            PortalVisual visualA = a.gameObject.AddComponent<PortalVisual>();
            PortalVisual visualB = b.gameObject.AddComponent<PortalVisual>();
            PortalVisualColours.Apply(visualA, PortalKind.Locaversal, a.OpeningHalfSize);
            PortalVisualColours.Apply(visualB, PortalKind.Locaversal, b.OpeningHalfSize);
            visualA.SetAnimationDurations(0.01f, 0.01f);
            visualB.SetAnimationDurations(0.01f, 0.01f);

            lifecycle.RegisterPair(PortalKind.Locaversal, a, b);
            lifecycle.CloseAll();

            Assert.That(a.IsActive, Is.False);
            Assert.That(b.IsActive, Is.False);
            Assert.That(visualA.IsClosing && visualB.IsClosing, Is.True,
                "Both endpoints must start shrinking together.");

            yield return new WaitForSeconds(0.2f);

            Assert.That(visualA.SurfaceObject.activeSelf, Is.False);
            Assert.That(visualB.SurfaceObject.activeSelf, Is.False);
            Assert.That(lifecycle.HasActivePair, Is.False);

            yield break;
        }

        [UnityTest]
        public IEnumerator ExistingAuthoredPortals_AreRepairedWithLifecycleAndVisuals()
        {
            GameObject player = Track(new GameObject("Player"));
            player.AddComponent<CharacterController>();
            GameObject cameraObject = Track(new GameObject("Camera"));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.SetParent(player.transform, false);

            LocaversalPortal a = CreateWallPortal("Portal A", new Vector3(0f, 2f, 0.05f));
            LocaversalPortal b = CreateWallPortal("Portal B", new Vector3(10f, 2f, 0.05f));
            a.SetPair(b);

            Assert.That(PortalSceneSetup.EnsureSetup(out _), Is.True);

            GameObject systems = GameObject.Find("Portal Systems");
            if (systems != null) Track(systems);
            Assert.That(Object.FindFirstObjectByType<PortalLifecycleManager>(), Is.Not.Null);
            Assert.That(a.GetComponent<PortalVisual>(), Is.Not.Null);
            Assert.That(b.GetComponent<PortalVisual>(), Is.Not.Null);
            Assert.That(player.GetComponent<PortalFiringController>().Lifecycle, Is.Not.Null);

            yield break;
        }
    }
}
