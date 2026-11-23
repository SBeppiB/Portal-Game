using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Portals.Core;
using Portals.Game;

namespace Game
{
    /// <summary>
    /// Universe selection panel. Builds its own Screen Space Overlay Canvas with
    /// layer / ring / position inputs and a Travel button when it has not been
    /// wired in the inspector. Drives the transition through
    /// <see cref="UniverseManager"/> and the linked <see cref="InterversalPortal"/>.
    /// Toggled with <c>U</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public class UniverseSelectionUI : MonoBehaviour
    {
        [SerializeField] private PortalTuner m_Tuner;
        [SerializeField] private UniverseManager m_UniverseManager;
        [SerializeField] private InterversalPortal m_TargetPortal;

        [SerializeField] private InputField m_LayerInput;
        [SerializeField] private InputField m_RingInput;
        [SerializeField] private InputField m_PositionInput;
        [SerializeField] private Text m_CoordinateLabel;
        [SerializeField] private Text m_StatusLabel;

        private GameObject m_CanvasGo;
        private bool m_Open;
        private bool m_CursorOwned;

        public bool IsOpen => m_Open;

        /// <summary>Finds an existing instance or creates a self-contained one.</summary>
        public static UniverseSelectionUI Create()
        {
            UniverseSelectionUI existing = FindFirstObjectByType<UniverseSelectionUI>();
            if (existing != null) return existing;

            var go = new GameObject("Universe Selection UI");
            return go.AddComponent<UniverseSelectionUI>();
        }

        private void Awake()
        {
            ResolveReferences();
            EnsureUI();
            SetOpen(false);
        }

        private void OnDestroy()
        {
            if (m_Open) GameplayInput.PopUiBlocker();
            if (m_CursorOwned) RestoreCursor();
        }

        private void Update()
        {
            if (PauseManager.IsPaused)
            {
                if (m_Open) SetOpen(false);
                return;
            }

            if (Input.GetKeyDown(KeyCode.U))
                SetOpen(!m_Open);
        }

        private void ResolveReferences()
        {
            if (m_Tuner == null) m_Tuner = FindFirstObjectByType<PortalTuner>();
            if (m_UniverseManager == null) m_UniverseManager = FindFirstObjectByType<UniverseManager>();
            if (m_TargetPortal == null) m_TargetPortal = FindFirstObjectByType<InterversalPortal>();
        }

        /// <summary>Opens or closes the panel and takes/releases the cursor.</summary>
        public void SetOpen(bool open)
        {
            if (m_Open == open) return;

            if (open)
            {
                // Only one gameplay panel owns the cursor at a time.
                CraftingUI other = FindFirstObjectByType<CraftingUI>();
                if (other != null && other.IsOpen) other.SetOpen(false);
            }

            m_Open = open;

            if (m_CanvasGo != null) m_CanvasGo.SetActive(open);

            if (open)
            {
                GameplayInput.PushUiBlocker();
                AcquireCursor();
            }
            else
            {
                GameplayInput.PopUiBlocker();
                if (m_CursorOwned) RestoreCursor();
            }
        }

        private void AcquireCursor()
        {
            if (m_CursorOwned) return;
            m_CursorOwned = true;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void RestoreCursor()
        {
            m_CursorOwned = false;
            if (PauseManager.IsPaused) return;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        /// <summary>Reads the input fields and applies the selection.</summary>
        public void ApplySelection()
        {
            int layer = Parse(m_LayerInput, 0);
            int ring = Parse(m_RingInput, 0);
            int position = Parse(m_PositionInput, 0);
            TrySelect(layer, ring, position);
        }

        /// <summary>
        /// Validates access, configures the linked portal and asks the universe
        /// manager to transition. Returns false when the tuner locks the target.
        /// </summary>
        public bool TrySelect(int layer, int ring, int position)
        {
            ResolveReferences();

            var coord = new UniverseCoordinate(layer, ring, position);
            string coordStr = coord.ToString();

            if (m_CoordinateLabel != null) m_CoordinateLabel.text = "Destination: " + coordStr;

            if (m_Tuner != null && !m_Tuner.CanAccess(coord.Ring, coord.Layer))
            {
                SetStatus("Locked: upgrade the Portal Tuner.");
                return false;
            }

            UniverseId target = new UniverseId(coordStr);

            if (m_TargetPortal != null)
            {
                var linked = m_TargetPortal.GetLinkedEndpoint() as InterversalPortal;
                m_TargetPortal.Configure(target, linked,
                    m_TargetPortal.MappingMode, m_TargetPortal.WorldOffset);
            }

            // Keep every registered interversal portal pointing at the chosen
            // universe so walking through any placed portal travels there.
            foreach (InterversalPortal portal in InterversalPortalSystem.AllPortals)
            {
                if (portal == null || portal == m_TargetPortal) continue;
                var linked = portal.GetLinkedEndpoint() as InterversalPortal;
                portal.Configure(target, linked, portal.MappingMode, portal.WorldOffset);
            }

            if (m_UniverseManager != null) m_UniverseManager.EnterUniverse(coordStr);

            SetStatus("Destination set: " + coordStr);
            return true;
        }

        private void SetStatus(string message)
        {
            if (m_StatusLabel != null) m_StatusLabel.text = message;
        }

        private static int Parse(InputField field, int fallback)
        {
            return field != null && int.TryParse(field.text, out int value) ? value : fallback;
        }

        private void EnsureUI()
        {
            if (m_CanvasGo != null) return;

            m_CanvasGo = new GameObject("UniverseCanvas", typeof(RectTransform));
            m_CanvasGo.transform.SetParent(transform, false);

            var canvas = m_CanvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;

            var scaler = m_CanvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            m_CanvasGo.AddComponent<GraphicRaycaster>();

            EnsureEventSystem();

            RectTransform panel = CreatePanel(m_CanvasGo.transform, "Panel", new Vector2(620f, 430f));

            CreateLabel(panel, "Title", "Universe Selection",
                new Vector2(24f, -16f), new Vector2(572f, 34f), 26, TextAnchor.UpperLeft, FontStyle.Bold);
            CreateLabel(panel, "Caption", "Layer / Ring / Position   (Layer 0-9, Ring 0-10, Position 0-999)",
                new Vector2(24f, -52f), new Vector2(572f, 24f), 14, TextAnchor.UpperLeft, FontStyle.Normal);

            if (m_LayerInput == null)
                m_LayerInput = CreateInput(panel, "LayerInput", "Layer", new Vector2(24f, -96f));
            if (m_RingInput == null)
                m_RingInput = CreateInput(panel, "RingInput", "Ring", new Vector2(24f, -140f));
            if (m_PositionInput == null)
                m_PositionInput = CreateInput(panel, "PositionInput", "Position", new Vector2(24f, -184f));

            if (m_CoordinateLabel == null)
                m_CoordinateLabel = CreateLabel(panel, "CoordinateLabel", "Destination: L00-R00-P000",
                    new Vector2(24f, -232f), new Vector2(572f, 28f), 16, TextAnchor.UpperLeft, FontStyle.Normal);

            if (m_StatusLabel == null)
                m_StatusLabel = CreateLabel(panel, "StatusLabel", "",
                    new Vector2(24f, -264f), new Vector2(572f, 28f), 15, TextAnchor.UpperLeft, FontStyle.Normal);

            var apply = CreateButton(panel, "ApplyButton", "Travel",
                new Vector2(24f, -308f), new Vector2(572f, 44f));
            apply.onClick.AddListener(ApplySelection);

            m_Open = false;
            m_CanvasGo.SetActive(false);
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static RectTransform CreatePanel(Transform parent, string name, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.color = new Color(0.035f, 0.05f, 0.08f, 0.94f);
            return rect;
        }

        private static Text CreateLabel(Transform parent, string name, string content, Vector2 anchoredPos,
            Vector2 size, int fontSize, TextAnchor alignment, FontStyle fontStyle)
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
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = new Color(0.88f, 0.92f, 0.98f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            return text;
        }

        private static InputField CreateInput(Transform parent, string name, string label, Vector2 anchoredPos)
        {
            CreateLabel(parent, name + "Label", label,
                anchoredPos, new Vector2(100f, 34f), 18, TextAnchor.MiddleLeft, FontStyle.Normal);

            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPos + new Vector2(108f, 0f);
            rect.sizeDelta = new Vector2(488f, 34f);

            var background = go.GetComponent<Image>();
            background.color = new Color(0.08f, 0.1f, 0.14f, 0.95f);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 0f);
            textRect.offsetMax = new Vector2(-10f, 0f);

            var text = textGo.AddComponent<Text>();
            text.font = GetFont();
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;
            text.color = Color.white;

            var input = go.AddComponent<InputField>();
            input.textComponent = text;
            input.targetGraphic = background;
            input.lineType = InputField.LineType.SingleLine;
            input.text = "0";
            return input;
        }

        private static Button CreateButton(Transform parent, string name, string content, Vector2 anchoredPos,
            Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.color = new Color(0.95f, 0.45f, 0.08f, 0.95f);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGo.AddComponent<Text>();
            text.font = GetFont();
            text.text = content;
            text.fontSize = 20;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.black;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            return button;
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
    }
}