using NUnit.Framework;
using Portals.Core;
using Portals.Energy;
using UnityEngine;

namespace Portals.Tests
{
    public class EnergyTests
    {
        private GameObject m_Owner;

        [SetUp]
        public void SetUp()
        {
            m_Owner = new GameObject("EnergyTestOwner");
        }

        [TearDown]
        public void TearDown()
        {
            if (m_Owner != null) Object.DestroyImmediate(m_Owner);
        }

        [Test]
        public void TrySpend_ReducesEnergyAndRejectsUnaffordableCosts()
        {
            EnergyStorage storage = m_Owner.AddComponent<EnergyStorage>();
            storage.Configure(100f, 100f, 0f, 1f);

            Assert.That(storage.TrySpend(20f), Is.True);
            Assert.That(storage.Current, Is.EqualTo(80f).Within(1e-5f));
            Assert.That(storage.TrySpend(200f), Is.False);
            Assert.That(storage.Current, Is.EqualTo(80f).Within(1e-5f));
        }

        [Test]
        public void Regeneration_RespectsDelayThenUsesConfiguredRate()
        {
            EnergyStorage storage = m_Owner.AddComponent<EnergyStorage>();
            storage.Configure(100f, 100f, 10f, 1f);

            Assert.That(storage.TrySpend(10f), Is.True); // 90 left, timer reset
            storage.Tick(0.5f);
            Assert.That(storage.Current, Is.EqualTo(90f).Within(1e-5f)); // still in delay

            storage.Tick(0.5f);
            Assert.That(storage.Current, Is.EqualTo(95f).Within(1e-4f)); // one half-second of regen
        }

        [Test]
        public void DepletedAndRestoredEvents_FireOnTransitionsOnly()
        {
            EnergyStorage storage = m_Owner.AddComponent<EnergyStorage>();
            storage.Configure(100f, 10f, 0f, 1f);

            int depleted = 0;
            int restored = 0;
            storage.Depleted += () => depleted++;
            storage.Restored += () => restored++;

            storage.DrainAll();
            storage.DrainAll();
            Assert.That(depleted, Is.EqualTo(1));

            storage.Recharge(5f);
            storage.Recharge(5f);
            Assert.That(restored, Is.EqualTo(1));

            storage.DrainAll();
            Assert.That(depleted, Is.EqualTo(2));
        }

        [Test]
        public void SetCapacity_PreservesEnergyRatio()
        {
            EnergyStorage storage = m_Owner.AddComponent<EnergyStorage>();
            storage.Configure(100f, 50f, 0f, 1f);

            storage.SetCapacity(200f, preserveRatio: true);

            Assert.That(storage.Capacity, Is.EqualTo(200f));
            Assert.That(storage.Current, Is.EqualTo(100f).Within(1e-5f));
        }

        [Test]
        public void PortalEnergySystem_AuthorizesThenSpendsCreationCost()
        {
            PortalEnergySystem system = m_Owner.AddComponent<PortalEnergySystem>();
            system.Initialize();
            system.Storage.Configure(100f, 10f, 0f, 1f);
            system.Costs.createLocaversal = 20f;
            system.Costs.createInterversal = 40f;

            Assert.That(system.TryAuthorizeCreation(PortalKind.Locaversal, out string reason), Is.False);
            Assert.That(reason, Does.Contain("Not enough energy"));

            system.Storage.Recharge(90f);
            Assert.That(system.CanAfford(PortalKind.Locaversal), Is.True);

            GameObject portalObject = new GameObject("Portal");
            try
            {
                LocaversalPortal portal = portalObject.AddComponent<LocaversalPortal>();
                Assert.That(system.NotifyPortalCreated(portal), Is.True);
                Assert.That(system.Storage.Current, Is.EqualTo(80f).Within(1e-5f));
                Assert.That(system.IsMaintaining(portal), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(portalObject);
            }
        }

        [Test]
        public void Maintenance_DrainsOnlyWhilePortalsAreRegistered()
        {
            PortalEnergySystem system = m_Owner.AddComponent<PortalEnergySystem>();
            system.Initialize();
            system.Storage.Configure(100f, 100f, 0f, 1f);
            system.Costs.createLocaversal = 0f;
            system.Costs.maintainPerPortalPerSecond = 2f;

            GameObject portalA = new GameObject("PortalA");
            GameObject portalB = new GameObject("PortalB");
            try
            {
                LocaversalPortal a = portalA.AddComponent<LocaversalPortal>();
                LocaversalPortal b = portalB.AddComponent<LocaversalPortal>();
                system.NotifyPortalCreated(a);
                system.NotifyPortalCreated(b);

                system.Tick(1f);
                Assert.That(system.Storage.Current, Is.EqualTo(96f).Within(1e-4f)); // 2 portals * 2/s

                system.NotifyPortalDestroyed(b);
                system.Tick(1f);
                Assert.That(system.Storage.Current, Is.EqualTo(94f).Within(1e-4f)); // 1 portal * 2/s
            }
            finally
            {
                Object.DestroyImmediate(portalA);
                Object.DestroyImmediate(portalB);
            }
        }
    }
}
