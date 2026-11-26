using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Portals.Runtime.Mechanics;

namespace Portals.Tests.EditMode
{
    public class ScanConfigurationTests
    {
        [Test]
        public void ScanConfiguration_Defaults_AreValid()
        {
            var cfg = new ScanConfiguration();
            Assert.IsTrue(cfg.IsValid());
            Assert.AreEqual(10f, cfg.frequency);
            Assert.AreEqual(5f, cfg.cacheDuration);
            Assert.IsTrue(cfg.enableCaching);
            Assert.AreEqual(5f, cfg.scanRange);
            Assert.AreEqual(10, cfg.discoveryLimit);
            Assert.AreEqual(10, cfg.maxDiscoveries);
            Assert.AreEqual(0.1f, cfg.riftInterferenceConfidenceAttenuation);
        }

        [Test]
        public void ScanConfiguration_Frequency_PropertyAlias()
        {
            var cfg = new ScanConfiguration();
            cfg.Frequency = 20f;
            Assert.AreEqual(20f, cfg.frequency);
            Assert.AreEqual(20f, cfg.Frequency);
        }

        [Test]
        public void ScanConfiguration_CacheDuration_PropertyAlias()
        {
            var cfg = new ScanConfiguration();
            cfg.CacheDuration = 10f;
            Assert.AreEqual(10f, cfg.cacheDuration);
            Assert.AreEqual(10f, cfg.CacheDuration);
        }

        [Test]
        public void ScanConfiguration_EnableCaching_PropertyAlias()
        {
            var cfg = new ScanConfiguration();
            cfg.EnableCaching = false;
            Assert.IsFalse(cfg.enableCaching);
            Assert.IsFalse(cfg.EnableCaching);
        }

        [Test]
        public void ScanConfiguration_ScanRange_Aliases()
        {
            var cfg = new ScanConfiguration();
            cfg.ScanRange = 15f;
            Assert.AreEqual(15f, cfg.scanRange);
            Assert.AreEqual(15f, cfg.Range);
            Assert.AreEqual(15f, cfg.DiscoveryRange);
            Assert.AreEqual(15f, cfg.ScanRadius);
            Assert.AreEqual(15f, cfg.Radius);
            Assert.AreEqual(15f, cfg.scanRadius);
            Assert.AreEqual(15f, cfg.discoveryRange);
            Assert.AreEqual(15f, cfg.radius);
        }

        [Test]
        public void ScanConfiguration_DiscoveryLimit_Aliases()
        {
            var cfg = new ScanConfiguration();
            cfg.DiscoveryLimit = 20;
            Assert.AreEqual(20, cfg.discoveryLimit);
            Assert.AreEqual(20, cfg.discoveryLimitCount);
        }

        [Test]
        public void ScanConfiguration_MaxDiscoveries_Aliases()
        {
            var cfg = new ScanConfiguration();
            cfg.MaxDiscoveries = 25;
            Assert.AreEqual(25, cfg.maxDiscoveries);
            Assert.AreEqual(25, cfg.MaxDiscoveryCount);
            Assert.AreEqual(25, cfg.MaxDiscoveriesCount);
        }

        [Test]
        public void ScanConfiguration_RiftInterferenceConfidenceAttenuation_PropertyAlias()
        {
            var cfg = new ScanConfiguration();
            cfg.RiftInterferenceConfidenceAttenuation = 0.25f;
            Assert.AreEqual(0.25f, cfg.riftInterferenceConfidenceAttenuation);
            Assert.AreEqual(0.25f, cfg.riftInterferenceConfidenceAttenuationAlias);
        }

        [Test]
        public void ScanConfiguration_RiftInterferenceConfidenceAttenuation_ClampsToRange()
        {
            var cfg = new ScanConfiguration();
            cfg.RiftInterferenceConfidenceAttenuation = -0.5f;
            Assert.AreEqual(0f, cfg.riftInterferenceConfidenceAttenuation);

            cfg.RiftInterferenceConfidenceAttenuation = 1.5f;
            Assert.AreEqual(1f, cfg.riftInterferenceConfidenceAttenuation);
        }
    }

    public class UniverseRegistryTests
    {
        private UniverseRegistry m_Registry;
        private OmniversalCoordinates m_Coord1;
        private OmniversalCoordinates m_Coord2;
        private UniverseSignature m_Sig1;
        private UniverseSignature m_Sig2;

        [SetUp]
        public void Setup()
        {
            m_Registry = new UniverseSignatureDatabase();
            m_Coord1 = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            m_Coord2 = new OmniversalCoordinates(2, 1, 5, 10, 1, Vector3.one);
            m_Sig1 = UniverseSignature.Generate(m_Coord1);
            m_Sig2 = UniverseSignature.Generate(m_Coord2);
        }

        [Test]
        public void Register_AddsSignature()
        {
            m_Registry.Register(m_Sig1);
            Assert.IsTrue(m_Registry.Contains(m_Coord1));
            Assert.AreEqual(1, m_Registry.Count);
        }

        [Test]
        public void Register_DuplicateIgnored()
        {
            m_Registry.Register(m_Sig1);
            m_Registry.Register(m_Sig1);
            Assert.AreEqual(1, m_Registry.Count);
        }

        [Test]
        public void TryRegister_ReturnsFalseForDuplicate()
        {
            m_Registry.Register(m_Sig1);
            bool result = m_Registry.TryRegister(m_Sig1);
            Assert.IsFalse(result);
            Assert.AreEqual(1, m_Registry.Count);
        }

        [Test]
        public void RegisterOrUpdate_UpdatesExisting()
        {
            m_Registry.Register(m_Sig1);
            var updatedSig = UniverseSignature.Generate(m_Coord1, new UniverseStructuralProfile { DestinationInstability = 0.5 });
            m_Registry.RegisterOrUpdate(updatedSig);
            Assert.AreEqual(1, m_Registry.Count);
            var retrieved = m_Registry.Get(m_Coord1);
            Assert.AreEqual(0.5, retrieved.Profile.DestinationInstability, 1e-9);
        }

        [Test]
        public void Contains_ByCoordinates_ReturnsTrue()
        {
            m_Registry.Register(m_Sig1);
            Assert.IsTrue(m_Registry.Contains(m_Coord1));
            Assert.IsFalse(m_Registry.Contains(m_Coord2));
        }

        [Test]
        public void Contains_BySignature_ReturnsTrue()
        {
            m_Registry.Register(m_Sig1);
            Assert.IsTrue(m_Registry.Contains(m_Sig1));
            Assert.IsFalse(m_Registry.Contains(m_Sig2));
        }

        [Test]
        public void Contains_BySignatureId_ReturnsTrue()
        {
            m_Registry.Register(m_Sig1);
            Assert.IsTrue(m_Registry.Contains(m_Sig1.Signature));
            Assert.IsTrue(m_Registry.Contains(m_Sig1.SignatureId));
            Assert.IsTrue(m_Registry.Contains(m_Sig1.Id));
            Assert.IsTrue(m_Registry.Contains(m_Sig1.UniverseId));
        }

        [Test]
        public void TryGet_ByCoordinates_ReturnsSignature()
        {
            m_Registry.Register(m_Sig1);
            Assert.IsTrue(m_Registry.TryGet(m_Coord1, out var sig));
            Assert.AreEqual(m_Sig1.Signature, sig.Signature);
        }

        [Test]
        public void TryGet_BySignatureId_ReturnsSignature()
        {
            m_Registry.Register(m_Sig1);
            Assert.IsTrue(m_Registry.TryGet(m_Sig1.Signature, out var sig));
            Assert.AreEqual(m_Sig1.Signature, sig.Signature);
        }

        [Test]
        public void Get_ReturnsSignatureOrNull()
        {
            m_Registry.Register(m_Sig1);
            var sig = m_Registry.Get(m_Coord1);
            Assert.IsNotNull(sig);
            Assert.AreEqual(m_Sig1.Signature, sig.Signature);

            var missing = m_Registry.Get(m_Coord2);
            Assert.IsNull(missing);
        }

        [Test]
        public void GetAll_ReturnsSortedList()
        {
            m_Registry.Register(m_Sig2);
            m_Registry.Register(m_Sig1);
            var all = m_Registry.GetAll();
            Assert.AreEqual(2, all.Count);
            Assert.AreEqual(m_Sig1.Signature, all[0].Signature);
            Assert.AreEqual(m_Sig2.Signature, all[1].Signature);
        }

        [Test]
        public void Remove_ByCoordinates_RemovesSignature()
        {
            m_Registry.Register(m_Sig1);
            m_Registry.Register(m_Sig2);
            bool removed = m_Registry.Remove(m_Coord1);
            Assert.IsTrue(removed);
            Assert.AreEqual(1, m_Registry.Count);
            Assert.IsFalse(m_Registry.Contains(m_Coord1));
        }

        [Test]
        public void Clear_RemovesAll()
        {
            m_Registry.Register(m_Sig1);
            m_Registry.Register(m_Sig2);
            m_Registry.Clear();
            Assert.AreEqual(0, m_Registry.Count);
            Assert.IsFalse(m_Registry.Contains(m_Coord1));
            Assert.IsFalse(m_Registry.Contains(m_Coord2));
        }

        [Test]
        public void DiscoveryStatus_Tracking()
        {
            m_Registry.Register(m_Sig1);
            Assert.AreEqual(DiscoveryStatus.Registered, m_Registry.GetDiscoveryStatus(m_Coord1));

            m_Registry.SetDiscoveryStatus(m_Coord1, DiscoveryStatus.Discovered);
            Assert.AreEqual(DiscoveryStatus.Discovered, m_Registry.GetDiscoveryStatus(m_Coord1));

            m_Registry.SetDiscoveryStatus(m_Coord1, DiscoveryStatus.Failed);
            Assert.AreEqual(DiscoveryStatus.Failed, m_Registry.GetDiscoveryStatus(m_Coord1));
        }

        [Test]
        public void MarkDiscovered_RegistersAndSetsStatus()
        {
            m_Registry.MarkDiscovered(m_Sig1);
            Assert.IsTrue(m_Registry.Contains(m_Coord1));
            Assert.AreEqual(DiscoveryStatus.Discovered, m_Registry.GetDiscoveryStatus(m_Coord1));
        }

        [Test]
        public void GetDiscovered_ReturnsDiscoveredOnly()
        {
            m_Registry.Register(m_Sig1);
            m_Registry.Register(m_Sig2);
            m_Registry.SetDiscoveryStatus(m_Coord1, DiscoveryStatus.Discovered);
            m_Registry.SetDiscoveryStatus(m_Coord2, DiscoveryStatus.Registered);

            var discovered = m_Registry.GetDiscovered();
            // IsDiscovered includes Discovered, Registered, and Cached
            Assert.AreEqual(2, discovered.Count);
            Assert.IsTrue(discovered.Any(s => s.Coordinates.Equals(m_Coord1)));
            Assert.IsTrue(discovered.Any(s => s.Coordinates.Equals(m_Coord2)));
        }

        [Test]
        public void GetByDiscoveryStatus_FiltersCorrectly()
        {
            m_Registry.Register(m_Sig1);
            m_Registry.Register(m_Sig2);
            m_Registry.SetDiscoveryStatus(m_Coord1, DiscoveryStatus.Discovered);
            m_Registry.SetDiscoveryStatus(m_Coord2, DiscoveryStatus.Pending);

            var discovered = m_Registry.GetByDiscoveryStatus(DiscoveryStatus.Discovered);
            var pending = m_Registry.GetByDiscoveryStatus(DiscoveryStatus.Pending);

            Assert.AreEqual(1, discovered.Count);
            Assert.AreEqual(1, pending.Count);
        }

        [Test]
        public void IsDiscovered_IncludesDiscoveredRegisteredCached()
        {
            m_Registry.Register(m_Sig1);
            m_Registry.Register(m_Sig2);
            m_Registry.SetDiscoveryStatus(m_Coord1, DiscoveryStatus.Discovered);
            m_Registry.SetDiscoveryStatus(m_Coord2, DiscoveryStatus.Registered);

            Assert.IsTrue(m_Registry.IsDiscovered(m_Coord1));
            Assert.IsTrue(m_Registry.IsDiscovered(m_Coord2));

            m_Registry.SetDiscoveryStatus(m_Coord2, DiscoveryStatus.Cached);
            Assert.IsTrue(m_Registry.IsDiscovered(m_Coord2));

            m_Registry.SetDiscoveryStatus(m_Coord2, DiscoveryStatus.Pending);
            Assert.IsFalse(m_Registry.IsDiscovered(m_Coord2));
        }
    }

    public class UniverseSignatureTests
    {
        [Test]
        public void Generate_CreatesSignatureWithValidDefaults()
        {
            var coord = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var sig = UniverseSignature.Generate(coord);

            Assert.IsNotNull(sig);
            Assert.AreEqual(coord, sig.Coordinates);
            Assert.IsTrue(sig.IsValid);
            Assert.IsNotNull(sig.Profile);
            Assert.IsFalse(string.IsNullOrEmpty(sig.Signature));
        }

        [Test]
        public void Generate_WithUniverseId_UsesProvidedId()
        {
            var coord = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var sig = UniverseSignature.Generate("custom-id", coord);

            Assert.AreEqual("custom-id", sig.UniverseId);
            Assert.AreEqual(coord, sig.Coordinates);
        }

        [Test]
        public void Generate_WithProfile_UsesProvidedProfile()
        {
            var coord = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var profile = new UniverseStructuralProfile { DestinationInstability = 0.75, RiftInterference = 0.25 };
            var sig = UniverseSignature.Generate(coord, profile);

            Assert.AreSame(profile, sig.Profile);
        }

        [Test]
        public void Generate_WithIsValid_SetsValidity()
        {
            var coord = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var sig = UniverseSignature.Generate(coord, null, false);

            Assert.IsFalse(sig.IsValid);
        }

        [Test]
        public void Signature_DeterministicForSameCoordinates()
        {
            var coord = new OmniversalCoordinates(10, 5, 100, 45.5, 1.5, new Vector3(1, 2, 3));
            var sig1 = UniverseSignature.Generate(coord);
            var sig2 = UniverseSignature.Generate(coord);

            Assert.AreEqual(sig1.Signature, sig2.Signature);
            Assert.AreEqual(sig1.SignatureId, sig2.SignatureId);
            Assert.AreEqual(sig1.Id, sig2.Id);
        }

        [Test]
        public void Signature_DifferentForDifferentCoordinates()
        {
            var coord1 = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var coord2 = new OmniversalCoordinates(2, 0, 0, 0, 0, Vector3.zero);
            var sig1 = UniverseSignature.Generate(coord1);
            var sig2 = UniverseSignature.Generate(coord2);

            Assert.AreNotEqual(sig1.Signature, sig2.Signature);
        }

        [Test]
        public void Equals_SameSignature_ReturnsTrue()
        {
            var coord = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var sig1 = UniverseSignature.Generate(coord);
            var sig2 = UniverseSignature.Generate(coord);

            Assert.IsTrue(sig1.Equals(sig2));
            Assert.IsTrue(sig1 == sig2);
            Assert.AreEqual(sig1.GetHashCode(), sig2.GetHashCode());
        }

        [Test]
        public void Equals_DifferentSignature_ReturnsFalse()
        {
            var sig1 = UniverseSignature.Generate(new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero));
            var sig2 = UniverseSignature.Generate(new OmniversalCoordinates(2, 0, 0, 0, 0, Vector3.zero));

            Assert.IsFalse(sig1.Equals(sig2));
            Assert.IsTrue(sig1 != sig2);
        }

        [Test]
        public void Create_Aliases_Generate()
        {
            var coord = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var sig1 = UniverseSignature.Create(coord);
            var sig2 = UniverseSignature.Generate(coord);

            Assert.AreEqual(sig1.Signature, sig2.Signature);
        }

        [Test]
        public void FromCoordinates_Aliases_Generate()
        {
            var coord = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var sig1 = UniverseSignature.FromCoordinates(coord);
            var sig2 = UniverseSignature.Generate(coord);

            Assert.AreEqual(sig1.Signature, sig2.Signature);
        }

        [Test]
        public void Signature_IncludesPrimeAuMarker()
        {
            var coord = new OmniversalCoordinates(1, 0, 5, 0, 0, Vector3.zero);
            coord = new OmniversalCoordinates(coord.Layer, coord.Ring, coord.AuPosition, coord.Theta, coord.Rho, coord.X, coord.Y, coord.Z, true);
            var sig = UniverseSignature.Generate(coord);

            Assert.IsTrue(sig.Signature.Contains(":P"));
        }
    }
}