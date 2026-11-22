using System;
using UnityEngine;

namespace Portals.Core
{
    /// <summary>
    /// Lightweight, inspector-friendly identifier for a universe.
    /// Deliberately a value type so it can be used as a dictionary key and compared cheaply.
    /// </summary>
    [Serializable]
    public struct UniverseId : IEquatable<UniverseId>
    {
        [SerializeField] private string m_Value;

        public UniverseId(string value)
        {
            m_Value = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        /// <summary>The raw identifier. Empty means "no universe".</summary>
        public string Value => m_Value ?? string.Empty;

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public static UniverseId None => default;

        public bool Equals(UniverseId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is UniverseId other && Equals(other);

        public override int GetHashCode() =>
            string.IsNullOrEmpty(m_Value) ? 0 : StringComparer.Ordinal.GetHashCode(m_Value);

        public override string ToString() => IsValid ? Value : "None";

        public static bool operator ==(UniverseId left, UniverseId right) => left.Equals(right);

        public static bool operator !=(UniverseId left, UniverseId right) => !left.Equals(right);

        public static implicit operator UniverseId(string value) => new UniverseId(value);
    }
}
