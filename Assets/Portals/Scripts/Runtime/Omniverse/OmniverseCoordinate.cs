using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Omniverse
{
    /// <summary>
    /// Omniverse address: Layer (1-50) / Ring (0-10) / Position (0-179).
    /// Position is circular: P000 is adjacent to P179.
    /// </summary>
    [Serializable]
    public struct OmniverseCoordinate : IEquatable<OmniverseCoordinate>
    {
        [SerializeField, Range(1, 50)] private int m_Layer;
        [SerializeField, Range(0, 10)] private int m_Ring;
        [SerializeField, Range(0, 179)] private int m_Position;

        public int Layer => m_Layer;
        public int Ring => m_Ring;
        public int Position => m_Position;

        public const int PositionsPerRing = 180;

        public OmniverseCoordinate(int layer, int ring, int position)
        {
            m_Layer = Mathf.Clamp(layer, 1, 50);
            m_Ring = Mathf.Clamp(ring, 0, 10);
            m_Position = ((position % PositionsPerRing) + PositionsPerRing) % PositionsPerRing;
        }

        public static OmniverseCoordinate Prime => new OmniverseCoordinate(1, 0, 0);

        public override string ToString() => $"L{m_Layer:00}-R{m_Ring:00}-P{m_Position:000}";

        public static bool TryParse(string text, out OmniverseCoordinate coord)
        {
            coord = Prime;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim().ToUpperInvariant();
            string[] parts = text.Split('-');
            if (parts.Length != 3) return false;
            if (!parts[0].StartsWith("L") || !parts[1].StartsWith("R") || !parts[2].StartsWith("P")) return false;
            if (!int.TryParse(parts[0].Substring(1), out int layer)) return false;
            if (!int.TryParse(parts[1].Substring(1), out int ring)) return false;
            if (!int.TryParse(parts[2].Substring(1), out int position)) return false;
            coord = new OmniverseCoordinate(layer, ring, position);
            return true;
        }

        public static OmniverseCoordinate Parse(string text)
        {
            if (!TryParse(text, out var c)) throw new FormatException($"Invalid omniverse coordinate: '{text}'");
            return c;
        }

        /// <summary>Circular distance between two positions on the same ring (0..90).</summary>
        public int GetCircularDistance(OmniverseCoordinate other)
        {
            int diff = Mathf.Abs(m_Position - other.m_Position);
            return Mathf.Min(diff, PositionsPerRing - diff);
        }

        /// <summary>Position neighbours (wraparound). Same layer/ring.</summary>
        public List<OmniverseCoordinate> GetAdjacentUniverses()
        {
            var list = new List<OmniverseCoordinate>(2);
            int prev = (m_Position - 1 + PositionsPerRing) % PositionsPerRing;
            int next = (m_Position + 1) % PositionsPerRing;
            list.Add(new OmniverseCoordinate(m_Layer, m_Ring, prev));
            list.Add(new OmniverseCoordinate(m_Layer, m_Ring, next));
            return list;
        }

        /// <summary>Shortest path along the position ring from this to other (same layer/ring).</summary>
        public List<OmniverseCoordinate> GetShortestPath(OmniverseCoordinate other)
        {
            var path = new List<OmniverseCoordinate>();
            if (m_Layer != other.m_Layer || m_Ring != other.m_Ring)
            {
                // Cross-layer/ring: just endpoints (no interpolation defined).
                path.Add(this);
                if (!Equals(other)) path.Add(other);
                return path;
            }

            int dist = GetCircularDistance(other);
            int step = (other.m_Position - m_Position + PositionsPerRing) % PositionsPerRing;
            if (step > PositionsPerRing / 2) step -= PositionsPerRing; // go the other way

            int pos = m_Position;
            for (int i = 0; i <= Mathf.Abs(step); i++)
            {
                path.Add(new OmniverseCoordinate(m_Layer, m_Ring, pos));
                if (i < Mathf.Abs(step))
                    pos = (pos + (step >= 0 ? 1 : -1) + PositionsPerRing) % PositionsPerRing;
            }
            return path;
        }

        public bool Equals(OmniverseCoordinate other) =>
            m_Layer == other.m_Layer && m_Ring == other.m_Ring && m_Position == other.m_Position;
        public override bool Equals(object obj) => obj is OmniverseCoordinate c && Equals(c);
        public override int GetHashCode() => (m_Layer * 397) ^ (m_Ring * 31) ^ m_Position;
        public static bool operator ==(OmniverseCoordinate a, OmniverseCoordinate b) => a.Equals(b);
        public static bool operator !=(OmniverseCoordinate a, OmniverseCoordinate b) => !a.Equals(b);
    }
}