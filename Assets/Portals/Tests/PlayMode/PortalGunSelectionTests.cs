using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Portals.Core;
using Portals.Integration;
using UnityEngine;
using UnityEngine.TestTools;

namespace Portals.Tests
{
    public class PortalGunSelectionTests
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

        private PortalFiringController CreateSetup(out GameObject player, out GameObject gun,
            out GameObject floor, out Camera camera, out InterversalPortalSystem universes)
        {
            player = Track(new GameObject("Player"));
            player.AddComponent<CharacterController>();

            GameObject cameraObject = Track(new GameObject("Test Camera"));
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            cameraObject.transform.localRotation = Quaternion.LookRotation(Vector3.down);

            floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floor.name = "Test Floor";
            floor.transform.position = new Vector3(0f, -0.1f, 0f);
            floor.transform.localScale = new Vector3(10f, 0.2f, 10f);

            gun = Track(new GameObject("PORTAL GUN"));

            Physics.SyncTransforms();
            Assert.That(PortalSceneSetup.EnsureSetup(out _), Is.True);

            foreach (GameObject created in new[]
            {
                GameObject.Find("Portal Systems"), GameObject.Find("Portal A"),
                GameObject.Find("Portal B"), GameObject.Find("Interversal Portal A"),
                GameObject.Find("Interversal Portal B"), GameObject.Find("SpawnPoint")
            })
            {
                if (created != null) Track(created);
            }

            universes = GameObject.Find("Portal Systems").GetComponent<InterversalPortalSystem>();
            return player.GetComponent<PortalFiringController>();
        }

        [UnityTest]
        public IEnumerator GunToggle_HidesModelAndBlocksFiringWhenUnequipped()
        {
            PortalFiringController firing = CreateSetup(out _, out GameObject gun, out GameObject floor,
                out Camera camera, out _);

            Assert.That(firing.IsEquipped, Is.False);
            Assert.That(gun.activeSelf, Is.False);
            Assert.That(firing.SelectedPortalLabel, Is.EqualTo("A"));

            LocaversalPortal portalA = firing.PortalA;
            LocaversalPortal portalB = firing.PortalB;

            firing.Fire();
            Assert.That(portalA.IsActive, Is.False, "Firing must be blocked while unequipped.");

            firing.ToggleEquipped();
            Assert.That(firing.IsEquipped, Is.True);
            Assert.That(gun.activeSelf, Is.True);

            firing.Fire();
            Assert.That(portalA.IsActive, Is.True);
            Assert.That(portalB.IsActive, Is.False);
            Assert.That(firing.SelectedPortalLabel, Is.EqualTo("B"));

            // Provide a second, separated surface so the next shot is not rejected as overlapping.
            floor.transform.position = new Vector3(5f, -0.1f, 0f);
            camera.transform.localPosition = new Vector3(5f, 1.6f, 0f);
            Physics.SyncTransforms();

            firing.Fire();
            Assert.That(portalB.IsActive, Is.True);
            Assert.That(firing.SelectedPortalLabel, Is.EqualTo("A"));

            firing.ToggleEquipped();
            Assert.That(gun.activeSelf, Is.False);

            yield break;
        }

        [UnityTest]
        public IEnumerator ModeAndDimensionSelection_UpdateStateImmediately()
        {
            PortalFiringController firing = CreateSetup(out GameObject player, out _, out _, out _,
                out InterversalPortalSystem universes);

            Assert.That(firing.TravelModeLabel, Is.EqualTo("LOCAVERSAL"));
            Assert.That(player.GetComponent<PortalHUD>(), Is.Not.Null);

            firing.ToggleTravelMode();
            Assert.That(firing.TravelModeLabel, Is.EqualTo("INTERVERSAL"));
            Assert.That(firing.TravelMode, Is.EqualTo(PortalKind.Interversal));

            firing.SelectDimension(2);
            Assert.That(firing.SelectedDimensionIndex, Is.EqualTo(2));
            Assert.That(universes.SelectedUniverse, Is.EqualTo(new UniverseId("Universe 3")));
            Assert.That(firing.SelectedDimensionName, Is.EqualTo("Dimension 3"));

            InterversalPortal interversalA = firing.InterversalA;
            InterversalPortal interversalB = firing.InterversalB;
            Assert.That(interversalA, Is.Not.Null);
            Assert.That(interversalB, Is.Not.Null);
            Assert.That(interversalA.Universe, Is.EqualTo(universes.SelectedUniverse));
            Assert.That(interversalB.Universe, Is.EqualTo(universes.SelectedUniverse));

            firing.ToggleTravelMode();
            Assert.That(firing.TravelModeLabel, Is.EqualTo("LOCAVERSAL"));

            yield break;
        }
    }
}
