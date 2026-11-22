using System.Collections;
using NUnit.Framework;
using Portals.Core;
using Portals.Traversal;
using UnityEngine;
using UnityEngine.TestTools;

namespace Portals.Tests
{
    /// <summary>
    /// Test double for a player movement controller. Records the momentum rotation and
    /// the additive assist impulse independently so momentum preservation is verifiable.
    /// </summary>
    public sealed class ExitAssistReceiverStub : MonoBehaviour, IPortalMomentumReceiver, IPortalExitAssistReceiver
    {
        public Vector3 Velocity = new Vector3(3f, -2f, 4f);
        public Vector3 VelocityAfterRotation;
        public int Rotations;
        public int Impulses;
        public Vector3 LastImpulse;

        public void ApplyPortalRotation(Quaternion rotation)
        {
            Rotations++;
            Velocity = rotation * Velocity;
            VelocityAfterRotation = Velocity;
        }

        public void ApplyExitImpulse(Vector3 velocityImpulse)
        {
            Impulses++;
            LastImpulse = velocityImpulse;
            Velocity += velocityImpulse;
        }
    }

    public class PortalExitAssistTests
    {
        private GameObject m_EntranceObject;
        private GameObject m_ExitObject;
        private GameObject m_Player;
        private CharacterController m_Controller;
        private PortalTraverser m_Traverser;
        private ExitAssistReceiverStub m_Receiver;
        private LocaversalPortal m_Entrance;
        private LocaversalPortal m_Exit;

        [SetUp]
        public void SetUp()
        {
            m_EntranceObject = CreatePortal("ExitAssist Entrance", new Vector3(0f, 0f, 0f), Quaternion.identity);
            m_Entrance = m_EntranceObject.GetComponent<LocaversalPortal>();

            m_Player = new GameObject("ExitAssist Player");
            m_Player.transform.position = new Vector3(0f, 0f, 1f);
            m_Controller = m_Player.AddComponent<CharacterController>();
            m_Controller.height = 2f;
            m_Controller.radius = 0.4f;
            m_Controller.center = new Vector3(0f, 1f, 0f);
            m_Receiver = m_Player.AddComponent<ExitAssistReceiverStub>();
            m_Traverser = m_Player.AddComponent<PortalTraverser>();
            m_Traverser.AutoDetect = false;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_Player);
            Object.DestroyImmediate(m_EntranceObject);
            if (m_ExitObject != null) Object.DestroyImmediate(m_ExitObject);
        }

        private static GameObject CreatePortal(string name, Vector3 position, Quaternion rotation)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetPositionAndRotation(position, rotation);
            gameObject.AddComponent<LocaversalPortal>().SetActive(true);
            return gameObject;
        }

        private void ConfigureExit(Quaternion exitRotation)
        {
            m_ExitObject = CreatePortal("ExitAssist Exit", new Vector3(20f, 0f, 0f), exitRotation);
            m_Exit = m_ExitObject.GetComponent<LocaversalPortal>();
            m_Entrance.SetPair(m_Exit);
            m_Traverser.SetPortals(m_Entrance, m_Exit);
        }

        private bool CrossPortal()
        {
            m_Traverser.TickDetection(); // prime previous position in front of the entrance
            SetPlayerPosition(new Vector3(0f, 0f, -0.5f));
            return m_Traverser.TickDetection();
        }

        private void SetPlayerPosition(Vector3 position)
        {
            m_Controller.enabled = false;
            m_Player.transform.position = position;
            m_Controller.enabled = true;
        }

        private static void AssertVectorNear(Vector3 expected, Vector3 actual, float epsilon = 1e-3f)
        {
            Assert.That((expected - actual).sqrMagnitude, Is.LessThan(epsilon * epsilon),
                $"Expected {expected} but got {actual}.");
        }

        [UnityTest]
        public IEnumerator FloorExit_AppliesUpwardAndHorizontalImpulse()
        {
            ConfigureExit(Quaternion.LookRotation(Vector3.up, Vector3.forward));
            m_Traverser.ConfigureFloorExitAssist(true, 6f, 2f, 0f, 0.8f, 0.6f);

            Assert.That(CrossPortal(), Is.True);
            Assert.That(m_Traverser.LastExitAssistApplied, Is.True);
            Assert.That(m_Traverser.LastExitAssistImpulse.y, Is.EqualTo(6f).Within(0.001f));

            Vector3 horizontal = Vector3.ProjectOnPlane(m_Traverser.LastExitAssistImpulse, Vector3.up);
            Assert.That(horizontal.magnitude, Is.EqualTo(2f).Within(0.001f));
            Assert.That(m_Receiver.Impulses, Is.EqualTo(1));
            AssertVectorNear(m_Traverser.LastExitAssistImpulse, m_Receiver.LastImpulse);
            Assert.That(m_Player.transform.position.y, Is.GreaterThan(m_Exit.Position.y),
                "The player must emerge above the floor portal plane.");

            yield break;
        }

        [UnityTest]
        public IEnumerator FloorExit_ImpulseIsDeterministicAndUsesExitAngle()
        {
            ConfigureExit(Quaternion.LookRotation(Vector3.up, Vector3.forward));
            m_Traverser.ConfigureFloorExitAssist(true, 5f, 2f, 90f, 0.8f, 0.6f);

            Assert.That(CrossPortal(), Is.True);

            Vector3 expected = PortalTraverser.ComputeExitAssistImpulse(
                m_Exit.Forward, m_Player.transform.forward, 5f, 2f, 90f);
            AssertVectorNear(expected, m_Traverser.LastExitAssistImpulse);
            // Repeated computation from identical inputs must be identical (no randomness).
            AssertVectorNear(expected, PortalTraverser.ComputeExitAssistImpulse(
                m_Exit.Forward, m_Player.transform.forward, 5f, 2f, 90f));

            Vector3 horizontal = Vector3.ProjectOnPlane(expected, Vector3.up).normalized;
            Vector3 facing = Vector3.ProjectOnPlane(m_Player.transform.forward, Vector3.up).normalized;
            Assert.That(Vector3.Dot(horizontal, facing), Is.EqualTo(0f).Within(0.01f),
                "Exit Angle must rotate the impulse deterministically from the facing direction.");

            yield break;
        }

        [UnityTest]
        public IEnumerator FloorExit_PreservesExistingMomentum()
        {
            ConfigureExit(Quaternion.LookRotation(Vector3.up, Vector3.forward));
            m_Traverser.ConfigureFloorExitAssist(true, 5f, 1.5f, 0f, 0.8f, 0.6f);

            Assert.That(CrossPortal(), Is.True);
            Assert.That(m_Receiver.Rotations, Is.EqualTo(1), "Traversal momentum must still be rotated.");
            AssertVectorNear(m_Receiver.VelocityAfterRotation + m_Traverser.LastExitAssistImpulse,
                m_Receiver.Velocity, 0.01f);
            Assert.That(m_Receiver.Velocity.magnitude, Is.GreaterThan(0f));

            yield break;
        }

        [UnityTest]
        public IEnumerator WallExit_DoesNotApplyAssist()
        {
            ConfigureExit(Quaternion.identity); // wall: outward normal is horizontal
            m_Traverser.ConfigureFloorExitAssist(true, 6f, 2f, 0f, 0.8f, 0.6f);

            Assert.That(CrossPortal(), Is.True);
            Assert.That(m_Traverser.LastExitAssistApplied, Is.False);
            AssertVectorNear(Vector3.zero, m_Traverser.LastExitAssistImpulse);
            Assert.That(m_Receiver.Impulses, Is.EqualTo(0));

            yield break;
        }

        [UnityTest]
        public IEnumerator CeilingExit_DoesNotApplyAssist()
        {
            ConfigureExit(Quaternion.LookRotation(Vector3.down, Vector3.forward));
            m_Traverser.ConfigureFloorExitAssist(true, 6f, 2f, 0f, 0.8f, 0.6f);

            Assert.That(CrossPortal(), Is.True);
            Assert.That(m_Traverser.LastExitAssistApplied, Is.False);
            Assert.That(m_Receiver.Impulses, Is.EqualTo(0));

            yield break;
        }

        [UnityTest]
        public IEnumerator FloorExit_UsesConfiguredGracePeriodAndBlocksReentry()
        {
            ConfigureExit(Quaternion.LookRotation(Vector3.up, Vector3.forward));
            m_Traverser.ConfigureFloorExitAssist(true, 6f, 2f, 0f, 0.8f, 0.6f);

            Assert.That(CrossPortal(), Is.True);
            Assert.That(m_Traverser.IgnoredPortal, Is.SameAs(m_Exit));
            Assert.That(m_Traverser.IgnoreUntil, Is.GreaterThanOrEqualTo(Time.time + 0.6f - 0.01f));

            // Try to fall straight back through the exit portal immediately.
            SetPlayerPosition(m_Exit.Position + Vector3.up * 1f);
            m_Traverser.TickDetection();
            SetPlayerPosition(m_Exit.Position - Vector3.up * 0.5f);

            Assert.That(m_Traverser.TickDetection(), Is.False,
                "The grace system must prevent an immediate re-entry loop.");

            yield break;
        }

        [UnityTest]
        public IEnumerator FloorExitAssist_DoesNotAffectRigidbodyObjects()
        {
            ConfigureExit(Quaternion.LookRotation(Vector3.up, Vector3.forward));

            GameObject box = new GameObject("Assisted Box");
            box.transform.position = new Vector3(0f, 0f, 1f);
            Rigidbody body = box.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.linearVelocity = Vector3.zero;
            BoxCollider collider = box.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.5f, 0.5f, 0.5f);
            ExitAssistReceiverStub boxReceiver = box.AddComponent<ExitAssistReceiverStub>();
            PortalTraverser boxTraverser = box.AddComponent<PortalTraverser>();
            boxTraverser.AutoDetect = false;
            boxTraverser.SetPortals(m_Entrance, m_Exit);
            boxTraverser.ConfigureFloorExitAssist(true, 6f, 2f, 0f, 0.8f, 0.6f);

            try
            {
                boxTraverser.TickDetection();
                body.position = new Vector3(0f, 0f, -0.5f);

                Assert.That(boxTraverser.TickDetection(), Is.True);
                Assert.That(boxTraverser.LastExitAssistApplied, Is.False,
                    "Exit assist is player-specific and must never touch Rigidbodies.");
                Assert.That(boxReceiver.Impulses, Is.EqualTo(0));
                Assert.That(body.linearVelocity.sqrMagnitude, Is.LessThan(10000f));
            }
            finally
            {
                Object.DestroyImmediate(box);
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator IsFloorNormal_UsesConfigurableThreshold()
        {
            Assert.That(PortalTraverser.IsFloorNormal(Vector3.up, 0.8f), Is.True);
            Assert.That(PortalTraverser.IsFloorNormal(Vector3.forward, 0.8f), Is.False);
            Assert.That(PortalTraverser.IsFloorNormal(Vector3.down, 0.8f), Is.False);

            Vector3 steep = Quaternion.Euler(37f, 0f, 0f) * Vector3.up;
            Vector3 shallow = Quaternion.Euler(30f, 0f, 0f) * Vector3.up;
            Assert.That(PortalTraverser.IsFloorNormal(steep, 0.8f), Is.False);
            Assert.That(PortalTraverser.IsFloorNormal(shallow, 0.8f), Is.True);

            yield break;
        }
    }
}
