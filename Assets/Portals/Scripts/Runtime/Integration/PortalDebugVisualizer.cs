using Portals.Core;
using UnityEngine;

namespace Portals.Integration
{
    /// <summary>
    /// Purely diagnostic overlay for portal authoring. Draws the opening ellipse, its
    /// local axes and a line to the configured partner. No gameplay logic lives here.
    /// Gizmos work in the Scene view; LineRenderers make the same information visible
    /// while playing (the Game view).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortalDebugVisualizer : MonoBehaviour
    {
        [SerializeField] private PortalEndpoint m_Portal;
        [SerializeField] private bool m_DrawGizmos = true;
        [SerializeField] private bool m_DrawRuntimeLines = true;
        [SerializeField] private Color m_RingColor = new Color(0.2f, 0.85f, 1f, 1f);
        [SerializeField] private Color m_ConnectionColor = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField, Min(0.1f)] private float m_AxisLength = 1f;
        [SerializeField, Range(8, 64)] private int m_RingSegments = 32;

        private LineRenderer m_RingLine;
        private LineRenderer m_ForwardLine;
        private LineRenderer m_UpLine;
        private LineRenderer m_ConnectionLine;

        private PortalEndpoint Portal => m_Portal != null ? m_Portal : (m_Portal = GetComponent<PortalEndpoint>());

        public void Configure(PortalEndpoint portal, Color ringColor)
        {
            m_Portal = portal;
            m_RingColor = ringColor;
        }

        /// <summary>
        /// Disables the runtime debug LineRenderers when a real PortalVisual is present.
        /// Scene-view gizmos remain available for authoring.
        /// </summary>
        public void SetRuntimeLinesVisible(bool visible)
        {
            m_DrawRuntimeLines = visible;
            if (visible) return;

            DestroyRuntimeLine(m_RingLine);
            DestroyRuntimeLine(m_ForwardLine);
            DestroyRuntimeLine(m_UpLine);
            DestroyRuntimeLine(m_ConnectionLine);
            m_RingLine = null;
            m_ForwardLine = null;
            m_UpLine = null;
            m_ConnectionLine = null;
        }

        private void Awake()
        {
            if (Portal == null) return;
            if (m_DrawRuntimeLines) CreateRuntimeLines();
        }

        private void OnDrawGizmos()
        {
            if (!m_DrawGizmos) return;

            PortalEndpoint portal = Portal;
            if (portal == null) return;

            Vector2 half = portal.OpeningHalfSize;
            Color previous = Gizmos.color;

            Gizmos.color = portal.IsActive ? m_RingColor : Color.gray;
            Vector3 previousPoint = portal.LocalToWorldPoint(new Vector3(half.x, 0f, 0f));
            for (int i = 1; i <= m_RingSegments; i++)
            {
                float angle = i / (float)m_RingSegments * Mathf.PI * 2f;
                Vector3 local = new Vector3(Mathf.Cos(angle) * half.x, Mathf.Sin(angle) * half.y, 0f);
                Vector3 point = portal.LocalToWorldPoint(local);
                Gizmos.DrawLine(previousPoint, point);
                previousPoint = point;
            }

            Vector3 centre = portal.Position;

            // Local +Z is the outward normal, +Y is opening up, +X is opening right.
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(centre, portal.Forward * m_AxisLength);
            Gizmos.color = Color.green;
            Gizmos.DrawRay(centre, portal.Up * m_AxisLength);
            Gizmos.color = Color.red;
            Gizmos.DrawRay(centre, portal.Rotation * Vector3.right * m_AxisLength);

            PortalEndpoint partner = portal.ConfiguredPartner;
            if (partner != null)
            {
                Gizmos.color = m_ConnectionColor;
                Gizmos.DrawLine(centre, partner.Position);
                Gizmos.DrawWireSphere(partner.Position, 0.25f);
            }

            Gizmos.color = previous;
        }

        private void LateUpdate()
        {
            if (!m_DrawRuntimeLines || Portal == null || m_RingLine == null) return;

            Vector2 half = Portal.OpeningHalfSize;
            Color ringColor = Portal.IsActive ? m_RingColor : Color.gray;

            for (int i = 0; i <= m_RingSegments; i++)
            {
                float angle = i / (float)m_RingSegments * Mathf.PI * 2f;
                Vector3 local = new Vector3(Mathf.Cos(angle) * half.x, Mathf.Sin(angle) * half.y, 0f);
                m_RingLine.SetPosition(i, Portal.LocalToWorldPoint(local));
            }
            m_RingLine.startColor = ringColor;
            m_RingLine.endColor = ringColor;

            SetAxis(m_ForwardLine, Portal.Position, Portal.Forward, Color.blue);
            SetAxis(m_UpLine, Portal.Position, Portal.Up, Color.green);

            PortalEndpoint partner = Portal.ConfiguredPartner;
            if (m_ConnectionLine != null)
            {
                bool show = partner != null;
                m_ConnectionLine.enabled = show;
                if (show)
                {
                    m_ConnectionLine.SetPosition(0, Portal.Position);
                    m_ConnectionLine.SetPosition(1, partner.Position);
                    m_ConnectionLine.startColor = m_ConnectionColor;
                    m_ConnectionLine.endColor = m_ConnectionColor;
                }
            }
        }

        private void SetAxis(LineRenderer line, Vector3 origin, Vector3 direction, Color color)
        {
            if (line == null) return;
            line.SetPosition(0, origin);
            line.SetPosition(1, origin + direction.normalized * m_AxisLength);
            line.startColor = color;
            line.endColor = color;
        }

        private void CreateRuntimeLines()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                m_DrawRuntimeLines = false;
                return;
            }

            m_RingLine = CreateLine("Debug Ring", m_RingSegments + 1, 0.035f, shader);
            m_ForwardLine = CreateLine("Debug Forward", 2, 0.04f, shader);
            m_UpLine = CreateLine("Debug Up", 2, 0.04f, shader);
            m_ConnectionLine = CreateLine("Debug Connection", 2, 0.025f, shader);
        }

        private LineRenderer CreateLine(string name, int pointCount, float width, Shader shader)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(transform, false);
            LineRenderer line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = pointCount;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.material = new Material(shader);
            return line;
        }

        private void OnDestroy()
        {
            // LineRenderer materials are created at runtime; avoid leaking them.
            DestroyRuntimeLine(m_RingLine);
            DestroyRuntimeLine(m_ForwardLine);
            DestroyRuntimeLine(m_UpLine);
            DestroyRuntimeLine(m_ConnectionLine);
        }

        private static void DestroyRuntimeLine(LineRenderer line)
        {
            if (line == null) return;
            if (line.sharedMaterial != null) Destroy(line.sharedMaterial);
            Destroy(line.gameObject);
        }
    }
}
