using UnityEngine;

namespace Portals.Integration
{
    /// <summary>
    /// A small, solid-blue puddle left on the floor when a portal finishes closing.
    /// It is purely decorative: no collider, Ignore Raycast layer, and no gameplay hooks.
    /// The puddle shrinks and fades out over exactly <see cref="Lifetime"/> seconds;
    /// every closure spawns an independent puddle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortalGooResidue : MonoBehaviour
    {
        /// <summary>Exact decay time, in seconds.</summary>
        public const float Lifetime = 5f;

        [SerializeField, Min(0.05f)] private float m_Radius = 0.35f;
        [SerializeField, Range(8, 48)] private int m_Segments = 20;

        private Mesh m_Mesh;
        private Material m_Material;
        private float m_Elapsed;
        private bool m_Destroyed;

        public float Duration => Lifetime;
        public float Elapsed => m_Elapsed;
        public bool IsDecaying => !m_Destroyed && m_DecayEnabled;
        public Material GooMaterial => m_Material;

        private bool m_DecayEnabled;

        /// <summary>
        /// Finds the floor beneath a closed portal with a downward raycast and spawns a
        /// puddle aligned to that floor. Returns null when no suitable floor exists.
        /// </summary>
        public static PortalGooResidue SpawnUnder(Vector3 portalPosition) =>
            SpawnUnder(portalPosition, ~0, 2f, 100f);

        public static PortalGooResidue SpawnUnder(
            Vector3 portalPosition, LayerMask floorLayers, float originHeight, float maxDistance)
        {
            Vector3 origin = portalPosition + Vector3.up * Mathf.Max(0f, originHeight);
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDistance,
                    floorLayers, QueryTriggerInteraction.Ignore))
                return null;

            // Only near-horizontal surfaces count as a floor.
            if (Vector3.Dot(hit.normal, Vector3.up) < 0.5f) return null;

            GameObject puddle = new GameObject("Portal Goo Residue");
            puddle.layer = 2; // Ignore Raycast - goo must never affect gameplay queries.
            puddle.transform.SetPositionAndRotation(
                hit.point + hit.normal * 0.01f,
                Quaternion.FromToRotation(Vector3.up, hit.normal));

            PortalGooResidue residue = puddle.AddComponent<PortalGooResidue>();
            residue.Initialize();
            return residue;
        }

        private void Initialize()
        {
            m_Elapsed = 0f;
            m_Destroyed = false;
            m_DecayEnabled = true;

            m_Mesh = BuildPuddleMesh(Mathf.Max(0.05f, m_Radius), Mathf.Max(8, m_Segments));
            MeshFilter filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = m_Mesh;

            MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            m_Material = CreateMaterial();
            renderer.sharedMaterial = m_Material;
            ApplyDecay(0f);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// Advances the decay by deltaTime seconds. Called from Update in normal play and
        /// directly by tests so the exact five-second behaviour can be verified.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!m_DecayEnabled || m_Destroyed || deltaTime <= 0f) return;

            m_Elapsed += deltaTime;
            float t = Lifetime <= 0f ? 1f : Mathf.Clamp01(m_Elapsed / Lifetime);
            ApplyDecay(t);

            if (m_Elapsed < Lifetime) return;

            m_Destroyed = true;
            m_DecayEnabled = false;
            Destroy(gameObject);
        }

        private void ApplyDecay(float t)
        {
            float scale = Mathf.Max(0f, 1f - t);
            transform.localScale = new Vector3(scale, scale, scale);

            if (m_Material == null) return;
            Color blue = new Color(0.08f, 0.45f, 1f, Mathf.Clamp01(1f - t));
            if (m_Material.HasProperty("_BaseColor")) m_Material.SetColor("_BaseColor", blue);
            if (m_Material.HasProperty("_Color")) m_Material.SetColor("_Color", blue);
        }

        private static Material CreateMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            Material material = new Material(shader);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return material;
        }

        private static Mesh BuildPuddleMesh(float radius, int segments)
        {
            int count = Mathf.Max(8, segments);
            Vector3[] vertices = new Vector3[count + 1];
            int[] triangles = new int[count * 3];

            vertices[0] = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                float angle = i / (float)count * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % count + 1;
            }

            Mesh mesh = new Mesh { name = "Portal Goo Puddle" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            if (m_Mesh != null) Destroy(m_Mesh);
            if (m_Material != null) Destroy(m_Material);
        }
    }
}
