using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Portals.Core;
using Portals.Traversal;
using UnityEngine;
using UnityEngine.TestTools;

namespace Portals.Tests
{
    /// <summary>Namespace-level so Unity can attach it as a MonoBehaviour.</summary>
    public sealed class MomentumReceiverStub : MonoBehaviour, IPortalMomentumReceiver
    {
        public int CallCount;
        public Quaternion LastRotation;

        public void ApplyPortalRotation(Quaternion rotation)
        {
            CallCount++;
            LastRotation = rotation;
        }
    }

    public class PortalTraversalTests
    {
        private readonly List<GameObject> m_Objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in m_Objects)
                if (gameObject != null) Object.DestroyImmediate(gameObject);
            m_Objects.Clear();
        }

        private static void AssertVectorNear(Vector3 expected, Vector3 actual, float epsilon = 1e-3f)
        {
            Assert.That((expected - actual).sqrMagnitude, Is.LessThan(epsilon * epsilon),
                $"Expected {expected} but got {actual}.");
        }

        private GameObject Track(GameObject gameObject)
        {
            m_Objects.Add(gameObject);
            return gameObject;
        }

        private LocaversalPortal CreateLocaversal(string name, Vector3 position, Quaternion rotation)
        {
            GameObject gameObject = Track(new GameObject(name));
            gameObject.transform.SetPositionAndRotation(position, rotation);
            return gameObject.AddComponent<LocaversalPortal>();
        }

        private InterversalPortal CreateInterversal(string name, Vector3 position, Quaternion rotation)
        {
            GameObject gameObject = Track(new GameObject(name));
            gameObject.transform.SetPositionAndRotation(position, rotation);
            return gameObject.AddComponent<InterversalPortal>();
        }

        private PortalTraverser CreateTraveller(Vector3 position, params PortalEndpoint[] portals)
        {
            GameObject gameObject = Track(new GameObject("Traveller"));
            gameObject.transform.position = position;

            Rigidbody body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = false;
            body.useGravity = false;

            PortalTraverser traverser = gameObject.AddComponent<PortalTraverser>();
            traverser.AutoDetect = false;
            traverser.SetPortals(portals);
            return traverser;
        }

        [UnityTest]
        public IEnumerator EnteringAndExiting_TransportsPoseAndVelocity()
        {
            LocaversalPortal entrance = CreateLocaversal("Entrance", Vector3.zero, Quaternion.identity);
            LocaversalPortal exit = CreateLocaversal("Exit", new Vector3(10f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));
            entrance.SetPair(exit);

            PortalTraverser traverser = CreateTraveller(new Vector3(0f, 0f, 1f), entrance, exit);
            Rigidbody body = traverser.GetComponent<Rigidbody>();
            body.linearVelocity = new Vector3(0f, 0f, -10f);

            Assert.That(traverser.TickDetection(), Is.False); // seed previous position

            body.position = new Vector3(0f, 0f, -0.5f);
            Assert.That(traverser.TickDetection(), Is.True);

            Quaternion frameRotation = PortalMath.ComputeFrameRotation(entrance.Rotation, exit.Rotation);
            // Traversal anchors at the entrance plane and clears the exit surface.
            Vector3 expectedPosition = exit.Position
                + exit.Forward * (exit.ExitOffset + 0.02f);

            AssertVectorNear(expectedPosition, body.position);
            AssertVectorNear(frameRotation * new Vector3(0f, 0f, -10f), body.linearVelocity);
            AssertVectorNear(frameRotation * Quaternion.identity * Vector3.forward,
                body.rotation * Vector3.forward);

            yield break;
        }

        [UnityTest]
        public IEnumerator HighSpeedTraversal_SweptSegmentDetectsCrossing()
        {
            LocaversalPortal entrance = CreateLocaversal("Entrance", Vector3.zero, Quaternion.identity);
            LocaversalPortal exit = CreateLocaversal("Exit", new Vector3(5f, 0f, 0f), Quaternion.identity);
            entrance.SetPair(exit);

            PortalTraverser traverser = CreateTraveller(new Vector3(0f, 0f, 8f), entrance, exit);
            Assert.That(traverser.TickDetection(), Is.False);

            // Teleports straight past the plane in one step - discrete overlap would miss it.
            traverser.GetComponent<Rigidbody>().position = new Vector3(0f, 0f, -12f);
            Assert.That(traverser.TickDetection(), Is.True);

            yield break;
        }

        [UnityTest]
        public IEnumerator ExitPortal_IgnoredUntilEntityMovesClear()
        {
            LocaversalPortal entrance = CreateLocaversal("Entrance", Vector3.zero, Quaternion.identity);
            LocaversalPortal exit = CreateLocaversal("Exit", new Vector3(10f, 0f, 0f), Quaternion.identity);
            entrance.SetPair(exit);

            PortalTraverser traverser = CreateTraveller(new Vector3(0f, 0f, 1f), entrance, exit);
            Rigidbody body = traverser.GetComponent<Rigidbody>();

            traverser.TickDetection();
            body.position = new Vector3(0f, 0f, -0.5f);
            Assert.That(traverser.TickDetection(), Is.True);

            yield return null; // advance a frame so HasTraversedThisFrame resets

            // Moving back through the exit immediately must NOT bounce the entity.
            body.position = new Vector3(10f, 0f, 0.05f);
            traverser.TickDetection();
            body.position = new Vector3(10f, 0f, -0.05f);

            Assert.That(traverser.TickDetection(), Is.False);
            Assert.That(traverser.IgnoredPortal, Is.SameAs(exit));
        }

        [UnityTest]
        public IEnumerator RigidbodyTraversal_PreservesAngularVelocity()
        {
            LocaversalPortal entrance = CreateLocaversal("Entrance", Vector3.zero, Quaternion.Euler(0f, 45f, 0f));
            LocaversalPortal exit = CreateLocaversal("Exit", new Vector3(0f, 3f, 0f), Quaternion.Euler(0f, -90f, 0f));
            entrance.SetPair(exit);

            PortalTraverser traverser = CreateTraveller(new Vector3(0f, 0f, 1f), entrance, exit);
            Rigidbody body = traverser.GetComponent<Rigidbody>();
            Vector3 angular = new Vector3(1f, 2f, 3f);
            body.angularVelocity = angular;

            traverser.TickDetection();
            body.position = new Vector3(0f, 0f, -0.4f);
            Assert.That(traverser.TickDetection(), Is.True);

            Quaternion frameRotation = PortalMath.ComputeFrameRotation(entrance.Rotation, exit.Rotation);
            AssertVectorNear(frameRotation * angular, body.angularVelocity);
            Assert.That(body.angularVelocity.magnitude, Is.EqualTo(angular.magnitude).Within(1e-3f));

            yield break;
        }

        [UnityTest]
        public IEnumerator MovingExitPortal_AddsPortalMotionToTraversalVelocity()
        {
            LocaversalPortal entrance = CreateLocaversal("Entrance", Vector3.zero, Quaternion.identity);
            LocaversalPortal exit = CreateLocaversal("Exit", new Vector3(10f, 0f, 0f), Quaternion.identity);
            entrance.SetPair(exit);

            // Sample both frames at explicit times; move the exit 5 m in 0.1 s.
            // Use a time base ahead of Time.time so the samples are always accepted,
            // regardless of how long earlier tests have been running.
            float now = Time.time + 1f;
            entrance.UpdateMotion(now);
            entrance.UpdateMotion(now + 0.1f);
            exit.UpdateMotion(now);
            exit.transform.position = new Vector3(10f, 0f, 5f);
            exit.UpdateMotion(now + 0.1f);

            PortalTraverser traverser = CreateTraveller(new Vector3(0f, 0f, 1f), entrance, exit);
            Rigidbody body = traverser.GetComponent<Rigidbody>();

            traverser.TickDetection();
            body.position = new Vector3(0f, 0f, -0.5f);
            Assert.That(traverser.TickDetection(), Is.True);

            // Static entrance, exit moving +Z at 50 m/s: v' = Ry(180) * 0 + (0,0,50).
            AssertVectorNear(new Vector3(0f, 0f, 50f), body.linearVelocity, 1e-2f);

            yield break;
        }

        [UnityTest]
        public IEnumerator MomentumReceiver_IsNotifiedWithFrameRotation()
        {
            LocaversalPortal entrance = CreateLocaversal("Entrance", Vector3.zero, Quaternion.identity);
            LocaversalPortal exit = CreateLocaversal("Exit", new Vector3(7f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));
            entrance.SetPair(exit);

            GameObject gameObject = Track(new GameObject("TravellerWithReceiver"));
            gameObject.transform.position = new Vector3(0f, 0f, 1f);
            Rigidbody body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = false;
            body.useGravity = false;

            MomentumReceiverStub receiver = gameObject.AddComponent<MomentumReceiverStub>();
            PortalTraverser traverser = gameObject.AddComponent<PortalTraverser>();
            traverser.AutoDetect = false;
            traverser.SetPortals(entrance, exit);

            traverser.TickDetection();
            body.position = new Vector3(0f, 0f, -0.5f);
            Assert.That(traverser.TickDetection(), Is.True);

            Quaternion expected = PortalMath.ComputeFrameRotation(entrance.Rotation, exit.Rotation);
            Assert.That(receiver.CallCount, Is.EqualTo(1));
            Assert.That(receiver.LastRotation, Is.EqualTo(expected));

            yield break;
        }

        [UnityTest]
        public IEnumerator InterversalTraversal_SwitchesEntityUniverse()
        {
            GameObject systemObject = Track(new GameObject("InterversalSystem"));
            InterversalPortalSystem system = systemObject.AddComponent<InterversalPortalSystem>();
            system.Initialize();

            InterversalPortal entrance = CreateInterversal("AlphaPortal", Vector3.zero, Quaternion.identity);
            InterversalPortal exit = CreateInterversal("BetaPortal", new Vector3(20f, 0f, 0f), Quaternion.identity);
            entrance.Configure("Alpha", exit, InterversalMappingMode.ViaPortalFrame, Vector3.zero);
            exit.Configure("Beta", entrance, InterversalMappingMode.ViaPortalFrame, Vector3.zero);

            GameObject travellerObject = Track(new GameObject("Traveller"));
            travellerObject.transform.position = new Vector3(0f, 0f, 1f);
            Rigidbody body = travellerObject.AddComponent<Rigidbody>();
            body.isKinematic = false;
            body.useGravity = false;
            travellerObject.AddComponent<UniverseBody>();
            PortalTraverser traverser = travellerObject.AddComponent<PortalTraverser>();
            traverser.AutoDetect = false;
            traverser.SetPortals(entrance, exit);

            traverser.TickDetection();
            body.position = new Vector3(0f, 0f, -0.5f);
            Assert.That(traverser.TickDetection(), Is.True);

            Assert.That(system.GetEntityUniverse(travellerObject), Is.EqualTo(new UniverseId("Beta")));
            Assert.That(travellerObject.GetComponent<UniverseBody>().Universe, Is.EqualTo(new UniverseId("Beta")));

            yield break;
        }
    }
}
