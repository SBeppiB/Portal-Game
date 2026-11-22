using NUnit.Framework;
using Portals.Core;
using UnityEngine;

namespace Portals.Tests
{
    public class DimensionSelectionTests
    {
        private GameObject m_Owner;

        [SetUp]
        public void SetUp()
        {
            m_Owner = new GameObject("DimensionSelectionOwner");
        }

        [TearDown]
        public void TearDown()
        {
            if (m_Owner != null) Object.DestroyImmediate(m_Owner);
        }

        [Test]
        public void DefaultDimensions_AreNineWithFirstSelected()
        {
            InterversalPortalSystem system = m_Owner.AddComponent<InterversalPortalSystem>();
            system.Initialize();

            Assert.That(system.Dimensions.Count, Is.EqualTo(9));
            Assert.That(system.SelectedDimensionIndex, Is.EqualTo(0));
            Assert.That(system.SelectedUniverse, Is.EqualTo(new UniverseId("Universe 1")));
            Assert.That(system.GetSelectedDimensionName(), Is.EqualTo("Dimension 1"));
        }

        [Test]
        public void SelectDimension_UpdatesUniverseAndRaisesEvent()
        {
            InterversalPortalSystem system = m_Owner.AddComponent<InterversalPortalSystem>();
            system.Initialize();

            UniverseId notified = UniverseId.None;
            int notifications = 0;
            system.SelectedUniverseChanged += id =>
            {
                notified = id;
                notifications++;
            };

            Assert.That(system.SelectDimension(2), Is.True);
            Assert.That(system.SelectedDimensionIndex, Is.EqualTo(2));
            Assert.That(system.SelectedUniverse, Is.EqualTo(new UniverseId("Universe 3")));
            Assert.That(system.GetSelectedDimensionName(), Is.EqualTo("Dimension 3"));
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(notified, Is.EqualTo(system.SelectedUniverse));
            Assert.That(system.TryGetUniverse(system.SelectedUniverse, out _), Is.True);

            // Selecting the same dimension again must not spam events.
            Assert.That(system.SelectDimension(2), Is.True);
            Assert.That(notifications, Is.EqualTo(1));
        }

        [Test]
        public void SelectDimension_RejectsInvalidIndex()
        {
            InterversalPortalSystem system = m_Owner.AddComponent<InterversalPortalSystem>();
            system.Initialize();
            UniverseId before = system.SelectedUniverse;

            Assert.That(system.SelectDimension(-1), Is.False);
            Assert.That(system.SelectDimension(99), Is.False);
            Assert.That(system.SelectedUniverse, Is.EqualTo(before));
        }

        [Test]
        public void SetSelectedUniverse_RejectsEmptyId()
        {
            InterversalPortalSystem system = m_Owner.AddComponent<InterversalPortalSystem>();
            system.Initialize();

            Assert.That(system.SetSelectedUniverse(UniverseId.None), Is.False);
        }
    }
}
