using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Portals.Core;
using Portals.Integration;
using Portals.Traversal;
using UnityEngine;
using UnityEngine.TestTools;

namespace Portals.Tests
{
    public class PortalIntegrationTests
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

        [UnityTest]
        public IEnumerator SceneSetup_CreatesPairedPortalsAndFiringPlacesThem()
        {
            GameObject player = Track(new GameObject("Player"));
            player.AddComponent<CharacterController>();

            GameObject cameraObject = Track(new GameObject("Test Camera"));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            cameraObject.transform.localRotation = Quaternion.LookRotation(Vector3.down);

            GameObject floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floor.name = "Test Floor";
            floor.transform.position = new Vector3(0f, -0.1f, 0f);
            floor.transform.localScale = new Vector3(20f, 0.2f, 20f);
            Physics.SyncTransforms();

            Assert.That(PortalSceneSetup.EnsureSetup(out _), Is.True);

            GameObject systems = GameObject.Find("Portal Systems");
            GameObject portalAObject = GameObject.Find(PortalSceneSetup.PortalAName);
            GameObject portalBObject = GameObject.Find(PortalSceneSetup.PortalBName);
            GameObject spawn = GameObject.Find("SpawnPoint");

            Assert.That(systems, Is.Not.Null);
            Assert.That(portalAObject, Is.Not.Null);
            Assert.That(portalBObject, Is.Not.Null);
            Assert.That(spawn, Is.Not.Null);
            if (systems != null) Track(systems);
            if (portalAObject != null) Track(portalAObject);
            if (portalBObject != null) Track(portalBObject);
            if (spawn != null) Track(spawn);

            LocaversalPortal portalA = portalAObject.GetComponent<LocaversalPortal>();
            LocaversalPortal portalB = portalBObject.GetComponent<LocaversalPortal>();

            Assert.That(portalA.ConfiguredPartner, Is.SameAs(portalB));
            Assert.That(portalA.IsActive, Is.False);
            Assert.That(portalB.IsActive, Is.False);
            Assert.That(player.GetComponent<PortalTraverser>(), Is.Not.Null);
            Assert.That(player.GetComponent<PortalPlacer>(), Is.Not.Null);
            Assert.That(player.GetComponent<PortalFiringController>(), Is.Not.Null);
            Assert.That(portalAObject.GetComponent<PortalDebugVisualizer>(), Is.Not.Null);
            Assert.That(portalBObject.GetComponent<PortalDebugVisualizer>(), Is.Not.Null);

            PortalFiringController firing = player.GetComponent<PortalFiringController>();
            firing.Fire(portalA);

            Assert.That(portalA.IsActive, Is.True);
            Assert.That(portalA.Position.y, Is.EqualTo(0.05f).Within(0.05f));
            Assert.That(portalB.IsActive, Is.False);

            yield break;
        }
    }
}
