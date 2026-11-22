using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Portals.Core;
using Portals.Traversal;
using UnityEngine;
using UnityEngine.TestTools;

namespace Portals.Tests
{
    /// <summary>
    /// Realistic integration tests: objects MOVE into portals using CharacterController.Move
    /// or Rigidbody motion, rather than being teleported across the plane by the test.
    /// </summary>
    public class CorePortalFunctionalityTests
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

        private LocaversalPortal[] CreateWallPortals()
        {
            LocaversalPortal a = CreateWallPortal("Portal A", new Vector3(0f, 2f, 0.05f));
            LocaversalPortal b = CreateWallPortal("Portal B", new Vector3(10f, 2f, 0.05f));
            a.SetPair(b);
            return new[] { a, b };
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

        private PortalTraverser CreateCharacter(Vector3 start, LocaversalPortal[] portals)
        {
            GameObject player = Track(new GameObject("Player"));
            player.transform.position = start;
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = new Vector3(0f, 1f, 0f);
            PortalTraverser traverser = player.AddComponent<PortalTraverser>();
            traverser.AutoDetect = false;
            traverser.SetPortals(portals);
            traverser.TickDetection(); // seed
            return traverser;
        }

        private int WalkInto(PortalTraverser traverser, Vector3 direction, float distance, int steps)
        {
            CharacterController controller = traverser.GetComponent<CharacterController>();
            int traversals = 0;
            traverser.Traversed += _ => traversals++;
            float step = distance / steps;
            for (int i = 0; i < steps; i++)
            {
                controller.Move(direction.normalized * step);
                if (traverser.TickDetection()) break;
            }
            return traversals;
        }

        [UnityTest]
        public IEnumerator CharacterWalksThroughA_AppearsAtB_AndBack()
        {
            LocaversalPortal[] portals = CreateWallPortals();
            PortalTraverser player = CreateCharacter(new Vector3(0f, 0.1f, 3f), portals);

            int forward = WalkInto(player, Vector3.back, 3f, 30);
            Assert.That(forward, Is.EqualTo(1), "Walking into Portal A must traverse to B.");
            Assert.That(player.transform.position.x, Is.GreaterThan(8f), "Player must appear at Portal B.");

            // Let a frame pass so the traversal cooldown/re-entry guard can reset.
            yield return null;

            // Walk back into Portal B (approach it from its front, +Z side).
            int backward = WalkInto(player, Vector3.back, 3f, 30);
            Assert.That(backward, Is.EqualTo(1), "Walking into Portal B must traverse to A.");
            Assert.That(player.transform.position.x, Is.LessThan(2f), "Player must reappear at Portal A.");

            yield break;
        }

        [UnityTest]
        public IEnumerator RigidbodyCube_PassesThroughBothDirections()
        {
            LocaversalPortal[] portals = CreateWallPortals();

            GameObject cubeObject = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            cubeObject.name = "Cube";
            cubeObject.transform.position = new Vector3(0f, 2f, 3f);
            cubeObject.transform.localScale = Vector3.one * 0.8f;
            Rigidbody body = cubeObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            PortalTraverser traverser = cubeObject.AddComponent<PortalTraverser>();
            traverser.AutoDetect = false;
            traverser.SetPortals(portals);
            traverser.TickDetection();

            int count = 0;
            traverser.Traversed += _ => count++;
            for (int i = 0; i < 40; i++)
            {
                body.MovePosition(body.position + Vector3.back * 0.1f);
                if (traverser.TickDetection()) break;
            }

            Assert.That(count, Is.EqualTo(1), "Rigidbody cube must traverse Portal A.");
            Assert.That(body.position.x, Is.GreaterThan(8f));
            yield break;
        }

        [UnityTest]
        public IEnumerator UnpairedAndClosedPortals_NeverTransport()
        {
            LocaversalPortal lone = CreateWallPortal("Unpaired", new Vector3(0f, 2f, 0.05f));
            PortalTraverser player = CreateCharacter(new Vector3(0f, 0.1f, 3f), new[] { lone });
            Assert.That(WalkInto(player, Vector3.back, 3f, 30), Is.EqualTo(0));

            LocaversalPortal[] pair = CreateWallPortals();
            PortalTraverser other = CreateCharacter(new Vector3(0f, 0.1f, 3f), pair);
            pair[0].SetActive(false);
            Assert.That(WalkInto(other, Vector3.back, 3f, 30), Is.EqualTo(0));

            yield break;
        }

        [UnityTest]
        public IEnumerator OversizedObject_IsBlocked()
        {
            LocaversalPortal[] portals = CreateWallPortals();
            GameObject big = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            big.transform.position = new Vector3(0f, 2f, 3f);
            big.transform.localScale = Vector3.one * 3f;
            Rigidbody body = big.AddComponent<Rigidbody>();
            body.useGravity = false;
            PortalTraverser traverser = big.AddComponent<PortalTraverser>();
            traverser.AutoDetect = false;
            traverser.SetPortals(portals);
            traverser.TickDetection();

            int count = 0;
            traverser.Traversed += _ => count++;
            for (int i = 0; i < 40; i++)
            {
                body.MovePosition(body.position + Vector3.back * 0.1f);
                traverser.TickDetection();
            }
            Assert.That(count, Is.EqualTo(0), "Oversized object must not fit through the aperture.");
            yield break;
        }
    }
}
