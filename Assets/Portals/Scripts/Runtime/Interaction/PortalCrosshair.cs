using UnityEngine;

namespace Portals.Interaction
{
    /// <summary>
    /// Minimal centred crosshair drawn with IMGUI. Has no gameplay logic.
    /// </summary>
    [DisallowMultipleComponent]
    public class PortalCrosshair : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float m_Size = 10f;
        [SerializeField, Min(1f)] private float m_Thickness = 2f;
        [SerializeField] private Color m_Color = Color.white;

        private Texture2D m_Texture;

        private void Awake()
        {
            m_Texture = new Texture2D(1, 1);
            m_Texture.SetPixel(0, 0, Color.white);
            m_Texture.Apply();
        }

        private void OnGUI()
        {
            if (m_Texture == null) return;

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            GUI.color = m_Color;
            GUI.DrawTexture(new Rect(cx - m_Size, cy - m_Thickness * 0.5f, m_Size * 2f, m_Thickness), m_Texture);
            GUI.DrawTexture(new Rect(cx - m_Thickness * 0.5f, cy - m_Size, m_Thickness, m_Size * 2f), m_Texture);
            GUI.color = Color.white;
        }

        private void OnDestroy()
        {
            if (m_Texture != null) Destroy(m_Texture);
        }
    }
}
