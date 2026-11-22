using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Portals.Core;
using Portals.Traversal;
using UnityEngine;
using UnityEngine.TestTools;

namespace Portals.Tests
{
    /// <summary>Covers traversal for generic Rigidbody objects, multiple colliders,
    /// aperture size validation, CharacterController objects and back-side entry.</summary>
    public class UniversalTraversalTests
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

        private static void AssertVectorNear(Vector3 expected, Vector3 actual, float epsilon = 0.05f)
        {
            Assert.That((expected - actual).sqrMagnitude, Is.LessThan(epsilon * epsilon),
                $"Expected {expected} but got {actual}.");
        }

        private LocaversalPortal[] CreatePortals(Quaternion exitRotation)
        {
            GameObject aObject = Track(new GameObject("PortalA"));
            aObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            LocaversalPortal a = aObject.AddComponent<LocaversalPortal>();

            GameObject bObject = Track(new GameObject("PortalB"));
            bObject.transform.SetPositionAndRotation(new Vector3(10f, 0f, 0f), exitRotation);
            LocaversalPortal b = bObject.AddComponent<LocaversalPortal>();

            a.SetPair(b);
            a.SetActive(true);
            b.SetActive(true);
            return new[] { a, b };
        }

        private PortalTraverser CreateTraveller(Vector3 start, LocaversalPortal[] portals,
            bool addCollider = true)
        {
            GameObject traveller = Track(new GameObject("Traveller"));
            traveller.transform.position = start;
            Rigidbody body = traveller.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = false;

            if (addCollider)
            {
                CapsuleCollider capsule = traveller.AddComponent<CapsuleCollider>();
                capsule.radius = 0.4f;
                capsule.height = 1.8f;
            }

            PortalTraverser traverser = traveller.AddComponent<PortalTraverser>();
            traverser.AutoDetect = false;
            traverser.SetPortals(portals);
            return traverser;
        }

        [UnityTest]
        public IEnumerator RigidbodyCapsuleObject_TraversesWithMomentumPreserved()
        {
            LocaversalPortal[] portals = CreatePortals(Quaternion.Euler(0f, 90f, 0f));
            PortalTraverser traverser = CreateTraveller(new Vector3(0f, 0f, 1f), portals);
            Rigidbody body = traverser.GetComponent<Rigidbody>();
            body.linearVelocity = new Vector3(0f, 0f, -10f);

            Assert.That(traverser.TickDetection(), Is.False);
            body.position = new Vector3(0f, 0f, -0.5f);

            Assert.That(traverser.TickDetection(), Is.True);

            Quaternion frame = PortalMath.ComputeFrameRotation(portals[0].Rotation, portals[1].Rotation);
            AssertVectorNear(frame * new Vector3(0f, 0f, -10f), body.linearVelocity, 0.1f);
            Assert.That(body.linearVelocity.magnitude, Is.EqualTo(10f).Within(0.1f));

            yield break;
        }

        [UnityTest]
        public IEnumerator MultipleColliders_ProduceOneTraversalEvent()
        {
            LocaversalPortal[] portals = CreatePortals(Quaternion.identity);
            PortalTraverser traverser = CreateTraveller(new Vector3(0f, 0f, 1f), portals);

            GameObject child = new GameObject("ChildCollider");
            child.transform.SetParent(traverser.transform, false);
            BoxCollider box = child.AddComponent<BoxCollider>();
            box.size = new Vector3(0.5f, 0.5f, 0.5f);

            int traversalCount = 0;
            traverser.Traversed += _ => traversalCount++;

            traverser.TickDetection();
            traverser.GetComponent<Rigidbody>().position = new Vector3(0f, 0f, -0.5f);
            Assert.That(traverser.TickDetection(), Is.True);
            Assert.That(traversalCount, Is.EqualTo(1));

            yield break;
        }

        [UnityTest]
        public IEnumerator ObjectTooLargeForAperture_IsRejected()
        {
            LocaversalPortal[] portals = CreatePortals(Quaternion.identity);
            PortalTraverser traverser = CreateTraveller(new Vector3(0f, 0f, 1f), portals, false);
            BoxCollider large = traverser.gameObject.AddComponent<BoxCollider>();
            large.size = new Vector3(4f, 4f, 4f);

            traverser.TickDetection();
            Vector3 before = traverser.GetComponent<Rigidbody>().position;
            traverser.GetComponent<Rigidbody>().position = new Vector3(0f, 0f, -0.5f);

            Assert.That(traverser.TickDetection(), Is.False);
            AssertVectorNear(new Vector3(0f, 0f, -0.5f), traverser.GetComponent<Rigidbody>().position);
            Assert.That(before.z, Is.GreaterThan(0f));

            yield break;
        }

        [UnityTest]
        public IEnumerator CharacterControllerObject_Traverses()
        {
            LocaversalPortal[] portals = CreatePortals(Quaternion.Euler(0f, 90f, 0f));

            GameObject playerLike = Track(new GameObject("PlayerLike"));
            CharacterController controller = playerLike.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            PortalTraverser traverser = playerLike.AddComponent<PortalTraverser>();
            traverser.AutoDetect = false;
            traverser.SetPortals(portals);

            controller.enabled = false;
            playerLike.transform.position = new Vector3(0f, 0f, 1f);
            controller.enabled = true;
            Assert.That(traverser.TickDetection(), Is.False);

            controller.enabled = false;
            playerLike.transform.position = new Vector3(0f, 0f, -0.5f);
            controller.enabled = true;

            Assert.That(traverser.TickDetection(), Is.True);
            Assert.That(playerLike.transform.position.x, Is.GreaterThan(9f));

            yield break;
        }

        [UnityTest]
        public IEnumerator PlayerSizedCapsuleCollider_TraversesAndKeepsMomentum()
        {
            LocaversalPortal[] portals = CreatePortals(Quaternion.Euler(0f, 90f, 0f));
            PortalTraverser traverser = CreateTraveller(new Vector3(0f, 0f, 1f), portals, false);

            CapsuleCollider capsule = traverser.gameObject.AddComponent<CapsuleCollider>();
            capsule.radius = 0.5f;
            capsule.height = 2f;
            capsule.center = Vector3.zero;

            Rigidbody body = traverser.GetComponent<Rigidbody>();
            body.linearVelocity = new Vector3(0f, 0f, -8f);

            traverser.TickDetection();
            body.position = new Vector3(0f, 0f, -0.5f);

            Assert.That(traverser.TickDetection(), Is.True,
                "A player-sized capsule (r=0.5, h=2) must fit the 2 x 3 m aperture.");
            Assert.That(body.linearVelocity.magnitude, Is.EqualTo(8f).Within(0.1f));

            yield break;
        }

        [UnityTest]
        public IEnumerator GroundedCharacterController_TraversesFloorPortal()
        {
            // Floor portal: forward = world up, aperture lies in the XZ plane.
            GameObject floorPortalObject = Track(new GameObject("FloorPortalA"));
            floorPortalObject.transform.SetPositionAndRotation(
                new Vector3(0f, 0.05f, 0f), Quaternion.LookRotation(Vector3.up, Vector3.forward));
            LocaversalPortal floorPortal = floorPortalObject.AddComponent<LocaversalPortal>();

            GameObject exitObject = Track(new GameObject("FloorPortalB"));
            exitObject.transform.SetPositionAndRotation(
                new Vector3(10f, 1f, 0f), Quaternion.Euler(0f, 90f, 0f));
            LocaversalPortal exit = exitObject.AddComponent<LocaversalPortal>();

            floorPortal.SetPair(exit);
            floorPortal.SetActive(true);
            exit.SetActive(true);

            // Mirrors the Player: origin at feet, CharacterController centred on the origin,
            // plus the decorative child capsule at +1.27 m.
            GameObject player = Track(new GameObject("Player"));
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = Vector3.zero;

            GameObject capsule = new GameObject("Capsule");
            capsule.transform.SetParent(player.transform, false);
            capsule.transform.localPosition = new Vector3(0f, 1.27f, 0f);
            CapsuleCollider capsuleCollider = capsule.AddComponent<CapsuleCollider>();
            capsuleCollider.radius = 0.5f;
            capsuleCollider.height = 2f;
            capsuleCollider.center = Vector3.zero;

            PortalTraverser traverser = player.AddComponent<PortalTraverser>();
            traverser.AutoDetect = false;
            traverser.SetPortals(floorPortal, exit);

            controller.enabled = false;
            player.transform.position = new Vector3(0f, 0f, -0.4f);
            controller.enabled = true;
            Assert.That(traverser.TickDetection(), Is.False);

            // Walk onto the floor portal while staying grounded (origin never crosses the plane).
            controller.enabled = false;
            player.transform.position = new Vector3(0f, 0f, 0f);
            controller.enabled = true;

            Assert.That(traverser.TickDetection(), Is.True,
                "A grounded player whose capsule overlaps a floor portal must traverse.");
            Assert.That(player.transform.position.x, Is.GreaterThan(9f));

            yield break;
        }

        [UnityTest]
        public IEnumerator ActualPlayerColliderSetup_TraversesPortal()
        {
            // Mirrors SampleScene: CharacterController (centre 0, height 2) plus an active
            // child CapsuleCollider at local y = 1.27 (height 2, radius 0.5).
            LocaversalPortal[] portals = CreatePortals(Quaternion.Euler(0f, 90f, 0f));
            portals[0].transform.position = new Vector3(0f, 1f, 0f);
            portals[1].transform.position = new Vector3(10f, 1f, 0f);

            GameObject player = Track(new GameObject("Player"));
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = Vector3.zero;

            GameObject capsule = new GameObject("Capsule");
            capsule.transform.SetParent(player.transform, false);
            capsule.transform.localPosition = new Vector3(0f, 1.27f, 0f);
            CapsuleCollider capsuleCollider = capsule.AddComponent<CapsuleCollider>();
            capsuleCollider.radius = 0.5f;
            capsuleCollider.height = 2f;
            capsuleCollider.center = Vector3.zero;
            capsuleCollider.isTrigger = false;

            PortalTraverser traverser = player.AddComponent<PortalTraverser>();
            traverser.AutoDetect = false;
            traverser.SetPortals(portals);

            controller.enabled = false;
            player.transform.position = new Vector3(0f, 0f, 1f);
            controller.enabled = true;
            Assert.That(traverser.TickDetection(), Is.False);

            controller.enabled = false;
            player.transform.position = new Vector3(0f, 0f, -0.5f);
            controller.enabled = true;

            Assert.That(traverser.TickDetection(), Is.True,
                "Player with the scene's CharacterController + child CapsuleCollider must traverse.");

            yield break;
        }

        [UnityTest]
        public IEnumerator BackSideEntry_UsesUnflippedFrameRotation()
        {
            LocaversalPortal[] portals = CreatePortals(Quaternion.Euler(0f, 90f, 0f));
            PortalTraverser traverser = CreateTraveller(new Vector3(0f, 0f, -1f), portals);
            Rigidbody body = traverser.GetComponent<Rigidbody>();
            body.linearVelocity = new Vector3(0f, 0f, 10f);

            Assert.That(traverser.TickDetection(), Is.False);
            body.position = new Vector3(0f, 0f, 0.5f);

            Assert.That(traverser.TickDetection(), Is.True);

            // Back entry uses exit.rotation * inverse(entrance.rotation) with no half turn.
            Quaternion frame = PortalMath.ComputeFrameRotation(
                portals[0].Rotation, portals[1].Rotation, false);
            AssertVectorNear(frame * new Vector3(0f, 0f, 10f), body.linearVelocity, 0.1f);
            AssertVectorNear(portals[1].Forward * 10f, body.linearVelocity, 0.1f);

            yield break;
        }
    }
}
