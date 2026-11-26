using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Portals.Runtime.Mechanics;

namespace Portals.Tests.EditMode
{
    public class OmniversalScannerTests
    {
        private OPMConfiguration m_Cfg;
        private ScanConfiguration m_ScanCfg;
        private UniverseRegistry m_Registry;
        private OmniversalScanner m_Scanner;

        [SetUp]
        public void Setup()
        {
            m_Cfg = OPMConfiguration.CreateDefault();
            m_ScanCfg = new ScanConfiguration
            {
                frequency = 10f,
                cacheDuration = 5f,
                enableCaching = true,
                discoveryLimit = 5,
                maxDiscoveries = 5,
                riftInterferenceConfidenceAttenuation = 0.1f
            };
            m_Registry = new UniverseSignatureDatabase();
            m_Scanner = new OmniversalScanner(m_Registry, m_Cfg, m_ScanCfg);
        }

        [TearDown]
        public void Teardown()
        {
            m_Scanner = null;
            m_Registry = null;
            m_ScanCfg = null;
            m_Cfg = null;
        }

        private OmniversalCoordinates ValidCoord(int layer = 1, int ring = 0, int au = 0, double theta = 0, double rho = 0, Vector3 spatial = default)
        {
            if (spatial == default) spatial = Vector3.zero;
            return new OmniversalCoordinates(layer, ring, au, theta, rho, spatial);
        }

        [Test]
        public void Scanner_InitialState_Idle()
        {
            Assert.AreEqual(ScanState.Idle, m_Scanner.State);
        }

        [Test]
        public void Scan_ValidDestination_ReturnsDiscovered()
        {
            var coord = ValidCoord();
            var sig = UniverseSignature.Generate(coord);
            m_Registry.Register(sig);

            var result = m_Scanner.ScanUniverse(coord);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(DiscoveryStatus.Discovered, result.DiscoveryStatus);
            Assert.AreEqual(coord, result.Destination);
            Assert.IsNotNull(result.Signature);
        }

        [Test]
        public void Scan_InvalidCoordinates_ReturnsFailed()
        {
            var coord = new OmniversalCoordinates(0, 0, 0, 0, 0, Vector3.zero); // layer 0 invalid
            var result = m_Scanner.ScanUniverse(coord);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(DiscoveryStatus.Failed, result.DiscoveryStatus);
            Assert.AreEqual("Invalid coordinates.", result.FailureReason);
        }

        [Test]
        public void Scan_UnknownDestination_ReturnsFailed()
        {
            var coord = ValidCoord(10, 5, 100);
            var result = m_Scanner.ScanUniverse(coord);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(DiscoveryStatus.Failed, result.DiscoveryStatus);
            Assert.AreEqual("Destination unavailable.", result.FailureReason);
        }

        [Test]
        public void Scan_CacheEnabled_ReturnsCachedResult()
        {
            var coord = ValidCoord();
            var sig = UniverseSignature.Generate(coord);
            m_Registry.Register(sig);

            var result1 = m_Scanner.ScanUniverse(coord);
            Assert.IsTrue(result1.Success);

            var result2 = m_Scanner.ScanUniverse(coord);
            Assert.IsTrue(result2.Success);
            Assert.AreEqual(result1.SequenceNumber, result2.SequenceNumber);
            Assert.AreEqual(result1.Timestamp, result2.Timestamp);
        }

        [Test]
        public void Scan_CacheDisabled_NoCaching()
        {
            m_ScanCfg.enableCaching = false;
            var coord = ValidCoord();
            var sig = UniverseSignature.Generate(coord);
            m_Registry.Register(sig);

            var result1 = m_Scanner.ScanUniverse(coord);
            var result2 = m_Scanner.ScanUniverse(coord);

            Assert.AreNotEqual(result1.SequenceNumber, result2.SequenceNumber);
            Assert.AreNotEqual(result1.Timestamp, result2.Timestamp);
        }

        [Test]
        public void ClearCache_InvalidatesCachedResults()
        {
            var coord = ValidCoord();
            var sig = UniverseSignature.Generate(coord);
            m_Registry.Register(sig);

            var result1 = m_Scanner.ScanUniverse(coord);
            m_Scanner.ClearCache();
            var result2 = m_Scanner.ScanUniverse(coord);

            Assert.AreNotEqual(result1.SequenceNumber, result2.SequenceNumber);
            Assert.AreNotEqual(result1.Timestamp, result2.Timestamp);
        }

        [Test]
        public void GenerateSignature_CreatesSignatureWithoutRegistration()
        {
            var coord = ValidCoord(20, 3, 50);
            var sig = m_Scanner.GenerateSignature(coord);

            Assert.IsNotNull(sig);
            Assert.AreEqual(coord, sig.Coordinates);
            Assert.IsFalse(m_Registry.Contains(coord));
        }

        [Test]
        public void CreateSignature_CreatesSignatureWithoutRegistration()
        {
            var coord = ValidCoord(20, 3, 50);
            var sig = m_Scanner.CreateSignature(coord);

            Assert.IsNotNull(sig);
            Assert.AreEqual(coord, sig.Coordinates);
            Assert.IsFalse(m_Registry.Contains(coord));
        }

        [Test]
        public void RegisterSignature_RegistersGeneratedSignature()
        {
            var coord = ValidCoord(20, 3, 50);
            var sig = m_Scanner.GenerateSignature(coord);
            bool registered = m_Scanner.RegisterSignature(sig);

            Assert.IsTrue(registered);
            Assert.IsTrue(m_Registry.Contains(coord));
        }

        [Test]
        public void IdentifyDestination_CreatesAndRegistersSignature()
        {
            var coord = ValidCoord(20, 3, 50);
            bool identified = m_Scanner.IdentifyDestination(coord);

            Assert.IsTrue(identified);
            Assert.IsTrue(m_Registry.Contains(coord));
            Assert.AreEqual(DiscoveryStatus.Pending, m_Registry.GetDiscoveryStatus(coord));
        }

        [Test]
        public void IdentifyDestination_ExistingSignature_DoesNotDuplicate()
        {
            var coord = ValidCoord(20, 3, 50);
            var sig = UniverseSignature.Generate(coord);
            m_Registry.Register(sig);
            m_Registry.SetDiscoveryStatus(coord, DiscoveryStatus.Registered);

            bool identified = m_Scanner.IdentifyDestination(coord);

            Assert.IsTrue(identified);
            Assert.AreEqual(DiscoveryStatus.Registered, m_Registry.GetDiscoveryStatus(coord));
        }

        [Test]
        public void Scan_RespectsMaxDiscoveriesLimit()
        {
            m_ScanCfg.maxDiscoveries = 2;
            m_Scanner = new OmniversalScanner(m_Registry, m_Cfg, m_ScanCfg);

            for (int i = 0; i < 3; i++)
            {
                var coord = ValidCoord(1, 0, i);
                var sig = UniverseSignature.Generate(coord);
                m_Registry.Register(sig);
            }

            var result1 = m_Scanner.ScanUniverse(ValidCoord(1, 0, 0));
            var result2 = m_Scanner.ScanUniverse(ValidCoord(1, 0, 1));
            var result3 = m_Scanner.ScanUniverse(ValidCoord(1, 0, 2));

            Assert.IsTrue(result1.Success);
            Assert.IsTrue(result2.Success);
            Assert.IsFalse(result3.Success);
            Assert.AreEqual(DiscoveryStatus.Failed, result3.DiscoveryStatus);
            Assert.AreEqual("Discovery limit reached.", result3.FailureReason);
        }

        [Test]
        public void Scan_DiscoveryLimit_EnforcedPerSession()
        {
            m_ScanCfg.maxDiscoveries = 3;
            m_Scanner = new OmniversalScanner(m_Registry, m_Cfg, m_ScanCfg);

            for (int i = 0; i < 4; i++)
            {
                var coord = ValidCoord(1, 0, i);
                var sig = UniverseSignature.Generate(coord);
                m_Registry.Register(sig);
            }

            Assert.IsTrue(m_Scanner.HasDiscoveryCapacity);
            m_Scanner.ScanUniverse(ValidCoord(1, 0, 0));
            m_Scanner.ScanUniverse(ValidCoord(1, 0, 1));
            m_Scanner.ScanUniverse(ValidCoord(1, 0, 2));
            Assert.IsFalse(m_Scanner.HasDiscoveryCapacity);

            var result4 = m_Scanner.ScanUniverse(ValidCoord(1, 0, 3));
            Assert.IsFalse(result4.Success);
        }

        [Test]
        public void ResetDiscoveryCount_AllowsNewDiscoveries()
        {
            m_ScanCfg.maxDiscoveries = 2;
            m_Scanner = new OmniversalScanner(m_Registry, m_Cfg, m_ScanCfg);

            var coord1 = ValidCoord(1, 0, 0);
            var coord2 = ValidCoord(1, 0, 1);
            var sig1 = UniverseSignature.Generate(coord1);
            var sig2 = UniverseSignature.Generate(coord2);
            m_Registry.Register(sig1);
            m_Registry.Register(sig2);

            m_Scanner.ScanUniverse(coord1);
            m_Scanner.ScanUniverse(coord2);
            Assert.IsFalse(m_Scanner.HasDiscoveryCapacity);

            m_Scanner.ResetDiscoveryCount();
            Assert.IsTrue(m_Scanner.HasDiscoveryCapacity);

            var coord3 = ValidCoord(1, 0, 2);
            var sig3 = UniverseSignature.Generate(coord3);
            m_Registry.Register(sig3);
            var result3 = m_Scanner.ScanUniverse(coord3);
            Assert.IsTrue(result3.Success);
        }

        [Test]
        public void Scan_Confidence_CalculatedWithRiftInterferenceAttenuation()
        {
            m_ScanCfg.riftInterferenceConfidenceAttenuation = 0.5f;
            m_Scanner = new OmniversalScanner(m_Registry, m_Cfg, m_ScanCfg);

            var coord = ValidCoord();
            var profile = new UniverseStructuralProfile
            {
                DestinationInstability = 0.0,
                RiftInterference = 0.8
            };
            var sig = UniverseSignature.Generate(coord, profile);
            m_Registry.Register(sig);

            var result = m_Scanner.ScanUniverse(coord);

            Assert.IsTrue(result.Success);
            // Confidence = 1.0 - 0.8 * 0.5 = 0.6, clamped to min 0.1
            Assert.AreEqual(0.6, result.Confidence, 1e-9);
        }

        [Test]
        public void Scan_Confidence_MinimumClampedAt01()
        {
            m_ScanCfg.riftInterferenceConfidenceAttenuation = 1.0f;
            m_Scanner = new OmniversalScanner(m_Registry, m_Cfg, m_ScanCfg);

            var coord = ValidCoord();
            var profile = new UniverseStructuralProfile
            {
                DestinationInstability = 0.0,
                RiftInterference = 2.0
            };
            var sig = UniverseSignature.Generate(coord, profile);
            m_Registry.Register(sig);

            var result = m_Scanner.ScanUniverse(coord);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(0.1, result.Confidence, 1e-9);
        }

        [Test]
        public void Scan_RiftInterferenceAttenuation_ValidationClampsToRange()
        {
            m_ScanCfg.riftInterferenceConfidenceAttenuation = 1.5f; // Should clamp to 1.0
            m_Scanner = new OmniversalScanner(m_Registry, m_Cfg, m_ScanCfg);

            var coord = ValidCoord();
            var profile = new UniverseStructuralProfile
            {
                DestinationInstability = 0.0,
                RiftInterference = 1.0
            };
            var sig = UniverseSignature.Generate(coord, profile);
            m_Registry.Register(sig);

            var result = m_Scanner.ScanUniverse(coord);

            // With attenuation clamped to 1.0: confidence = 1.0 - 1.0 * 1.0 = 0.0 -> clamped to 0.1
            Assert.AreEqual(0.1, result.Confidence, 1e-9);
        }

        [Test]
        public void ScanConfiguration_IsValid_ValidatesAllFields()
        {
            var cfg = new ScanConfiguration
            {
                frequency = 10f,
                cacheDuration = 5f,
                enableCaching = true,
                scanRange = 5f,
                discoveryLimit = 10,
                maxDiscoveries = 10,
                riftInterferenceConfidenceAttenuation = 0.1f
            };
            Assert.IsTrue(cfg.IsValid());

            cfg.frequency = 0;
            Assert.IsFalse(cfg.IsValid());
            cfg = new ScanConfiguration { frequency = 10f, cacheDuration = 5f, enableCaching = true, scanRange = 5f, discoveryLimit = 10, maxDiscoveries = 10, riftInterferenceConfidenceAttenuation = 0.1f };
            cfg.cacheDuration = -1;
            Assert.IsFalse(cfg.IsValid());
            cfg = new ScanConfiguration { frequency = 10f, cacheDuration = 5f, enableCaching = true, scanRange = 5f, discoveryLimit = 10, maxDiscoveries = 10, riftInterferenceConfidenceAttenuation = 0.1f };
            cfg.scanRange = -1;
            Assert.IsFalse(cfg.IsValid());
            cfg = new ScanConfiguration { frequency = 10f, cacheDuration = 5f, enableCaching = true, scanRange = 5f, discoveryLimit = 10, maxDiscoveries = 10, riftInterferenceConfidenceAttenuation = 0.1f };
            cfg.discoveryLimit = -1;
            Assert.IsFalse(cfg.IsValid());
            cfg = new ScanConfiguration { frequency = 10f, cacheDuration = 5f, enableCaching = true, scanRange = 5f, discoveryLimit = 10, maxDiscoveries = 10, riftInterferenceConfidenceAttenuation = 0.1f };
            cfg.maxDiscoveries = -1;
            Assert.IsFalse(cfg.IsValid());
            cfg = new ScanConfiguration { frequency = 10f, cacheDuration = 5f, enableCaching = true, scanRange = 5f, discoveryLimit = 10, maxDiscoveries = 10, riftInterferenceConfidenceAttenuation = 0.1f };
            cfg.riftInterferenceConfidenceAttenuation = -0.1f;
            Assert.IsFalse(cfg.IsValid());
            cfg = new ScanConfiguration { frequency = 10f, cacheDuration = 5f, enableCaching = true, scanRange = 5f, discoveryLimit = 10, maxDiscoveries = 10, riftInterferenceConfidenceAttenuation = 0.1f };
            cfg.riftInterferenceConfidenceAttenuation = 1.1f;
            Assert.IsFalse(cfg.IsValid());
            cfg = new ScanConfiguration { frequency = 10f, cacheDuration = 5f, enableCaching = true, scanRange = 5f, discoveryLimit = 10, maxDiscoveries = 10, riftInterferenceConfidenceAttenuation = 0.1f };
            cfg.riftInterferenceConfidenceAttenuation = float.NaN;
            Assert.IsFalse(cfg.IsValid());
        }

        [Test]
        public void Scanner_Tick_ProcessesQueue()
        {
            var coord1 = ValidCoord(1, 0, 0);
            var coord2 = ValidCoord(1, 0, 1);
            var sig1 = UniverseSignature.Generate(coord1);
            var sig2 = UniverseSignature.Generate(coord2);
            m_Registry.Register(sig1);
            m_Registry.Register(sig2);

            m_Scanner.EnqueueDestination(coord1);
            m_Scanner.EnqueueDestination(coord2);

            Assert.AreEqual(2, m_Scanner.PendingCount);

            // MaxScansPerTick defaults to 1, so only 1 scan per Tick
            var result = m_Scanner.Tick(1f); // 1 second at 10Hz = 10 scans possible, but MaxScansPerTick=1

            Assert.AreEqual(1, m_Scanner.PendingCount);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);

            // Process the second
            var result2 = m_Scanner.Tick(1f);
            Assert.AreEqual(0, m_Scanner.PendingCount);
            Assert.IsNotNull(result2);
            Assert.IsTrue(result2.Success);
        }

        [Test]
        public void Scanner_ProcessQueue_ReturnsMultipleResults()
        {
            var coord1 = ValidCoord(1, 0, 0);
            var coord2 = ValidCoord(1, 0, 1);
            var sig1 = UniverseSignature.Generate(coord1);
            var sig2 = UniverseSignature.Generate(coord2);
            m_Registry.Register(sig1);
            m_Registry.Register(sig2);

            m_Scanner.EnqueueDestination(coord1);
            m_Scanner.EnqueueDestination(coord2);

            var results = m_Scanner.ProcessQueue(2);

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results[0].Success);
            Assert.IsTrue(results[1].Success);
        }

        [Test]
        public void Scanner_ScanNext_ReturnsNextQueued()
        {
            var coord1 = ValidCoord(1, 0, 0);
            var coord2 = ValidCoord(1, 0, 1);
            var sig1 = UniverseSignature.Generate(coord1);
            var sig2 = UniverseSignature.Generate(coord2);
            m_Registry.Register(sig1);
            m_Registry.Register(sig2);

            m_Scanner.EnqueueDestination(coord1);
            m_Scanner.EnqueueDestination(coord2);

            var result1 = m_Scanner.ScanNext();
            var result2 = m_Scanner.ScanNext();
            var result3 = m_Scanner.ScanNext();

            Assert.IsNotNull(result1);
            Assert.IsTrue(result1.Success);
            Assert.IsNotNull(result2);
            Assert.IsTrue(result2.Success);
            Assert.IsNull(result3);
        }

        [Test]
        public void Scan_WithOrigin_CalculatesStructuralDivergence()
        {
            var origin = ValidCoord(1, 0, 0);
            var dest = ValidCoord(2, 1, 50);
            var sig = UniverseSignature.Generate(dest);
            m_Registry.Register(sig);

            var result = m_Scanner.ScanUniverse(dest, origin);

            Assert.IsTrue(result.Success);
            Assert.GreaterOrEqual(result.StructuralDivergence, 0.0);
            Assert.LessOrEqual(result.StructuralDivergence, 1.0);
        }

        [Test]
        public void Scan_Confidence_ValidSignatureStartsAt1()
        {
            var coord = ValidCoord();
            var sig = UniverseSignature.Generate(coord);
            m_Registry.Register(sig);

            var result = m_Scanner.ScanUniverse(coord);

            Assert.AreEqual(1.0, result.Confidence, 1e-9);
        }

        [Test]
        public void Scan_DiscoveryStatus_ProgressesCorrectly()
        {
            var coord = ValidCoord();
            var sig = UniverseSignature.Generate(coord);
            m_Registry.Register(sig);

            Assert.AreEqual(DiscoveryStatus.Registered, m_Registry.GetDiscoveryStatus(coord));

            var result = m_Scanner.ScanUniverse(coord);

            Assert.AreEqual(DiscoveryStatus.Discovered, result.DiscoveryStatus);
            Assert.AreEqual(DiscoveryStatus.Discovered, m_Registry.GetDiscoveryStatus(coord));
        }

        [Test]
        public void Scan_CacheDuration_ExpiredCacheRescans()
        {
            float fixedTime = 1000f;
            m_Scanner.SetTimeProvider(() => fixedTime);

            var coord = ValidCoord();
            var sig = UniverseSignature.Generate(coord);
            m_Registry.Register(sig);

            var result1 = m_Scanner.ScanUniverse(coord);
            fixedTime += 10f; // 10 seconds later, cache duration is 5f
            m_Scanner.SetTimeProvider(() => fixedTime);

            var result2 = m_Scanner.ScanUniverse(coord);

            Assert.AreNotEqual(result1.SequenceNumber, result2.SequenceNumber);
            Assert.AreNotEqual(result1.Timestamp, result2.Timestamp);
        }

        [Test]
        public void Scan_CachedScan_DoesNotIncrementDiscoveryCount()
        {
            m_ScanCfg.maxDiscoveries = 5;
            m_Scanner = new OmniversalScanner(m_Registry, m_Cfg, m_ScanCfg);
            float fixedTime = 1000f;
            m_Scanner.SetTimeProvider(() => fixedTime);

            var coord = ValidCoord(1, 0, 0);
            var sig = UniverseSignature.Generate(coord);
            m_Registry.Register(sig);

            var result1 = m_Scanner.ScanUniverse(coord);
            Assert.IsTrue(result1.Success);
            Assert.AreEqual(1, m_Scanner.DiscoveriesThisSession);

            fixedTime += 1f; // within cache duration
            var result2 = m_Scanner.ScanUniverse(coord);
            Assert.IsTrue(result2.Success);
            Assert.AreEqual(result1.SequenceNumber, result2.SequenceNumber); // cached
            Assert.AreEqual(1, m_Scanner.DiscoveriesThisSession); // not incremented
        }

        [Test]
        public void Scan_CachedScan_AtLimit_DoesNotIncrementCount()
        {
            m_ScanCfg.maxDiscoveries = 1;
            m_Scanner = new OmniversalScanner(m_Registry, m_Cfg, m_ScanCfg);
            float fixedTime = 1000f;
            m_Scanner.SetTimeProvider(() => fixedTime);

            var coord = ValidCoord(1, 0, 0);
            var sig = UniverseSignature.Generate(coord);
            m_Registry.Register(sig);

            var result1 = m_Scanner.ScanUniverse(coord);
            Assert.IsTrue(result1.Success);
            Assert.AreEqual(1, m_Scanner.DiscoveriesThisSession);
            Assert.IsFalse(m_Scanner.HasDiscoveryCapacity);

            // Cached scan of the same destination at the limit must not increment.
            fixedTime += 1f;
            var result2 = m_Scanner.ScanUniverse(coord);
            Assert.IsTrue(result2.Success);
            Assert.AreEqual(result1.SequenceNumber, result2.SequenceNumber);
            Assert.AreEqual(1, m_Scanner.DiscoveriesThisSession);
        }

        [Test]
        public void Scan_NewDestination_AtLimit_IsRejected()
        {
            m_ScanCfg.maxDiscoveries = 1;
            m_Scanner = new OmniversalScanner(m_Registry, m_Cfg, m_ScanCfg);

            var coordA = ValidCoord(1, 0, 0);
            var coordB = ValidCoord(1, 0, 1);
            var sigA = UniverseSignature.Generate(coordA);
            var sigB = UniverseSignature.Generate(coordB);
            m_Registry.Register(sigA);
            m_Registry.Register(sigB);

            var resultA = m_Scanner.ScanUniverse(coordA);
            Assert.IsTrue(resultA.Success);
            Assert.IsFalse(m_Scanner.HasDiscoveryCapacity);

            // A new (uncached) destination at the limit must be rejected.
            var resultB = m_Scanner.ScanUniverse(coordB);
            Assert.IsFalse(resultB.Success);
            Assert.AreEqual(DiscoveryStatus.Failed, resultB.DiscoveryStatus);
            Assert.AreEqual("Discovery limit reached.", resultB.FailureReason);
            Assert.AreEqual(1, m_Scanner.DiscoveriesThisSession);
        }

        [Test]
        public void Scan_RepeatedCachedScans_DoNotCreateDuplicates()
        {
            m_ScanCfg.maxDiscoveries = 5;
            m_Scanner = new OmniversalScanner(m_Registry, m_Cfg, m_ScanCfg);
            float fixedTime = 1000f;
            m_Scanner.SetTimeProvider(() => fixedTime);

            var coord = ValidCoord(1, 0, 0);
            var sig = UniverseSignature.Generate(coord);
            m_Registry.Register(sig);

            var result1 = m_Scanner.ScanUniverse(coord);
            Assert.IsTrue(result1.Success);

            fixedTime += 1f;
            var result2 = m_Scanner.ScanUniverse(coord);
            fixedTime += 1f;
            var result3 = m_Scanner.ScanUniverse(coord);

            Assert.IsTrue(result2.Success);
            Assert.IsTrue(result3.Success);
            Assert.AreEqual(1, m_Registry.Count); // no duplicate registry entry
            Assert.AreEqual(1, m_Scanner.DiscoveriesThisSession); // only first scan counted
        }

        [Test]
        public void Scan_ResetDiscoveryCount_ThenNewDiscovery()
        {
            m_ScanCfg.maxDiscoveries = 1;
            m_Scanner = new OmniversalScanner(m_Registry, m_Cfg, m_ScanCfg);

            var coordA = ValidCoord(1, 0, 0);
            var coordB = ValidCoord(1, 0, 1);
            var sigA = UniverseSignature.Generate(coordA);
            var sigB = UniverseSignature.Generate(coordB);
            m_Registry.Register(sigA);
            m_Registry.Register(sigB);

            var resultA = m_Scanner.ScanUniverse(coordA);
            Assert.IsTrue(resultA.Success);
            Assert.IsFalse(m_Scanner.HasDiscoveryCapacity);

            m_Scanner.ResetDiscoveryCount();
            Assert.AreEqual(0, m_Scanner.DiscoveriesThisSession);
            Assert.IsTrue(m_Scanner.HasDiscoveryCapacity);

            var resultB = m_Scanner.ScanUniverse(coordB);
            Assert.IsTrue(resultB.Success);
            Assert.AreEqual(1, m_Scanner.DiscoveriesThisSession);
        }

        [Test]
        public void Scan_FailedScan_DoesNotConsumeCapacity()
        {
            m_ScanCfg.maxDiscoveries = 1;
            m_Scanner = new OmniversalScanner(m_Registry, m_Cfg, m_ScanCfg);

            var invalidCoord = new OmniversalCoordinates(0, 0, 0, 0, 0, Vector3.zero);
            var validCoord = ValidCoord(1, 0, 0);
            var sig = UniverseSignature.Generate(validCoord);
            m_Registry.Register(sig);

            // Failed scan must not consume discovery capacity.
            var failed = m_Scanner.ScanUniverse(invalidCoord);
            Assert.IsFalse(failed.Success);
            Assert.AreEqual(0, m_Scanner.DiscoveriesThisSession);
            Assert.IsTrue(m_Scanner.HasDiscoveryCapacity);

            var success = m_Scanner.ScanUniverse(validCoord);
            Assert.IsTrue(success.Success);
            Assert.AreEqual(1, m_Scanner.DiscoveriesThisSession);
            Assert.IsFalse(m_Scanner.HasDiscoveryCapacity);
        }

        [Test]
        public void Scan_FailedScan_AtLimit_DoesNotConsumeCapacity()
        {
            m_ScanCfg.maxDiscoveries = 1;
            m_Scanner = new OmniversalScanner(m_Registry, m_Cfg, m_ScanCfg);

            var validCoord = ValidCoord(1, 0, 0);
            var sig = UniverseSignature.Generate(validCoord);
            m_Registry.Register(sig);

            var success = m_Scanner.ScanUniverse(validCoord);
            Assert.IsTrue(success.Success);
            Assert.IsFalse(m_Scanner.HasDiscoveryCapacity);

            // Failed scan at the limit must not increment or change state.
            var invalidCoord = new OmniversalCoordinates(0, 0, 0, 0, 0, Vector3.zero);
            var failed = m_Scanner.ScanUniverse(invalidCoord);
            Assert.IsFalse(failed.Success);
            Assert.AreEqual(1, m_Scanner.DiscoveriesThisSession);
        }

        [Test]
        public void Scan_DiscoveryCount_TracksCorrectly()
        {
            m_ScanCfg.maxDiscoveries = 5;
            m_Scanner = new OmniversalScanner(m_Registry, m_Cfg, m_ScanCfg);

            Assert.AreEqual(0, m_Scanner.DiscoveriesThisSession);

            for (int i = 0; i < 3; i++)
            {
                var coord = ValidCoord(1, 0, i);
                var sig = UniverseSignature.Generate(coord);
                m_Registry.Register(sig);
                m_Scanner.ScanUniverse(coord);
            }

            Assert.AreEqual(3, m_Scanner.DiscoveriesThisSession);
        }
    }
}