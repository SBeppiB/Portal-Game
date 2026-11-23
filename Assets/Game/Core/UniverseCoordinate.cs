using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Address of an alternate universe: Layer-Ring-Position (e.g. L03-R02-P044).
    /// Serializable and inspector-friendly. Physics is identical across coordinates;
    /// only content varies.
    /// </summary>
    [Serializable]
    public struct UniverseCoordinate : IEquatable<UniverseCoordinate>
    {
        [SerializeField, Min(0)] private int m_Layer;
        [SerializeField, Range(0, 10)] private int m_Ring;
        [SerializeField, Range(0, 999)] private int m_Position;

        public int Layer => m_Layer;
        public int Ring => m_Ring;
        public int Position => m_Position;

        public UniverseCoordinate(int layer, int ring, int position)
        {
            m_Layer = Mathf.Max(0, layer);
            m_Ring = Mathf.Clamp(ring, 0, 10);
            m_Position = Mathf.Clamp(position, 0, 999);
        }

        public static UniverseCoordinate Prime => new UniverseCoordinate(0, 0, 0);

        public override string ToString() => $"L{m_Layer:00}-R{m_Ring:00}-P{m_Position:000}";

        public static bool TryParse(string text, out UniverseCoordinate coord)
        {
            coord = Prime;
            if (string.IsNullOrWhiteSpace(text)) return false;

            text = text.Trim().ToUpperInvariant();
            // Accept "L03-R02-P044" (or "L3-R2-P44").
            string[] parts = text.Split('-');
            if (parts.Length != 3) return false;

            if (!parts[0].StartsWith("L") || !parts[1].StartsWith("R") || !parts[2].StartsWith("P"))
                return false;

            if (!int.TryParse(parts[0].Substring(1), out int layer)) return false;
            if (!int.TryParse(parts[1].Substring(1), out int ring)) return false;
            if (!int.TryParse(parts[2].Substring(1), out int position)) return false;

            coord = new UniverseCoordinate(layer, ring, position);
            return true;
        }

        public static UniverseCoordinate Parse(string text)
        {
            if (!TryParse(text, out var coord))
                throw new FormatException($"Invalid universe coordinate: '{text}'");
            return coord;
        }

        public bool Equals(UniverseCoordinate other) =>
            m_Layer == other.m_Layer && m_Ring == other.m_Ring && m_Position == other.m_Position;

        public override bool Equals(object obj) => obj is UniverseCoordinate c && Equals(c);

        public override int GetHashCode() => (m_Layer * 397) ^ (m_Ring * 31) ^ m_Position;

        public static bool operator ==(UniverseCoordinate a, UniverseCoordinate b) => a.Equals(b);
        public static bool operator !=(UniverseCoordinate a, UniverseCoordinate b) => !a.Equals(b);
    }
}