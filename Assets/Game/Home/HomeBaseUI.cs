using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// Home-base readout: storage contents, discovered coordinates and tuner
    /// progression. Builds its own Canvas and Text objects when it has not been
    /// wired in the inspector, and only rebuilds the label strings when the
    /// underlying home state actually changes (no per-frame string rebuilding).
    /// </summary>
    [DisallowMultipleComponent]
    public class HomeBaseUI : MonoBehaviour
    {
        [SerializeField] private HomeBaseManager m_HomeBase;
        [SerializeField] private Text m_InventoryText;
        [SerializeField] private Text m_DiscoveredText;
        [SerializeField] private Text m_TunerText;

        private readonly StringBuilder m_Builder = new StringBuilder();
        private int m_LastVersion = int.MinValue;
        private Texture2D m_PanelTex;
        private Sprite m_PanelSprite;

        /// <summary>Finds an existing instance or creates a self-contained one.</summary>
        public static HomeBaseUI Create()
        {
            HomeBaseUI existing = FindFirstObjectByType<HomeBaseUI>();
            if (existing != null) return existing;

            var go = new GameObject("Home Base UI");
            return go.AddComponent<HomeBaseUI>();
        }

        private void Awake()
        {
            if (m_HomeBase == null) m_HomeBase = FindFirstObjectByType<HomeBaseManager>();
            EnsureUI();
            Refresh(force: true);
        }

        private void OnDestroy()
        {
            if (m_PanelSprite != null) Destroy(m_PanelSprite);
            if (m_PanelTex != null) Destroy(m_PanelTex);
        }

        private void Update()
        {
            if (m_HomeBase == null) return;
            Refresh(force: false);
        }

        private void Refresh(bool force)
        {
            if (m_HomeBase == null) return;
            int version = m_HomeBase.StateVersion;
            if (!force && version == m_LastVersion) return;
            m_LastVersion = version;

            if (m_InventoryText != null)
            {
                m_Builder.Clear();
                ResourceInventory storage = m_HomeBase.Storage;
                if (storage != null)
                {
                    foreach (ResourceStack stack in storage.Stacks)
                    {
                        if (stack == null || stack.resource == null) continue;
                        m_Builder.Append(stack.resource.DisplayName)
                                 .Append(": ")
                                 .Append(stack.amount)
                                 .Append('\n');
                    }
                    if (m_Builder.Length == 0) m_Builder.AppendLine("Storage empty.");
                }
                else
                {
                    m_Builder.AppendLine("Storage unavailable.");
                }
                m_InventoryText.text = m_Builder.ToString();
            }

            if (m_DiscoveredText != null)
            {
                m_Builder.Clear();
                m_Builder.Append("Discovered: ");
                var discovered = m_HomeBase.Discovered;
                if (discovered == null || discovered.Count == 0)
                {
                    m_Builder.Append("none");
                }
                else
                {
                    for (int i = 0; i < discovered.Count; i++)
                    {
                        if (i > 0) m_Builder.Append(", ");
                        m_Builder.Append(discovered[i]);
                    }
                }
                m_DiscoveredText.text = m_Builder.ToString();
            }

            if (m_TunerText != null)
            {
                PortalTuner tuner = m_HomeBase.Tuner;
                m_TunerText.text = tuner != null
                    ? $"Portal Tuner: Ring {tuner.MaxRing}, Layer {tuner.MaxLayer}"
                    : "Portal Tuner: unavailable";
            }
        }

        private void EnsureUI()
        {
            if (m_InventoryText != null && m_DiscoveredText != null && m_TunerText != null) return;

            var canvasGo = new GameObject("HomeBaseCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            m_PanelTex = MakeTexture(new Color(0.03f, 0.05f, 0.08f, 0.82f));
            m_PanelSprite = Sprite.Create(m_PanelTex,
                new Rect(0f, 0f, m_PanelTex.width, m_PanelTex.height), new Vector2(0.5f, 0.5f), 100f);

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panelRect = (RectTransform)panelGo.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.anchoredPosition = new Vector2(-20f, -20f);
            panelRect.sizeDelta = new Vector2(430f, 300f);

            var panelImage = panelGo.GetComponent<Image>();
            panelImage.sprite = m_PanelSprite;
            panelImage.type = Image.Type.Simple;
            panelImage.color = Color.white;

            m_InventoryText = CreateText(panelGo.transform, "InventoryText",
                new Vector2(16f, -48f), new Vector2(398f, 150f), 16, TextAnchor.UpperLeft);
            m_DiscoveredText = CreateText(panelGo.transform, "DiscoveredText",
                new Vector2(16f, -206f), new Vector2(398f, 40f), 14, TextAnchor.UpperLeft);
            m_TunerText = CreateText(panelGo.transform, "TunerText",
                new Vector2(16f, -252f), new Vector2(398f, 24f), 14, TextAnchor.UpperLeft);

            CreateText(panelGo.transform, "Title",
                new Vector2(16f, -12f), new Vector2(398f, 30f), 22, TextAnchor.UpperLeft).text = "Home Base";
        }

        private Text CreateText(Transform parent, string name, Vector2 anchoredPos, Vector2 size,
            int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var text = go.AddComponent<Text>();
            text.font = GetFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.88f, 0.92f, 0.98f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            return text;
        }

        private static Font GetFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                catch { font = null; }
            }
            return font;
        }

        private static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }
    }
}