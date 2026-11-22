using NUnit.Framework;
using Portals.Core;
using UnityEngine;

namespace Portals.Tests
{
    public class PortalPlacementTests
    {
        private GameObject m_Owner;
        private GameObject m_Surface;

        [SetUp]
        public void SetUp()
        {
            m_Owner = new GameObject("PlacementTestOwner");

            m_Surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            m_Surface.name = "TestSurface";
            m_Surface.transform.position = new Vector3(0f, 0f, 0f);
            m_Surface.transform.localScale = new Vector3(10f, 0.2f, 10f);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            if (m_Owner != null) Object.DestroyImmediate(m_Owner);
            if (m_Surface != null) Object.DestroyImmediate(m_Surface);
        }

        [Test]
        public void FirstPortal_CanBePlacedWhileItsPartnerIsInactive()
        {
            GameObject aObject = new GameObject("PortalA");
            GameObject bObject = new GameObject("PortalB");
            try
            {
                LocaversalPortal a = aObject.AddComponent<LocaversalPortal>();
                LocaversalPortal b = bObject.AddComponent<LocaversalPortal>();
                a.SetPair(b);
                a.SetActive(false);
                b.SetActive(false);

                PortalPlacer placer = m_Owner.AddComponent<PortalPlacer>();

                Ray downward = new Ray(new Vector3(0f, 5f, 0f), Vector3.down);
                Assert.That(placer.TryPlace(a, downward, out PortalPlacementFailure failure), Is.True,
                    $"Placement failed with {failure}.");
                Assert.That(a.IsActive, Is.True);
                Assert.That(b.IsActive, Is.False);

                // Traversal must still be disabled until the partner is active.
                Assert.That(a.GetLinkedEndpoint(), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(aObject);
                Object.DestroyImmediate(bObject);
            }
        }

        [Test]
        public void Placement_IsRejectedWhenNoPartnerIsAssigned()
        {
            GameObject aObject = new GameObject("PortalA");
            try
            {
                LocaversalPortal a = aObject.AddComponent<LocaversalPortal>();
                PortalPlacer placer = m_Owner.AddComponent<PortalPlacer>();

                Ray downward = new Ray(new Vector3(0f, 5f, 0f), Vector3.down);
                Assert.That(placer.TryPlace(a, downward, out PortalPlacementFailure failure), Is.False);
                Assert.That(failure, Is.EqualTo(PortalPlacementFailure.MissingPair));
            }
            finally
            {
                Object.DestroyImmediate(aObject);
            }
        }
    }
}
