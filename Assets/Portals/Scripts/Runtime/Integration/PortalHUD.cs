using UnityEngine;

namespace Portals.Integration
{
    /// <summary>
    /// Minimal IMGUI overlay showing travel mode, selected dimension, gun state and the
    /// next portal to be fired. Reads controller state every OnGUI call so changes are
    /// reflected immediately, without requiring a Canvas or any animation/effects.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortalHUD : MonoBehaviour
    {
        [SerializeField] private PortalFiringController m_Controller;
        [SerializeField] private bool m_ShowHelpLine = true;

        private GUIStyle m_Panel;
        private GUIStyle m_Mode;
        private GUIStyle m_Label;

        private void Awake()
        {
            if (m_Controller == null) m_Controller = GetComponent<PortalFiringController>();
            if (m_Controller == null) m_Controller = FindFirstObjectByType<PortalFiringController>();
        }

        private void EnsureStyles()
        {
            if (m_Mode != null) return;

            m_Panel = new GUIStyle(GUI.skin.box);
            m_Panel.normal.background = MakeTexture(new Color(0.05f, 0.05f, 0.08f, 0.78f));
            m_Panel.padding = new RectOffset(14, 14, 10, 12);

            m_Mode = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.4f, 0.9f, 1f) }
            };

            m_Label = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                normal = { textColor = Color.white }
            };
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void OnGUI()
        {
            if (m_Controller == null) return;
            EnsureStyles();

            Rect panel = new Rect(16f, 16f, 340f, m_ShowHelpLine ? 180f : 170f);
            GUI.Box(panel, GUIContent.none, m_Panel);

            float y = panel.y + 8f;
            GUI.Label(new Rect(panel.x + 12f, y, panel.width - 24f, 34f),
                m_Controller.TravelModeLabel, m_Mode);
            y += 34f;

            GUI.Label(new Rect(panel.x + 12f, y, panel.width - 24f, 22f),
                $"Dimension: {m_Controller.SelectedDimensionIndex + 1} - {m_Controller.SelectedDimensionName}", m_Label);
            y += 22f;

            GUI.Label(new Rect(panel.x + 12f, y, panel.width - 24f, 22f),
                $"Gun: {(m_Controller.IsEquipped ? "EQUIPPED" : "HIDDEN")}", m_Label);
            y += 22f;

            GUI.Label(new Rect(panel.x + 12f, y, panel.width - 24f, 22f),
                $"Next portal: {m_Controller.SelectedPortalLabel}", m_Label);
            y += 22f;

            GUI.Label(new Rect(panel.x + 12f, y, panel.width - 24f, 22f),
                m_Controller.PairLabel, m_Label);
            y += 22f;

            GUI.Label(new Rect(panel.x + 12f, y, panel.width - 24f, 22f),
                $"Lifetime: {m_Controller.LifetimeLabel}", m_Label);
            y += 24f;

            if (GUI.Button(new Rect(panel.x + 12f, y, 112f, 24f), "Close All"))
                m_Controller.CloseAllPortals();
            y += 28f;

            if (m_ShowHelpLine)
            {
                GUI.Label(new Rect(panel.x + 12f, y, panel.width - 24f, 22f),
                    "T gun  M mode  1-9 dim  N hold  B close  LMB fire", m_Label);
            }
        }

        private void OnDestroy()
        {
            if (m_Panel != null && m_Panel.normal.background != null)
                Destroy(m_Panel.normal.background);
        }
    }
}
