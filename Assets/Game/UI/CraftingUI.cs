using UnityEngine;
using Portals.Core;
using Portals.Game;

namespace Game
{
    /// <summary>
    /// Player-facing IMGUI crafting interface. Toggled with <c>R</c>. Lists the
    /// crafting station's recipes, shows required versus owned materials and lets
    /// the player craft when a recipe's inputs and tuner requirements are met.
    /// The panel owns the cursor while open and reads live inventory state, so no
    /// per-frame string rebuilding or polling is required.
    /// </summary>
    [DisallowMultipleComponent]
    public class CraftingUI : MonoBehaviour
    {
        [SerializeField] private CraftingStation m_Station;
        [SerializeField] private bool m_StartOpen;

        private bool m_Open;
        private int m_Selected;
        private Vector2 m_Scroll;
        private string m_Status = "";
        private float m_StatusUntil;
        private bool m_CursorOwned;

        private GUIStyle m_Panel;
        private GUIStyle m_Title;
        private GUIStyle m_Header;
        private GUIStyle m_Label;
        private GUIStyle m_Small;
        private GUIStyle m_RecipeButton;
        private GUIStyle m_RecipeSelected;
        private GUIStyle m_CraftButton;
        private GUIStyle m_CraftDisabled;
        private GUIStyle m_Enough;
        private GUIStyle m_Missing;
        private GUIStyle m_StatusStyle;

        private Texture2D m_PanelTex;
        private Texture2D m_SelectTex;
        private Texture2D m_CraftTex;
        private Texture2D m_CraftDisabledTex;

        public bool IsOpen => m_Open;

        public CraftingStation Station => m_Station;

        private void Awake()
        {
            if (m_Station == null) m_Station = FindFirstObjectByType<CraftingStation>();
            m_Open = m_StartOpen;
            if (m_Open)
            {
                GameplayInput.PushUiBlocker();
                AcquireCursor();
            }
        }

        private void OnDestroy()
        {
            if (m_Open) GameplayInput.PopUiBlocker();
            if (m_CursorOwned) RestoreCursor();
            if (m_PanelTex != null) Destroy(m_PanelTex);
            if (m_SelectTex != null) Destroy(m_SelectTex);
            if (m_CraftTex != null) Destroy(m_CraftTex);
            if (m_CraftDisabledTex != null) Destroy(m_CraftDisabledTex);
        }

        private void Update()
        {
            if (m_Station == null) m_Station = FindFirstObjectByType<CraftingStation>();

            if (PauseManager.IsPaused)
            {
                if (m_Open) SetOpen(false);
                return;
            }

            if (Input.GetKeyDown(KeyCode.R))
                SetOpen(!m_Open);

            if (!string.IsNullOrEmpty(m_Status) && Time.unscaledTime > m_StatusUntil)
                m_Status = "";
        }

        /// <summary>Opens or closes the panel and takes/releases the cursor.</summary>
        public void SetOpen(bool open)
        {
            if (m_Open == open) return;

            if (open)
            {
                // Only one gameplay panel owns the cursor at a time.
                UniverseSelectionUI other = FindFirstObjectByType<UniverseSelectionUI>();
                if (other != null && other.IsOpen) other.SetOpen(false);
            }

            m_Open = open;
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

        private void Status(string message)
        {
            m_Status = message;
            m_StatusUntil = Time.unscaledTime + 3f;
        }

        private void EnsureStyles()
        {
            if (m_Panel != null) return;

            m_PanelTex = MakeTexture(new Color(0.035f, 0.05f, 0.08f, 0.96f));
            m_SelectTex = MakeTexture(new Color(0.95f, 0.45f, 0.08f, 0.85f));

            m_Panel = new GUIStyle(GUI.skin.box) { normal = { background = m_PanelTex } };
            m_Panel.padding = new RectOffset(16, 16, 14, 14);

            m_Title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.55f, 0.10f) }
            };

            m_Header = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.85f, 0.9f, 0.98f) }
            };

            m_Label = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                normal = { textColor = new Color(0.85f, 0.88f, 0.92f) }
            };

            m_Small = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.6f, 0.65f, 0.72f) }
            };

            m_RecipeButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = 30,
                margin = new RectOffset(4, 4, 3, 3)
            };
            m_RecipeButton.normal.textColor = new Color(0.85f, 0.9f, 0.98f);
            m_RecipeButton.hover.textColor = Color.white;

            m_RecipeSelected = new GUIStyle(m_RecipeButton);
            m_RecipeSelected.normal.background = m_SelectTex;
            m_RecipeSelected.normal.textColor = Color.black;
            m_RecipeSelected.hover.background = m_SelectTex;
            m_RecipeSelected.hover.textColor = Color.black;
            m_RecipeSelected.active.background = m_SelectTex;
            m_RecipeSelected.active.textColor = Color.black;

            m_CraftButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 20,
                fixedHeight = 42,
                margin = new RectOffset(4, 4, 4, 4)
            };
            m_CraftTex = MakeTexture(new Color(0.12f, 0.45f, 0.18f, 0.95f));
            m_CraftDisabledTex = MakeTexture(new Color(0.12f, 0.13f, 0.15f, 0.9f));
            m_CraftButton.normal.background = m_CraftTex;
            m_CraftButton.hover.background = m_SelectTex;
            m_CraftButton.active.background = m_SelectTex;
            m_CraftButton.normal.textColor = Color.white;
            m_CraftButton.hover.textColor = Color.black;
            m_CraftButton.active.textColor = Color.black;

            m_CraftDisabled = new GUIStyle(m_CraftButton);
            m_CraftDisabled.normal.background = m_CraftDisabledTex;
            m_CraftDisabled.hover.background = m_CraftDisabledTex;
            m_CraftDisabled.active.background = m_CraftDisabledTex;
            m_CraftDisabled.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
            m_CraftDisabled.hover.textColor = new Color(0.5f, 0.5f, 0.5f);
            m_CraftDisabled.active.textColor = new Color(0.5f, 0.5f, 0.5f);

            m_Enough = new GUIStyle(m_Label) { normal = { textColor = new Color(0.45f, 0.9f, 0.45f) } };
            m_Missing = new GUIStyle(m_Label) { normal = { textColor = new Color(1f, 0.45f, 0.4f) } };

            m_StatusStyle = new GUIStyle(m_Label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.75f, 0.4f) }
            };
        }

        private void OnGUI()
        {
            if (!m_Open || m_Station == null) return;
            EnsureStyles();

            const float width = 540f;
            const float height = 480f;
            var area = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.Box(area, GUIContent.none, m_Panel);

            GUILayout.BeginArea(new Rect(area.x + 18f, area.y + 12f, area.width - 36f, area.height - 24f));

            GUILayout.Label("Crafting", m_Title);
            GUILayout.Label("Select a recipe, review materials, then craft. [R] Close", m_Small);
            GUILayout.Space(6f);

            var recipes = m_Station.Recipes;
            if (recipes == null || recipes.Count == 0)
            {
                GUILayout.Label("No crafting recipes available.", m_Label);
            }
            else
            {
                if (m_Selected >= recipes.Count) m_Selected = recipes.Count - 1;
                if (m_Selected < 0) m_Selected = 0;

                m_Scroll = GUILayout.BeginScrollView(m_Scroll, GUILayout.Height(210f));
                for (int i = 0; i < recipes.Count; i++)
                {
                    CraftingRecipe recipe = recipes[i];
                    if (recipe == null) continue;

                    bool can = m_Station.CanCraft(recipe);
                    string marker = can ? "  [Ready]" : "  [Short]";
                    bool clicked = GUILayout.Button(recipe.DisplayName + marker,
                        i == m_Selected ? m_RecipeSelected : m_RecipeButton);
                    if (clicked) m_Selected = i;
                }
                GUILayout.EndScrollView();

                GUILayout.Space(8f);
                DrawRecipeDetails(recipes[m_Selected]);
            }

            if (!string.IsNullOrEmpty(m_Status))
                GUILayout.Label(m_Status, m_StatusStyle);

            GUILayout.EndArea();
        }

        private void DrawRecipeDetails(CraftingRecipe recipe)
        {
            if (recipe == null) return;

            GUILayout.Label(recipe.DisplayName, m_Header);

            var inventory = m_Station.Inventory;
            bool canCraft = m_Station.CanCraft(recipe);

            if (recipe.Inputs != null)
            {
                foreach (CraftingIngredient ingredient in recipe.Inputs)
                {
                    if (ingredient == null || ingredient.resource == null) continue;
                    int owned = inventory != null ? inventory.GetAmount(ingredient.resource) : 0;
                    bool enough = owned >= ingredient.amount;
                    string line = $"  {ingredient.resource.DisplayName}: {owned}/{ingredient.amount}";
                    GUILayout.Label(line, enough ? m_Enough : m_Missing);
                }
            }

            if (recipe.RequiredRing > 0 || recipe.RequiredLayer > 0)
            {
                var tuner = m_Station.Tuner;
                string tunerLine = $"  Requires Tuner: Ring {recipe.RequiredRing}, Layer {recipe.RequiredLayer}";
                bool tunerOk = tuner == null || tuner.CanAccess(recipe.RequiredRing, recipe.RequiredLayer);
                GUILayout.Label(tunerLine, tunerOk ? m_Enough : m_Missing);
            }

            GUILayout.Space(8f);

            if (GUILayout.Button(canCraft ? "Craft" : "Cannot Craft", canCraft ? m_CraftButton : m_CraftDisabled))
            {
                if (m_Station.Craft(recipe))
                    Status("Crafted " + recipe.DisplayName + ".");
                else
                    Status("Missing materials.");
            }

            if (recipe.Output != null)
                GUILayout.Label($"Output: {recipe.Output.DisplayName} x{recipe.OutputAmount}", m_Small);
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