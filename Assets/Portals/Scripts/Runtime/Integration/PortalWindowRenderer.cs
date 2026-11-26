using System;
using System.Collections.Generic;
using Portals.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Portals.Integration
{
    /// <summary>
    /// Renders an actual view into a linked portal destination using a secondary camera
    /// and a RenderTexture assigned to the portal surface material. Supports moving portals
    /// and recursive render limits.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortalWindowRenderer : MonoBehaviour
    {
        [SerializeField] private PortalEndpoint m_SourcePortal;
        [SerializeField] private PortalEndpoint m_DestinationPortal;
        [SerializeField, Range(0.1f, 2f)] private float m_ResolutionScale = 0.5f;
        [SerializeField, Min(64)] private int m_MinResolution = 256;
        [SerializeField, Min(1)] private int m_MaxRecursion = 1;
        [SerializeField] private Camera m_MainCamera;
        [SerializeField] private LayerMask m_CullingMask = ~0;

        private Camera m_PortalCamera;
        private RenderTexture m_RenderTexture;
        private PortalVisual m_Visual;
        private int m_RecursionDepth;
        private bool m_Initialized;
        private MeshRenderer m_SurfaceRenderer;
        private Material m_SurfaceMaterial;

        // Static guard across all portal renderers to prevent recursive rendering:
        // when portal A renders portal B's view, portal B must not render portal A
        // back into the same frame.
        private static readonly HashSet<PortalEndpoint> s_RenderingEndpoints = new HashSet<PortalEndpoint>();

        public PortalEndpoint SourcePortal => m_SourcePortal;
        public PortalEndpoint DestinationPortal => m_DestinationPortal;
        public RenderTexture RenderTexture => m_RenderTexture;

        private void Awake()
        {
            if (m_SourcePortal == null) m_SourcePortal = GetComponent<PortalEndpoint>();
            if (m_MainCamera == null) m_MainCamera = Camera.main;
            m_Visual = GetComponent<PortalVisual>();
            EnsureInitialized();
        }

        private void OnEnable()
        {
            if (m_SourcePortal == null) m_SourcePortal = GetComponent<PortalEndpoint>();
            if (m_MainCamera == null) m_MainCamera = Camera.main;
            m_Visual = GetComponent<PortalVisual>();
            if (m_Visual != null && m_Visual.SurfaceObject != null)
                m_SurfaceRenderer = m_Visual.SurfaceObject.GetComponent<MeshRenderer>();
        }

        private void OnDisable()
        {
            // Reset the surface material so the swirl fallback shows when not rendering.
            if (m_SurfaceRenderer != null && m_SurfaceRenderer.sharedMaterial != null)
                m_SurfaceRenderer.sharedMaterial.SetFloat("_HasPortalView", 0f);
        }

        private void OnDestroy()
        {
            if (m_RenderTexture != null)
            {
                m_RenderTexture.Release();
                Destroy(m_RenderTexture);
            }
            if (m_PortalCamera != null) Destroy(m_PortalCamera.gameObject);
        }

        public void Configure(PortalEndpoint source, PortalEndpoint destination)
        {
            m_SourcePortal = source;
            m_DestinationPortal = destination;
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (m_Initialized) return;
            m_Initialized = true;

            if (m_SourcePortal == null) m_SourcePortal = GetComponent<PortalEndpoint>();

            GameObject camObj = new GameObject("Portal Camera");
            camObj.transform.SetParent(transform, false);
            camObj.hideFlags = HideFlags.HideAndDontSave;
            m_PortalCamera = camObj.AddComponent<Camera>();
            m_PortalCamera.enabled = false;
            m_PortalCamera.clearFlags = CameraClearFlags.Skybox;
            m_PortalCamera.cullingMask = m_CullingMask;
            m_PortalCamera.nearClipPlane = 0.01f;
            m_PortalCamera.depth = -10;

            CreateRenderTexture();
        }

        private void CreateRenderTexture()
        {
            int size = ComputeResolution();
            if (m_RenderTexture != null && m_RenderTexture.width == size) return;
            if (m_RenderTexture != null)
            {
                m_RenderTexture.Release();
                Destroy(m_RenderTexture);
            }

            if (!SystemInfo.supportsRenderTextures)
            {
                Debug.LogWarning("[PortalWindowRenderer] RenderTextures not supported on this platform; " +
                    "falling back to swirl-only rendering.");
                m_RenderTexture = null;
                return;
            }

    m_RenderTexture = new RenderTexture(size, size, 24);
    m_RenderTexture.name = "PortalViewRT";

    bool created = m_RenderTexture.Create();
    if (!created)
    {
        Destroy(m_RenderTexture);
        m_RenderTexture = null;
        Debug.LogWarning("[PortalWindowRenderer] RenderTexture creation failed; " +
            "falling back to swirl-only rendering.");
        return;
    }

    m_PortalCamera.targetTexture = m_RenderTexture;
        }

        private int ComputeResolution()
        {
            int baseSize = m_MainCamera != null ? Mathf.Max(m_MinResolution, (int)(Screen.width * m_ResolutionScale)) : m_MinResolution;
            return Mathf.Clamp(baseSize, m_MinResolution, 2048);
        }

        private void LateUpdate()
        {
            if (m_SourcePortal == null || m_DestinationPortal == null || m_PortalCamera == null) return;
            if (m_MainCamera == null) m_MainCamera = Camera.main;
            if (m_MainCamera == null) return;

            // In headless/batch mode there is no GPU to render portal views; fall back to swirl.
            if (Application.isBatchMode) return;

            // Prevent recursive rendering: if our destination is currently rendering
            // its own view (which includes us), skip this frame to break the loop.
            if (s_RenderingEndpoints.Contains(m_DestinationPortal)) return;

            CreateRenderTexture();

            if (m_RenderTexture == null || m_PortalCamera.targetTexture == null) return;

            // Transform the main camera through the portal pair.
            Matrix4x4 entrance = m_SourcePortal.transform.localToWorldMatrix;
            Matrix4x4 exit = m_DestinationPortal.transform.localToWorldMatrix;
            // 180-degree flip about portal normal.
            Matrix4x4 flip = Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f));
            Matrix4x4 portalMatrix = exit * flip * entrance.inverse;

            Vector3 camPos = portalMatrix.MultiplyPoint3x4(m_MainCamera.transform.position);
            Quaternion camRot = portalMatrix.rotation * m_MainCamera.transform.rotation;

            m_PortalCamera.transform.SetPositionAndRotation(camPos, camRot);
            m_PortalCamera.fieldOfView = m_MainCamera.fieldOfView;
            m_PortalCamera.aspect = m_MainCamera.aspect;
            m_PortalCamera.nearClipPlane = m_MainCamera.nearClipPlane;
            m_PortalCamera.farClipPlane = m_MainCamera.farClipPlane;

            // Oblique clip plane at destination portal plane.
            Vector3 normal = m_DestinationPortal.Forward;
            Vector3 point = m_DestinationPortal.Position;
            Vector4 clipPlane = new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(normal, point));
            m_PortalCamera.projectionMatrix = m_PortalCamera.CalculateObliqueMatrix(clipPlane);

            s_RenderingEndpoints.Add(m_SourcePortal);
            m_PortalCamera.Render();
            s_RenderingEndpoints.Remove(m_SourcePortal);

            // Assign texture to surface material.
            if (m_SurfaceRenderer == null && m_Visual != null && m_Visual.SurfaceObject != null)
                m_SurfaceRenderer = m_Visual.SurfaceObject.GetComponent<MeshRenderer>();
            if (m_SurfaceRenderer != null && m_SurfaceRenderer.sharedMaterial != null)
            {
                m_SurfaceMaterial = m_SurfaceRenderer.sharedMaterial;
                m_SurfaceMaterial.SetTexture("_PortalViewTex", m_RenderTexture);
                m_SurfaceMaterial.SetFloat("_HasPortalView", 1f);
            }
        }
    }
}
