using UnityEngine;
using Portals.Core;
using Portals.Game;
using Portals.Menus;
using Portals.Save;

namespace Game
{
    /// <summary>
    /// Gameplay menu host. <c>Escape</c> toggles a pause menu with save, load and
    /// quit; <c>F5</c> quick-saves and <c>F9</c> quick-loads. Attached once to the
    /// persistent GameFlow host so it survives scene loads.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameMenuController : MonoBehaviour
    {
        /// <summary>Attaches the controller to the persistent host if it is missing.</summary>
        public static void EnsureAttached()
        {
            var host = GameObject.Find("Portal Game");
            if (host == null) return;
            if (host.GetComponent<GameMenuController>() != null) return;
            host.AddComponent<GameMenuController>();
        }

        private bool m_HasSave;
        private bool m_WasPaused;

        private void Update()
        {
            if (!GameFlow.InGameplay) return;

            bool paused = PauseManager.IsPaused;
            if (paused && !m_WasPaused)
                m_HasSave = ComputeHasSave();
            m_WasPaused = paused;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // Escape closes an open gameplay panel before pausing, so it never
                // both closes a panel and freezes the game on the same press.
                CraftingUI crafting = FindFirstObjectByType<CraftingUI>();
                if (crafting != null && crafting.IsOpen)
                {
                    crafting.SetOpen(false);
                    return;
                }

                UniverseSelectionUI universe = FindFirstObjectByType<UniverseSelectionUI>();
                if (universe != null && universe.IsOpen)
                {
                    universe.SetOpen(false);
                    return;
                }

                PauseManager.Toggle();
                return;
            }

            if (paused) return;

            if (Input.GetKeyDown(KeyCode.F5))
            {
                if (GameFlow.QuickSave(out int slot, out string error))
                {
                    m_HasSave = true;
                    MenuGUI.Status($"Quick saved to slot {slot + 1}.");
                }
                else
                {
                    MenuGUI.Status("Quick save failed: " + error);
                }
            }
            else if (Input.GetKeyDown(KeyCode.F9))
            {
                int slot = GameFlow.LastUsedSlot >= 0
                    ? GameFlow.LastUsedSlot
                    : SaveSystem.MostRecentValidSlot();

                if (slot < 0)
                    MenuGUI.Status("No saved game to load.");
                else if (GameFlow.LoadSlot(slot))
                    MenuGUI.Status($"Loaded slot {slot + 1}.");
                else
                    MenuGUI.Status("Load failed.");
            }
        }

        private static bool ComputeHasSave() =>
            GameFlow.LastUsedSlot >= 0 || SaveSystem.MostRecentValidSlot() >= 0;

        private void OnGUI()
        {
            if (!PauseManager.IsPaused || !GameFlow.InGameplay) return;

            MenuGUI.EnsureStyles();
            MenuGUI.DrawDim(0.62f);

            Rect box = MenuGUI.CenterArea(400f, 380f);
            GUI.Box(box, GUIContent.none);

            MenuGUI.Label(new Rect(box.x, box.y + 22f, box.width, 44f), "Paused", 32,
                TextAnchor.MiddleCenter, new Color(1f, 0.55f, 0.10f));

            float x = box.x + 40f;
            float w = box.width - 80f;
            float y = box.y + 90f;

            if (MenuGUI.Button(new Rect(x, y, w, 48f), "Resume"))
                PauseManager.Resume();
            y += 58f;

            if (MenuGUI.Button(new Rect(x, y, w, 48f), "Save Game"))
            {
                if (GameFlow.QuickSave(out int slot, out string error))
                {
                    m_HasSave = true;
                    MenuGUI.Status($"Saved to slot {slot + 1}.");
                }
                else
                {
                    MenuGUI.Status("Save failed: " + error);
                }
            }
            y += 58f;

            if (MenuGUI.Button(new Rect(x, y, w, 48f), "Load Game", m_HasSave))
            {
                int slot = GameFlow.LastUsedSlot >= 0
                    ? GameFlow.LastUsedSlot
                    : SaveSystem.MostRecentValidSlot();

                if (slot >= 0 && GameFlow.LoadSlot(slot))
                    MenuGUI.Status($"Loaded slot {slot + 1}.");
                else
                    MenuGUI.Status("No valid save found.");
            }
            y += 58f;

            if (MenuGUI.Button(new Rect(x, y, w, 48f), "Quit"))
                GameFlow.QuitGame();

            MenuGUI.DrawStatus(new Rect(box.x + 20f, box.yMax - 60f, box.width - 40f, 24f));
        }
    }
}