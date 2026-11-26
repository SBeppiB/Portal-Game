using System;

namespace Portals.Runtime.Mechanics
{
    [Serializable]
    public struct PortalStructuralDivergence : IEquatable<PortalStructuralDivergence>
    {
        public double LayerDivergence { get; }
        public double RingDivergence { get; }
        public double AuDivergence { get; }
        public double TotalDivergence { get; }
        public double Compatibility { get; }

        public PortalStructuralDivergence(double layerDivergence, double ringDivergence, double auDivergence, double totalDivergence, double compatibility)
        {
            LayerDivergence = layerDivergence;
            RingDivergence = ringDivergence;
            AuDivergence = auDivergence;
            TotalDivergence = totalDivergence;
            Compatibility = compatibility;
        }

        public bool Equals(PortalStructuralDivergence other) =>
            LayerDivergence == other.LayerDivergence &&
            RingDivergence == other.RingDivergence &&
            AuDivergence == other.AuDivergence &&
            TotalDivergence == other.TotalDivergence &&
            Compatibility == other.Compatibility;

        public override bool Equals(object obj) => obj is PortalStructuralDivergence other && Equals(other);
        public override int GetHashCode() => (LayerDivergence, RingDivergence, AuDivergence, TotalDivergence, Compatibility).GetHashCode();
        public static bool operator ==(PortalStructuralDivergence a, PortalStructuralDivergence b) => a.Equals(b);
        public static bool operator !=(PortalStructuralDivergence a, PortalStructuralDivergence b) => !a.Equals(b);
    }

    [Serializable]
    public struct PortalStructuralDivergenceResult : IEquatable<PortalStructuralDivergenceResult>
    {
        public double LayerDivergence { get; }
        public double RingDivergence { get; }
        public double AuDivergence { get; }
        public double TotalDivergence { get; }
        public double Compatibility { get; }

        public PortalStructuralDivergenceResult(double layerDivergence, double ringDivergence, double auDivergence, double totalDivergence, double compatibility)
        {
            LayerDivergence = layerDivergence;
            RingDivergence = ringDivergence;
            AuDivergence = auDivergence;
            TotalDivergence = totalDivergence;
            Compatibility = compatibility;
        }

        public bool Equals(PortalStructuralDivergenceResult other) =>
            LayerDivergence == other.LayerDivergence &&
            RingDivergence == other.RingDivergence &&
            AuDivergence == other.AuDivergence &&
            TotalDivergence == other.TotalDivergence &&
            Compatibility == other.Compatibility;

        public override bool Equals(object obj) => obj is PortalStructuralDivergenceResult other && Equals(other);
        public override int GetHashCode() => (LayerDivergence, RingDivergence, AuDivergence, TotalDivergence, Compatibility).GetHashCode();
        public static bool operator ==(PortalStructuralDivergenceResult a, PortalStructuralDivergenceResult b) => a.Equals(b);
        public static bool operator !=(PortalStructuralDivergenceResult a, PortalStructuralDivergenceResult b) => !a.Equals(b);

        public static implicit operator PortalStructuralDivergenceResult(PortalStructuralDivergence v) =>
            new PortalStructuralDivergenceResult(v.LayerDivergence, v.RingDivergence, v.AuDivergence, v.TotalDivergence, v.Compatibility);
        public static implicit operator PortalStructuralDivergence(PortalStructuralDivergenceResult v) =>
            new PortalStructuralDivergence(v.LayerDivergence, v.RingDivergence, v.AuDivergence, v.TotalDivergence, v.Compatibility);
    }

    [Serializable]
    public struct StructuralDivergenceResult : IEquatable<StructuralDivergenceResult>
    {
        public double LayerDivergence { get; }
        public double RingDivergence { get; }
        public double AuDivergence { get; }
        public double TotalDivergence { get; }
        public double Compatibility { get; }

        public StructuralDivergenceResult(double layerDivergence, double ringDivergence, double auDivergence, double totalDivergence, double compatibility)
        {
            LayerDivergence = layerDivergence;
            RingDivergence = ringDivergence;
            AuDivergence = auDivergence;
            TotalDivergence = totalDivergence;
            Compatibility = compatibility;
        }

        public bool Equals(StructuralDivergenceResult other) =>
            LayerDivergence == other.LayerDivergence &&
            RingDivergence == other.RingDivergence &&
            AuDivergence == other.AuDivergence &&
            TotalDivergence == other.TotalDivergence &&
            Compatibility == other.Compatibility;

        public override bool Equals(object obj) => obj is StructuralDivergenceResult other && Equals(other);
        public override int GetHashCode() => (LayerDivergence, RingDivergence, AuDivergence, TotalDivergence, Compatibility).GetHashCode();
        public static bool operator ==(StructuralDivergenceResult a, StructuralDivergenceResult b) => a.Equals(b);
        public static bool operator !=(StructuralDivergenceResult a, StructuralDivergenceResult b) => !a.Equals(b);

        public static implicit operator StructuralDivergenceResult(PortalStructuralDivergence v) =>
            new StructuralDivergenceResult(v.LayerDivergence, v.RingDivergence, v.AuDivergence, v.TotalDivergence, v.Compatibility);
        public static implicit operator PortalStructuralDivergence(StructuralDivergenceResult v) =>
            new PortalStructuralDivergence(v.LayerDivergence, v.RingDivergence, v.AuDivergence, v.TotalDivergence, v.Compatibility);
    }

    [Serializable]
    public struct StructuralDivergence : IEquatable<StructuralDivergence>
    {
        public double LayerDivergence { get; }
        public double RingDivergence { get; }
        public double AuDivergence { get; }
        public double TotalDivergence { get; }
        public double Compatibility { get; }

        public StructuralDivergence(double layerDivergence, double ringDivergence, double auDivergence, double totalDivergence, double compatibility)
        {
            LayerDivergence = layerDivergence;
            RingDivergence = ringDivergence;
            AuDivergence = auDivergence;
            TotalDivergence = totalDivergence;
            Compatibility = compatibility;
        }

        public bool Equals(StructuralDivergence other) =>
            LayerDivergence == other.LayerDivergence &&
            RingDivergence == other.RingDivergence &&
            AuDivergence == other.AuDivergence &&
            TotalDivergence == other.TotalDivergence &&
            Compatibility == other.Compatibility;

        public override bool Equals(object obj) => obj is StructuralDivergence other && Equals(other);
        public override int GetHashCode() => (LayerDivergence, RingDivergence, AuDivergence, TotalDivergence, Compatibility).GetHashCode();
        public static bool operator ==(StructuralDivergence a, StructuralDivergence b) => a.Equals(b);
        public static bool operator !=(StructuralDivergence a, StructuralDivergence b) => !a.Equals(b);

        public static implicit operator StructuralDivergence(PortalStructuralDivergence v) =>
            new StructuralDivergence(v.LayerDivergence, v.RingDivergence, v.AuDivergence, v.TotalDivergence, v.Compatibility);
        public static implicit operator PortalStructuralDivergence(StructuralDivergence v) =>
            new PortalStructuralDivergence(v.LayerDivergence, v.RingDivergence, v.AuDivergence, v.TotalDivergence, v.Compatibility);
    }
}
