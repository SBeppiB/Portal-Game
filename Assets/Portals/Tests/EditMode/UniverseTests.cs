using NUnit.Framework;
using Portals.Core;
using UnityEngine;

namespace Portals.Tests
{
    public class UniverseTests
    {
        private GameObject m_Owner;

        [SetUp]
        public void SetUp()
        {
            m_Owner = new GameObject("UniverseTestOwner");
        }

        [TearDown]
        public void TearDown()
        {
            if (m_Owner != null) Object.DestroyImmediate(m_Owner);
        }

        [Test]
        public void UniverseIds_CompareByValue()
        {
            UniverseId a = "Alpha";
            UniverseId b = "Alpha";
            UniverseId c = "Beta";

            Assert.That(a, Is.EqualTo(b));
            Assert.That(a != c, Is.True);
            Assert.That(UniverseId.None.IsValid, Is.False);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void Universes_KeepSeparateStatesAndPhysicsProfiles()
        {
            InterversalPortalSystem system = m_Owner.AddComponent<InterversalPortalSystem>();
            system.Initialize();

            UniverseState alpha = system.GetOrCreateUniverse("Alpha");
            UniverseState beta = system.GetOrCreateUniverse("Beta");

            Assert.That(alpha, Is.Not.Null);
            Assert.That(beta, Is.Not.Null);
            Assert.That(alpha, Is.Not.SameAs(beta));

            alpha.Physics.gravity = new Vector3(0f, -20f, 0f);
            Assert.That(beta.Physics.gravity.y, Is.EqualTo(-9.81f).Within(1e-4f));
        }

        [Test]
        public void LocaversalPair_ExposesTheOtherEndpoint()
        {
            GameObject aObject = new GameObject("A");
            GameObject bObject = new GameObject("B");
            try
            {
                LocaversalPortal a = aObject.AddComponent<LocaversalPortal>();
                LocaversalPortal b = bObject.AddComponent<LocaversalPortal>();

                a.SetPair(b);

                Assert.That(a.GetLinkedEndpoint(), Is.SameAs(b));
                Assert.That(b.GetLinkedEndpoint(), Is.SameAs(a));
            }
            finally
            {
                Object.DestroyImmediate(aObject);
                Object.DestroyImmediate(bObject);
            }
        }

        [Test]
        public void InterversalMappingModes_ProduceDifferentExits()
        {
            GameObject entranceObject = new GameObject("Entrance");
            GameObject exitObject = new GameObject("Exit");
            try
            {
                InterversalPortal entrance = entranceObject.AddComponent<InterversalPortal>();
                InterversalPortal exit = exitObject.AddComponent<InterversalPortal>();
                exitObject.transform.SetPositionAndRotation(new Vector3(10f, 0f, 0f), Quaternion.identity);

                Vector3 worldPoint = new Vector3(0f, 0f, -1f);
                Quaternion worldRotation = Quaternion.identity;

                entrance.Configure("UniverseA", exit, InterversalMappingMode.ViaPortalFrame, Vector3.zero);
                entrance.ComputeExitPose(exit, worldPoint, worldRotation,
                    out Vector3 framedExit, out _);

                entrance.Configure("UniverseA", exit, InterversalMappingMode.PreserveWorldOffset,
                    new Vector3(100f, 0f, 0f));
                entrance.ComputeExitPose(exit, worldPoint, worldRotation,
                    out Vector3 offsetExit, out Quaternion offsetRotation);

                Assert.That((framedExit - offsetExit).sqrMagnitude, Is.GreaterThan(1f));
                Assert.That((offsetExit - new Vector3(100f, 0f, -1f)).sqrMagnitude, Is.LessThan(1e-6f));
                Assert.That(offsetRotation, Is.EqualTo(worldRotation));
            }
            finally
            {
                Object.DestroyImmediate(entranceObject);
                Object.DestroyImmediate(exitObject);
            }
        }

        [Test]
        public void EntityCanSwitchUniverse()
        {
            InterversalPortalSystem system = m_Owner.AddComponent<InterversalPortalSystem>();
            system.Initialize();

            GameObject entity = new GameObject("Traveller");
            try
            {
                entity.AddComponent<UniverseBody>();

                system.SwitchEntityUniverse(entity, "Beta");

                Assert.That(system.GetEntityUniverse(entity), Is.EqualTo(new UniverseId("Beta")));
                Assert.That(entity.GetComponent<UniverseBody>().Universe, Is.EqualTo(new UniverseId("Beta")));
                Assert.That(system.GetOrCreateUniverse("Beta").Entities.Contains(entity), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(entity);
            }
        }
    }
}
