using System;

namespace Portals.Runtime.Mechanics
{
    [Serializable]
    public class OmniversalScanResult
    {
        public bool Success;
        public OmniversalCoordinates Destination;
        public UniverseSignature Signature;
        public double StructuralDivergence;
        public double Instability;
        public double CoordinateError;
        public double Accuracy;

        private double m_Confidence;
        public double Confidence
        {
            get => m_Confidence;
            set => m_Confidence = Math.Max(0.0, Math.Min(1.0, value));
        }

        public double RiftInterference;
        public float Timestamp;
        public int SequenceNumber;
        public DiscoveryStatus DiscoveryStatus = DiscoveryStatus.Unknown;
        public string FailureReason = string.Empty;
    }
}
