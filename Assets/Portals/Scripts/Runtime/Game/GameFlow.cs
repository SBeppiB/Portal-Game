using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Portals.Core;
using Portals.Diagnostics;
using Portals.Save;
using Save = Portals.Save;

namespace Portals.Game
{
    /// <summary>
    /// Central game-flow state machine: main menu &lt;-&gt; gameplay, new game,
    /// continue, load-slot and quit. Owns the persistent "Portal Game" host that
    /// carries the pause menu and per-frame input polling across scene loads.
    ///
    /// Transitions always go through here so the game can never be left paused,
    /// cursor-locked in a menu, or with a pending save applied to the wrong scene.
    /// </summary>
    public static class GameFlow
    {
        public enum FlowState { Boot, MainMenu, Gameplay }

        public const string MenuSceneName = "MainMenu";
        public const string GameplaySceneName = "MAINSCENE";

        public static FlowState State { get; private set; } = FlowState.Boot;
        public static bool InGameplay => State == FlowState.Gameplay;

        /// <summary>Save data waiting to be applied to the freshly loaded scene.</summary>
        public static Save.SaveGameData PendingSave { get; private set; }

        /// <summary>Last slot the player saved to / loaded from (drives quick save).</summary>
        public static int LastUsedSlot { get; set; } = -1;

        private static GameObject _host;
        private static GameHost _hostBehaviour;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (_host != null) return;

            _host = new GameObject("Portal Game");
            Object.DontDestroyOnLoad(_host);
            _hostBehaviour = _host.AddComponent<GameHost>();

            SceneManager.sceneLoaded += OnSceneLoaded;
            GameplayInput.InGameplayScene = false;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // A scene transition destroys any open gameplay UI; drop its blockers so
            // input cannot stay blocked because a panel's OnDisable never ran.
            GameplayInput.ClearUiBlockers();

            if (scene.name == MenuSceneName)
            {
                State = FlowState.MainMenu;
                GameplayInput.InGameplayScene = false;
                PendingSave = null;

                // A menu must never inherit a paused gameplay state.
                PauseManager.ForceResumeForTransition();
                EnsureMenuAudioListener();
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
            else
            {
                State = FlowState.Gameplay;
                GameplayInput.InGameplayScene = true;

                // Loading must never leave the game paused.
                PauseManager.ForceResumeForTransition();

                if (PendingSave != null && _hostBehaviour != null)
                {
                    SaveGameData pending = PendingSave;
                    PendingSave = null;
                    _hostBehaviour.StartCoroutine(ApplyPendingSave(pending));
                }
            }
        }

        private static IEnumerator ApplyPendingSave(SaveGameData data)
        {
            // Let PortalSceneBootstrap / PortalSceneSetup finish wiring the scene
            // (portal systems, energy, firing controller) before restoring state.
            for (int i = 0; i < 3; i++) yield return null;

            bool ok = false;
            try { ok = GameplaySaveBridge.Apply(data); }
            catch (System.Exception e)
            {
                PortalDiagnostics.Service?.CaptureException(e, "SaveLoad");
            }

            if (!ok)
                Debug.LogError("[GameFlow] Failed to apply saved game state.");
            else
                Debug.Log($"[GameFlow] Loaded save '{data.saveName}' ({data.sceneName}).");
        }

        // ------------------------------------------------------------------
        // Public flow operations (called by the menus)
        // ------------------------------------------------------------------

        /// <summary>Starts a fresh game: no pending save, clean gameplay scene.</summary>
        public static void StartNewGame()
        {
            PendingSave = null;
            LastUsedSlot = -1;
            PauseManager.ForceResumeForTransition();
            SceneManager.LoadScene(GameplaySceneName);
        }

        /// <summary>Loads the most recently saved valid slot. False if none exists.</summary>
        public static bool ContinueGame()
        {
            int slot = Save.SaveSystem.MostRecentValidSlot();
            if (slot < 0) return false;
            return LoadSlot(slot);
        }

        /// <summary>Loads a specific slot. False when the slot is missing/invalid.</summary>
        public static bool LoadSlot(int slot)
        {
            SaveGameData data = Save.SaveSystem.Load(slot);
            if (data == null) return false;

            PendingSave = data;
            LastUsedSlot = slot;
            PauseManager.ForceResumeForTransition();

            if (data.sceneName == SceneManager.GetActiveScene().name && InGameplay)
            {
                // Already in the right scene: apply in place (still goes through the
                // coroutine so scene wiring settles first).
                SaveGameData pending = PendingSave;
                PendingSave = null;
                if (_hostBehaviour != null) _hostBehaviour.StartCoroutine(ApplyPendingSave(pending));
            }
            else
            {
                SceneManager.LoadScene(data.sceneName);
            }
            return true;
        }

        /// <summary>Saves the running game into a slot (overwrites after caller confirmed).</summary>
        public static bool SaveToSlot(int slot, out string error)
        {
            error = null;
            if (!InGameplay)
            {
                error = "Not in gameplay.";
                return false;
            }

            SaveGameData data = GameplaySaveBridge.Capture(slot);
            if (data == null)
            {
                error = "Could not capture game state.";
                return false;
            }

            if (!Save.SaveSystem.Save(slot, data, out error)) return false;
            LastUsedSlot = slot;
            return true;
        }

        /// <summary>Saves to the most recent slot, or slot 0 when none used yet.</summary>
        public static bool QuickSave(out int slot, out string error)
        {
            slot = LastUsedSlot >= 0 ? LastUsedSlot : Save.SaveSystem.MostRecentValidSlot();
            if (slot < 0) slot = 0;
            return SaveToSlot(slot, out error);
        }

        public static void ReturnToMainMenu()
        {
            PendingSave = null;
            PauseManager.ForceResumeForTransition();
            SceneManager.LoadScene(MenuSceneName);
        }

        public static void QuitGame()
        {
            PauseManager.ForceResumeForTransition();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit();
#endif
        }

        private static void EnsureMenuAudioListener()
        {
            if (Camera.main != null && Camera.main.GetComponent<AudioListener>() != null) return;
            if (Object.FindFirstObjectByType<AudioListener>() != null) return;

            var go = new GameObject("Menu Audio Listener");
            go.AddComponent<AudioListener>();
        }
    }
}
