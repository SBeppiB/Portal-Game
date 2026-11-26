using System;

namespace Portals.Runtime.Mechanics
{
    [Serializable]
    public class ScanConfiguration
    {
        public float frequency = 10f;
        public float cacheDuration = 5f;
        public bool enableCaching = true;

        // Scan range / discovery limits (tunable).
        public float scanRange = 5f;
        public int discoveryLimit = 10;

        /// <summary>
        /// Maximum number of successful new discoveries per session. Cached scans do not consume
        /// capacity; only newly discovered destinations increment the session counter.
        /// Use <see cref="OmniversalScanner.ResetDiscoveryCount"/> to reset the session counter.
        /// </summary>
        public int maxDiscoveries = 10;

        // Rift interference confidence attenuation (0..1). How much rift interference reduces confidence.
        public float riftInterferenceConfidenceAttenuation = 0.1f;

        // Alias properties (PascalCase / alternative names).
        public float Frequency { get => frequency; set => frequency = value; }
        public float CacheDuration { get => cacheDuration; set => cacheDuration = value; }
        public bool EnableCaching { get => enableCaching; set => enableCaching = value; }

        public float ScanRange { get => scanRange; set => scanRange = value; }
        public float Range { get => scanRange; set => scanRange = value; }
        public float DiscoveryRange { get => scanRange; set => scanRange = value; }
        public float ScanRadius { get => scanRange; set => scanRange = value; }
        public float Radius { get => scanRange; set => scanRange = value; }

        public int DiscoveryLimit { get => discoveryLimit; set => discoveryLimit = value; }
        public int MaxDiscoveries { get => maxDiscoveries; set => maxDiscoveries = value; }
        public int MaxDiscoveryCount { get => maxDiscoveries; set => maxDiscoveries = value; }
        public int MaxDiscoveriesCount { get => maxDiscoveries; set => maxDiscoveries = value; }

        public float RiftInterferenceConfidenceAttenuation
        {
            get => riftInterferenceConfidenceAttenuation;
            set => riftInterferenceConfidenceAttenuation = Math.Clamp(value, 0f, 1f);
        }

        // Lowercase alias properties for common field-style access.
        public float scanRadius { get => scanRange; set => scanRange = value; }
        public float discoveryRange { get => scanRange; set => scanRange = value; }
        public float radius { get => scanRange; set => scanRange = value; }
        public int discoveryLimitCount { get => discoveryLimit; set => discoveryLimit = value; }
        public float riftInterferenceConfidenceAttenuationAlias
        {
            get => riftInterferenceConfidenceAttenuation;
            set => riftInterferenceConfidenceAttenuation = Math.Clamp(value, 0f, 1f);
        }

        public bool IsValid()
        {
            if (frequency <= 0f || !float.IsFinite(frequency)) return false;
            if (cacheDuration < 0f || !float.IsFinite(cacheDuration)) return false;
            if (scanRange < 0f || !float.IsFinite(scanRange)) return false;
            if (discoveryLimit < 0) return false;
            if (maxDiscoveries < 0) return false;
            if (riftInterferenceConfidenceAttenuation < 0f || riftInterferenceConfidenceAttenuation > 1f || !float.IsFinite(riftInterferenceConfidenceAttenuation)) return false;
            return true;
        }
    }
}
