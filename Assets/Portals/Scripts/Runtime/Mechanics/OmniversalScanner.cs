using System;
using System.Collections.Generic;
using UnityEngine;

namespace Portals.Runtime.Mechanics
{
    public class OmniversalScanner
    {
        public UniverseRegistry Database { get; set; }
        public OPMConfiguration Configuration { get; set; }
        public ScanConfiguration ScanConfig { get; set; }
        public ScanState State { get; private set; } = ScanState.Idle;

        public event Action<OmniversalScanResult> ScanCompleted;

        // Simulated scan frequency is independent of the physical Unity update rate.
        public float SimulatedScanFrequency
        {
            get => ScanConfig != null ? ScanConfig.frequency : (Configuration != null ? Configuration.scannerFrequency : 10f);
            set
            {
                if (ScanConfig != null) ScanConfig.frequency = value;
                if (Configuration != null) Configuration.scannerFrequency = value;
            }
        }

        public float ScanFrequency
        {
            get => SimulatedScanFrequency;
            set => SimulatedScanFrequency = value;
        }

        public int MaxScansPerTick { get; set; } = 1;

        private readonly Dictionary<string, OmniversalScanResult> m_Cache = new Dictionary<string, OmniversalScanResult>();
        private readonly Dictionary<string, float> m_CacheTime = new Dictionary<string, float>();

        private readonly Queue<OmniversalCoordinates> m_Queue = new Queue<OmniversalCoordinates>();

        private Func<float> m_TimeProvider = () => UnityEngine.Time.realtimeSinceStartup;

        private int m_Sequence;
        private float m_Accumulator;
        private int m_DiscoveriesThisSession;

        public OmniversalScanner(UniverseRegistry database = null, OPMConfiguration config = null, ScanConfiguration scanConfig = null)
        {
            Database = database ?? new UniverseSignatureDatabase();
            Configuration = config ?? OPMConfiguration.CreateDefault();
            ScanConfig = scanConfig ?? new ScanConfiguration();
            m_DiscoveriesThisSession = 0;
        }

        public void SetTimeProvider(Func<float> provider) { if (provider != null) m_TimeProvider = provider; }

        public void ClearCache() { m_Cache.Clear(); m_CacheTime.Clear(); }

        public void ResetDiscoveryCount() { m_DiscoveriesThisSession = 0; }

        public int DiscoveriesThisSession => m_DiscoveriesThisSession;

        public bool HasDiscoveryCapacity => ScanConfig == null || m_DiscoveriesThisSession < ScanConfig.maxDiscoveries;

        public void EnqueueDestination(OmniversalCoordinates destination)
        {
            if (destination.IsValid(Configuration ?? OPMConfiguration.CreateDefault()))
                m_Queue.Enqueue(destination);
        }

        public void QueueDestination(OmniversalCoordinates destination) => EnqueueDestination(destination);
        public void AddDestination(OmniversalCoordinates destination) => EnqueueDestination(destination);
        public void Schedule(OmniversalCoordinates destination) => EnqueueDestination(destination);
        public void ScheduleScan(OmniversalCoordinates destination) => EnqueueDestination(destination);
        public int PendingCount => m_Queue.Count;

        public OmniversalScanResult Scan(OmniversalCoordinates destination, OmniversalCoordinates? origin = null, double coordinateError = 0.0)
            => ScanUniverse(destination, origin, coordinateError);

        public OmniversalScanResult ScanUniverse(OmniversalCoordinates destination, OmniversalCoordinates? origin = null, double coordinateError = 0.0)
        {
            State = ScanState.Scanning;
            OPMConfiguration cfg = Configuration ?? OPMConfiguration.CreateDefault();
            string key = UniverseSignatureDatabase.MakeKey(destination);

            // Cache check
            if (ScanConfig != null && ScanConfig.enableCaching && m_Cache.TryGetValue(key, out var cached))
            {
                if (m_CacheTime.TryGetValue(key, out float t) && m_TimeProvider() - t <= (ScanConfig.cacheDuration > 0 ? ScanConfig.cacheDuration : (cfg != null ? cfg.scannerCacheDuration : 5f)))
                {
                    State = ScanState.Complete;
                    ScanCompleted?.Invoke(cached);
                    return cached;
                }
            }

            // Enforce discovery limit per scan
            if (!HasDiscoveryCapacity)
            {
                State = ScanState.Failed;
                var limitResult = new OmniversalScanResult
                {
                    Destination = destination,
                    Timestamp = m_TimeProvider(),
                    SequenceNumber = ++m_Sequence,
                    DiscoveryStatus = DiscoveryStatus.Failed,
                    Success = false,
                    FailureReason = "Discovery limit reached."
                };
                ScanCompleted?.Invoke(limitResult);
                return limitResult;
            }

            State = ScanState.Analysing;
            OmniversalScanResult result = new OmniversalScanResult
            {
                Destination = destination,
                Timestamp = m_TimeProvider(),
                SequenceNumber = ++m_Sequence,
                DiscoveryStatus = DiscoveryStatus.Unknown
            };

            if (!destination.IsValid(cfg))
            {
                result.Success = false;
                result.FailureReason = "Invalid coordinates.";
                result.DiscoveryStatus = DiscoveryStatus.Failed;
                State = ScanState.Failed;
                ScanCompleted?.Invoke(result);
                return result;
            }

            if (Database == null || !Database.TryGet(destination, out var sig) || sig == null)
            {
                result.Success = false;
                result.FailureReason = "Destination unavailable.";
                result.DiscoveryStatus = DiscoveryStatus.Failed;
                State = ScanState.Failed;
                ScanCompleted?.Invoke(result);
                return result;
            }

            if (!sig.IsValid)
            {
                result.Success = false;
                result.FailureReason = "Invalid signature.";
                result.DiscoveryStatus = DiscoveryStatus.Failed;
                State = ScanState.Failed;
                ScanCompleted?.Invoke(result);
                return result;
            }

            result.Signature = sig;
            result.Success = true;
            result.Instability = sig.Profile != null ? sig.Profile.DestinationInstability : 0.0;
            result.RiftInterference = sig.Profile != null ? sig.Profile.RiftInterference : 0.0;

            // Structural divergence
            if (origin.HasValue && origin.Value.IsValid(cfg))
            {
                try
                {
                    result.StructuralDivergence = PortalStructuralAnalyzer.CalculateStructuralDivergence(origin.Value, destination, cfg);
                }
                catch
                {
                    result.StructuralDivergence = 0.0;
                }
            }
            else
            {
                result.StructuralDivergence = 0.0;
            }

            result.CoordinateError = coordinateError;
            try
            {
                result.Accuracy = PortalAccuracyCalculator.CalculateAccuracy(coordinateError);
            }
            catch
            {
                result.Accuracy = 1.0;
            }

            // Confidence based on validity and interference (configurable attenuation)
            float attenuation = ScanConfig != null ? ScanConfig.riftInterferenceConfidenceAttenuation : 0.1f;
            result.Confidence = sig.IsValid ? 1.0 : 0.5;
            if (result.RiftInterference > 0) result.Confidence = Math.Max(0.1, result.Confidence - result.RiftInterference * attenuation);

            result.DiscoveryStatus = DiscoveryStatus.Discovered;

            // Register on successful scan (discovery registers; does not create identity).
            try { Database.MarkDiscovered(sig); } catch { Database.Register(sig); }
            m_DiscoveriesThisSession++;

            // Cache
            if (ScanConfig != null && ScanConfig.enableCaching)
            {
                m_Cache[key] = result;
                m_CacheTime[key] = result.Timestamp;
            }

            State = ScanState.Complete;
            ScanCompleted?.Invoke(result);
            return result;
        }

        /// <summary>
        /// Generates a universe signature without registering it in the database.
        /// </summary>
        public UniverseSignature GenerateSignature(OmniversalCoordinates destination) => UniverseSignature.Generate(destination);

        /// <summary>
        /// Creates a universe signature without registering it in the database.
        /// </summary>
        public UniverseSignature CreateSignature(OmniversalCoordinates destination) => UniverseSignature.Generate(destination);

        /// <summary>
        /// Registers a signature in the database without scanning.
        /// </summary>
        public bool RegisterSignature(UniverseSignature signature)
        {
            if (signature == null) return false;
            Database.Register(signature);
            return true;
        }

        /// <summary>
        /// Identifies a destination: ensures a signature exists in the database and marks it Pending.
        /// Does not perform a full scan.
        /// </summary>
        public bool IdentifyDestination(OmniversalCoordinates destination)
        {
            OPMConfiguration cfg = Configuration ?? OPMConfiguration.CreateDefault();
            if (!destination.IsValid(cfg)) return false;
            // Identity exists regardless of discovery; ensure signature available.
            if (Database == null) return false;
            if (!Database.Contains(destination))
            {
                var sig = UniverseSignature.Generate(destination);
                Database.Register(sig);
                Database.SetDiscoveryStatus(destination, DiscoveryStatus.Pending);
            }
            return true;
        }

        public OmniversalScanResult Tick(float deltaTime)
        {
            m_Accumulator += deltaTime;
            float interval = 1f / Math.Max(0.0001f, SimulatedScanFrequency);
            OmniversalScanResult last = null;
            int processed = 0;
            while (m_Accumulator >= interval && processed < MaxScansPerTick && m_Queue.Count > 0)
            {
                m_Accumulator -= interval;
                var coord = m_Queue.Dequeue();
                last = ScanUniverse(coord);
                processed++;
            }
            // Prevent accumulator blowup without processing (logical cap).
            if (m_Queue.Count == 0) m_Accumulator = 0f;
            else if (m_Accumulator > interval * 4f) m_Accumulator = interval * 4f;
            return last;
        }

        public List<OmniversalScanResult> ProcessQueue(int maxCount)
        {
            var results = new List<OmniversalScanResult>();
            int n = Math.Min(maxCount, m_Queue.Count);
            for (int i = 0; i < n; i++)
            {
                if (m_Queue.Count == 0) break;
                var coord = m_Queue.Dequeue();
                results.Add(ScanUniverse(coord));
            }
            return results;
        }

        public OmniversalScanResult ScanNext()
        {
            if (m_Queue.Count == 0) return null;
            return ScanUniverse(m_Queue.Dequeue());
        }
    }
}
