using System;
using System.Collections.Generic;
using System.Linq;

namespace Portals.Runtime.Mechanics
{
    public class UniverseRegistry
    {
        protected readonly Dictionary<string, UniverseSignature> m_ByKey = new Dictionary<string, UniverseSignature>();
        protected readonly Dictionary<string, DiscoveryStatus> m_Discovery = new Dictionary<string, DiscoveryStatus>();

        public int Count => m_ByKey.Count;

        public static string MakeKey(OmniversalCoordinates coord) => coord.ToString() + (coord.IsPrimeAu ? ":P" : "");

        public void Register(UniverseSignature signature)
        {
            if (signature == null) return;
            string key = MakeKey(signature.Coordinates);
            if (m_ByKey.ContainsKey(key)) return;
            m_ByKey[key] = signature;
            if (!m_Discovery.ContainsKey(key) || m_Discovery[key] == DiscoveryStatus.Unknown)
                m_Discovery[key] = DiscoveryStatus.Registered;
        }

        public bool TryRegister(UniverseSignature signature)
        {
            if (signature == null) return false;
            string key = MakeKey(signature.Coordinates);
            if (m_ByKey.ContainsKey(key)) return false;
            m_ByKey[key] = signature;
            m_Discovery[key] = DiscoveryStatus.Registered;
            return true;
        }

        public void RegisterOrUpdate(UniverseSignature signature)
        {
            if (signature == null) return;
            string key = MakeKey(signature.Coordinates);
            m_ByKey[key] = signature;
            if (!m_Discovery.ContainsKey(key)) m_Discovery[key] = DiscoveryStatus.Registered;
        }

        public void Upsert(UniverseSignature signature) => RegisterOrUpdate(signature);
        public void RegisterSignature(UniverseSignature signature) => Register(signature);
        public void Add(UniverseSignature signature) => Register(signature);
        public bool AddIfNotExists(UniverseSignature signature) => TryRegister(signature);

        public bool Contains(OmniversalCoordinates coord) => m_ByKey.ContainsKey(MakeKey(coord));

        public bool Contains(UniverseSignature signature)
        {
            if (signature == null) return false;
            return m_ByKey.ContainsKey(MakeKey(signature.Coordinates));
        }

        public bool Contains(string signatureId)
        {
            if (string.IsNullOrEmpty(signatureId)) return false;
            return m_ByKey.Values.Any(s => string.Equals(s.Signature, signatureId, StringComparison.Ordinal) || string.Equals(s.SignatureId, signatureId, StringComparison.Ordinal) || string.Equals(s.Id, signatureId, StringComparison.Ordinal) || string.Equals(s.UniverseId, signatureId, StringComparison.Ordinal));
        }

        public bool TryGet(OmniversalCoordinates coord, out UniverseSignature signature)
        {
            return m_ByKey.TryGetValue(MakeKey(coord), out signature);
        }

        public bool TryGet(string signatureId, out UniverseSignature signature)
        {
            signature = null;
            if (string.IsNullOrEmpty(signatureId)) return false;
            foreach (var kvp in m_ByKey)
            {
                if (string.Equals(kvp.Value.Signature, signatureId, StringComparison.Ordinal) ||
                    string.Equals(kvp.Value.SignatureId, signatureId, StringComparison.Ordinal) ||
                    string.Equals(kvp.Value.Id, signatureId, StringComparison.Ordinal) ||
                    string.Equals(kvp.Value.UniverseId, signatureId, StringComparison.Ordinal))
                {
                    signature = kvp.Value;
                    return true;
                }
            }
            return false;
        }

        public bool TryGet(UniverseSignature signature, out UniverseSignature registered)
        {
            registered = null;
            if (signature == null) return false;
            return TryGet(signature.Coordinates, out registered);
        }

        public bool TryGetByCoordinates(OmniversalCoordinates coord, out UniverseSignature signature) => TryGet(coord, out signature);
        public bool TryGetBySignature(string signatureId, out UniverseSignature signature) => TryGet(signatureId, out signature);
        public bool TryGetById(string id, out UniverseSignature signature) => TryGet(id, out signature);

        public UniverseSignature Get(OmniversalCoordinates coord)
        {
            TryGet(coord, out var sig);
            return sig;
        }

        public UniverseSignature Get(string signatureId)
        {
            TryGet(signatureId, out var sig);
            return sig;
        }

        public UniverseSignature GetByCoordinates(OmniversalCoordinates coord) => Get(coord);
        public UniverseSignature GetBySignature(string signatureId) => Get(signatureId);
        public UniverseSignature GetBySignature(UniverseSignature signature)
        {
            if (signature == null) return null;
            return Get(signature.Coordinates);
        }
        public UniverseSignature GetById(string id) => Get(id);
        public UniverseSignature GetByCoords(OmniversalCoordinates coord) => Get(coord);
        public UniverseSignature GetUniverse(OmniversalCoordinates coord) => Get(coord);
        public UniverseSignature GetUniverse(string signatureId) => Get(signatureId);
        public UniverseSignature Find(OmniversalCoordinates coord) => Get(coord);
        public UniverseSignature Find(string signatureId) => Get(signatureId);
        public UniverseSignature FindBySignature(string signatureId) => Get(signatureId);
        public UniverseSignature FindByCoordinates(OmniversalCoordinates coord) => Get(coord);
        public UniverseSignature Lookup(OmniversalCoordinates coord) => Get(coord);
        public UniverseSignature Lookup(string signatureId) => Get(signatureId);

        public IReadOnlyList<UniverseSignature> GetAll()
        {
            return m_ByKey.Values.OrderBy(s => s.Signature, StringComparer.Ordinal)
                .ThenBy(s => s.UniverseId, StringComparer.Ordinal).ToList();
        }

        public IEnumerable<UniverseSignature> All => GetAll();
        public IReadOnlyList<UniverseSignature> Registered => GetAll();
        public IEnumerable<UniverseSignature> Entries => GetAll();
        public IEnumerable<UniverseSignature> Universes => GetAll();
        public IEnumerable<UniverseSignature> Items => GetAll();
        public IReadOnlyList<UniverseSignature> AllUniverses => GetAll();
        public IReadOnlyList<UniverseSignature> ListAll() => GetAll();
        public IReadOnlyList<UniverseSignature> GetAllRegistered() => GetAll();

        public bool Remove(OmniversalCoordinates coord)
        {
            string key = MakeKey(coord);
            m_Discovery.Remove(key);
            return m_ByKey.Remove(key);
        }

        public bool Remove(UniverseSignature signature)
        {
            if (signature == null) return false;
            return Remove(signature.Coordinates);
        }

        public bool Remove(string signatureId)
        {
            if (string.IsNullOrEmpty(signatureId)) return false;
            if (!TryGet(signatureId, out var sig) || sig == null) return false;
            return Remove(sig.Coordinates);
        }

        public void Clear()
        {
            m_ByKey.Clear();
            m_Discovery.Clear();
        }

        public bool IsRegistered(OmniversalCoordinates coord) => Contains(coord);
        public bool IsRegistered(UniverseSignature signature) => Contains(signature);
        public bool IsRegistered(string signatureId) => Contains(signatureId);
        public bool IsKnown(OmniversalCoordinates coord) => Contains(coord);
        public bool ContainsUniverse(OmniversalCoordinates coord) => Contains(coord);

        public DiscoveryStatus GetDiscoveryStatus(OmniversalCoordinates coord)
        {
            string key = MakeKey(coord);
            if (m_Discovery.TryGetValue(key, out var status)) return status;
            return m_ByKey.ContainsKey(key) ? DiscoveryStatus.Registered : DiscoveryStatus.Unknown;
        }

        public DiscoveryStatus GetDiscoveryStatus(UniverseSignature signature)
        {
            if (signature == null) return DiscoveryStatus.Unknown;
            return GetDiscoveryStatus(signature.Coordinates);
        }

        public DiscoveryStatus GetDiscoveryStatus(string signatureId)
        {
            if (string.IsNullOrEmpty(signatureId)) return DiscoveryStatus.Unknown;
            if (TryGet(signatureId, out var sig) && sig != null)
                return GetDiscoveryStatus(sig.Coordinates);
            return DiscoveryStatus.Unknown;
        }

        public void SetDiscoveryStatus(OmniversalCoordinates coord, DiscoveryStatus status)
        {
            m_Discovery[MakeKey(coord)] = status;
        }

        public void SetDiscoveryStatus(UniverseSignature signature, DiscoveryStatus status)
        {
            if (signature == null) return;
            SetDiscoveryStatus(signature.Coordinates, status);
        }

        public void SetDiscoveryStatus(string signatureId, DiscoveryStatus status)
        {
            if (TryGet(signatureId, out var sig) && sig != null)
                SetDiscoveryStatus(sig.Coordinates, status);
        }

        public bool IsDiscovered(OmniversalCoordinates coord)
        {
            var s = GetDiscoveryStatus(coord);
            return s == DiscoveryStatus.Discovered || s == DiscoveryStatus.Registered || s == DiscoveryStatus.Cached;
        }

        public void MarkDiscovered(UniverseSignature signature)
        {
            if (signature == null) return;
            Register(signature);
            SetDiscoveryStatus(signature.Coordinates, DiscoveryStatus.Discovered);
        }

        public void MarkRegistered(UniverseSignature signature)
        {
            if (signature == null) return;
            Register(signature);
            SetDiscoveryStatus(signature.Coordinates, DiscoveryStatus.Registered);
        }

        public void MarkPending(OmniversalCoordinates coord) => SetDiscoveryStatus(coord, DiscoveryStatus.Pending);
        public void MarkFailed(OmniversalCoordinates coord) => SetDiscoveryStatus(coord, DiscoveryStatus.Failed);

        public IReadOnlyList<UniverseSignature> GetDiscovered()
        {
            var result = new List<UniverseSignature>();
            foreach (var sig in GetAll())
            {
                if (IsDiscovered(sig.Coordinates)) result.Add(sig);
            }
            return result;
        }

        public IReadOnlyList<UniverseSignature> GetByDiscoveryStatus(DiscoveryStatus status)
        {
            var result = new List<UniverseSignature>();
            foreach (var sig in GetAll())
            {
                if (GetDiscoveryStatus(sig.Coordinates) == status) result.Add(sig);
            }
            return result;
        }

        // Aliases.
        public DiscoveryStatus GetState(OmniversalCoordinates coord) => GetDiscoveryStatus(coord);
        public DiscoveryStatus GetDiscoveryState(OmniversalCoordinates coord) => GetDiscoveryStatus(coord);
        public DiscoveryStatus GetState(UniverseSignature signature) => GetDiscoveryStatus(signature);
        public DiscoveryStatus GetState(string signatureId) => GetDiscoveryStatus(signatureId);
    }
}
