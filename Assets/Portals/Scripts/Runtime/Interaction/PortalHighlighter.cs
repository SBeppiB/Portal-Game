using UnityEngine;

namespace Portals.Interaction
{
    /// <summary>
    /// Casts a ray from the main camera and highlights compatible interactable objects
    /// by swapping their renderer materials to a temporary highlight instance. Original
    /// materials are restored when the target changes or becomes invalid, so nothing is
    /// permanently altered.
    /// </summary>
    [DisallowMultipleComponent]
    public class PortalHighlighter : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float m_Range = 5f;
        [SerializeField] private Color m_HighlightColor = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField, Min(0f)] private float m_HighlightEmission = 0.6f;
        [SerializeField, Tooltip("Own colliders are ignored so the player is never highlighted.")]
        private Transform m_IgnoreRoot;

        private readonly RaycastHit[] m_HitBuffer = new RaycastHit[16];

        private Camera m_Camera;
        private GameObject m_CurrentTarget;
        private Material[] m_OriginalMaterials;
        private Material[] m_HighlightMaterials;

        public float Range => m_Range;
        public GameObject CurrentTarget => m_CurrentTarget;

        private void Awake()
        {
            m_Camera = Camera.main;
            if (m_IgnoreRoot == null) m_IgnoreRoot = transform;
        }

        /// <summary>Re-evaluates the current target. Called every frame and by tests.</summary>
        public void Refresh()
        {
            if (m_Camera == null) m_Camera = Camera.main;
            if (m_Camera == null) return;

            Ray ray = new Ray(m_Camera.transform.position, m_Camera.transform.forward);
            int count = Physics.RaycastNonAlloc(ray, m_HitBuffer, m_Range, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            GameObject newTarget = null;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = m_HitBuffer[i];
                if (hit.collider == null || hit.distance >= nearest) continue;
                Transform candidate = hit.collider.transform;
                if (m_IgnoreRoot != null &&
                    (candidate == m_IgnoreRoot || candidate.IsChildOf(m_IgnoreRoot))) continue;

                InteractableObject interactable = hit.collider.GetComponentInParent<InteractableObject>();
                if (interactable != null)
                {
                    if (!interactable.PickupCompatible) continue;
                    newTarget = interactable.gameObject;
                }
                else if (hit.collider.GetComponentInParent<Rigidbody>() != null)
                {
                    // Any dynamic object is a valid default target even without metadata.
                    newTarget = hit.collider.GetComponentInParent<Rigidbody>().gameObject;
                }
                else continue;

                nearest = hit.distance;
            }

            if (newTarget == m_CurrentTarget) return;

            RestoreHighlight();
            m_CurrentTarget = newTarget;
            if (m_CurrentTarget != null) ApplyHighlight(m_CurrentTarget);
        }

        private void Update() => Refresh();

        private void ApplyHighlight(GameObject target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            int materialCount = 0;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) materialCount += renderers[i].sharedMaterials.Length;

            m_OriginalMaterials = new Material[materialCount];
            m_HighlightMaterials = new Material[materialCount];
            int index = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null) continue;
                Material[] shared = renderer.sharedMaterials;
                for (int j = 0; j < shared.Length; j++)
                {
                    Material original = shared[j];
                    m_OriginalMaterials[index] = original;
                    Material highlight = original != null
                        ? new Material(original)
                        : new Material(Shader.Find("Standard"));
                    if (highlight.HasProperty("_Color")) highlight.SetColor("_Color", m_HighlightColor);
                    if (highlight.HasProperty("_BaseColor")) highlight.SetColor("_BaseColor", m_HighlightColor);
                    if (highlight.HasProperty("_EmissionColor"))
                    {
                        highlight.EnableKeyword("_EMISSION");
                        highlight.SetColor("_EmissionColor", m_HighlightColor * m_HighlightEmission);
                    }
                    m_HighlightMaterials[index] = highlight;
                    shared[j] = highlight;
                    index++;
                }
                renderer.sharedMaterials = shared;
            }
        }

        private void RestoreHighlight()
        {
            if (m_CurrentTarget == null || m_OriginalMaterials == null) return;

            Renderer[] renderers = m_CurrentTarget.GetComponentsInChildren<Renderer>(true);
            int index = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null) continue;
                Material[] shared = renderer.sharedMaterials;
                for (int j = 0; j < shared.Length; j++)
                {
                    if (index < m_OriginalMaterials.Length) shared[j] = m_OriginalMaterials[index];
                    index++;
                }
                renderer.sharedMaterials = shared;
            }

            if (m_HighlightMaterials != null)
            {
                for (int i = 0; i < m_HighlightMaterials.Length; i++)
                {
                    if (m_HighlightMaterials[i] != null) Destroy(m_HighlightMaterials[i]);
                }
            }
            m_OriginalMaterials = null;
            m_HighlightMaterials = null;
        }

        private void OnDisable()
        {
            RestoreHighlight();
            m_CurrentTarget = null;
        }
    }
}
