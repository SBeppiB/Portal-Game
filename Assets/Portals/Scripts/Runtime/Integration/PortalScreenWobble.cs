using System.Collections;
using Portals.Core;
using Portals.Traversal;
using UnityEngine;

namespace Portals.Integration
{
    /// <summary>
    /// Brief, configurable screen-space wobble played when the player traverses a portal.
    /// It only touches the camera field of view and a transient overlay; it never moves
    /// the player, changes rotation or affects traversal. No camera shake or particles.
    /// </summary>
    [DisallowMultipleComponent]
    public class PortalScreenWobble : MonoBehaviour
    {
        [Header("Effect")]
        [SerializeField] private bool m_WobbleEnabled = true;
        [SerializeField, Range(0f, 1f)] private float m_Intensity = 0.35f;
        [SerializeField, Min(0.05f)] private float m_Duration = 0.25f;

        [Header("References")]
        [SerializeField] private Camera m_Camera;
        [SerializeField] private PortalTraverser m_Traverser;

        private float m_BaseFov;
        private float m_Timer;
        private bool m_Active;
        private Texture2D m_Overlay;

        public bool Enabled => m_WobbleEnabled;
        public float Intensity => m_Intensity;
        public float Duration => m_Duration;
        public bool IsActive => m_Active;

        private void Awake()
        {
            if (m_Camera == null) m_Camera = Camera.main;
            if (m_Traverser == null) m_Traverser = GetComponent<PortalTraverser>();

            m_Overlay = new Texture2D(1, 1);
            m_Overlay.SetPixel(0, 0, Color.white);
            m_Overlay.Apply();
        }

        private void OnEnable()
        {
            if (m_Traverser != null) m_Traverser.Traversed += OnTraversed;
        }

        private void OnDisable()
        {
            if (m_Traverser != null) m_Traverser.Traversed -= OnTraversed;
            if (m_Active) StopEffect();
        }

        private void OnDestroy()
        {
            if (m_Overlay != null) Destroy(m_Overlay);
        }

        private void OnTraversed(PortalTraversalInfo info)
        {
            if (!m_WobbleEnabled) return;
            Trigger();
        }

        /// <summary>Starts the wobble. Exposed for tests and scripted use.</summary>
        public void Configure(bool enabled, float intensity, float duration)
        {
            m_WobbleEnabled = enabled;
            m_Intensity = Mathf.Clamp01(intensity);
            m_Duration = Mathf.Max(0.05f, duration);
        }

        public void Trigger()
        {
            if (!m_WobbleEnabled) return;
            if (m_Camera == null) m_Camera = Camera.main;
            if (m_Camera == null) return;

            if (m_BaseFov <= 0f) m_BaseFov = m_Camera.fieldOfView;
            m_Timer = 0f;
            m_Active = true;
        }

        private void Update()
        {
            if (!m_Active) return;

            if (m_Camera == null) { m_Active = false; return; }
            if (m_BaseFov <= 0f) m_BaseFov = m_Camera.fieldOfView;

            m_Timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(m_Timer / m_Duration);
            float envelope = Mathf.Sin(t * Mathf.PI);

            float fovOffset = m_Intensity * 8f * envelope;
            m_Camera.fieldOfView = m_BaseFov + fovOffset;

            if (t >= 1f) StopEffect();
        }

        private void StopEffect()
        {
            m_Active = false;
            m_Timer = 0f;
            if (m_Camera != null && m_BaseFov > 0f) m_Camera.fieldOfView = m_BaseFov;
        }

        private void OnGUI()
        {
            if (!m_Active || m_Overlay == null || m_Camera == null) return;

            float t = Mathf.Clamp01(m_Timer / m_Duration);
            float envelope = Mathf.Sin(t * Mathf.PI);
            float alpha = m_Intensity * 0.25f * envelope;

            Color previous = GUI.color;
            GUI.color = new Color(0.2f, 0.5f, 1f, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), m_Overlay);
            GUI.color = previous;
        }
    }
}
