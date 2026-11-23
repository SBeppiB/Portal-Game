using System;
using UnityEngine;

namespace Portals.Core
{
    /// <summary>
    /// Central pause-state manager. All gameplay systems should consult
    /// PauseManager.IsPaused instead of keeping their own pause flags.
    ///
    /// Pausing sets Time.timeScale to 0 (stops physics, animations, timers and
    /// deltaTime-driven gameplay), swaps to a dedicated UI AudioListener (gameplay
    /// audio goes silent, menu sounds keep playing) and frees the cursor.
    /// UI must keep running on unscaled time (OnGUI, Time.unscaledTime).
    /// </summary>
    public static class PauseManager
    {
        public static bool IsPaused { get; private set; }
        public static event Action<bool> PauseChanged;

        private static float _previousTimeScale = 1f;
        private static GameObject _uiAudioHost;
        private static bool _gameplayListenerWasEnabled;

        public static void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;
            _previousTimeScale = Time.timeScale <= 0f ? 1f : Time.timeScale;
            Time.timeScale = 0f;

            // Swap audio listeners so gameplay audio stops but UI sounds still play.
            // The UI listener host is created lazily but kept alive (DDOL) so it is
            // never duplicated across scenes.
            AudioListener main = Camera.main != null ? Camera.main.GetComponent<AudioListener>() : null;
            _gameplayListenerWasEnabled = main != null && main.enabled;
            if (main != null) main.enabled = false;

            if (_uiAudioHost == null)
            {
                _uiAudioHost = new GameObject("Pause UI Audio Listener");
                UnityEngine.Object.DontDestroyOnLoad(_uiAudioHost);
                _uiAudioHost.AddComponent<AudioListener>();
            }
            _uiAudioHost.SetActive(true);
            AudioListener uiListener = _uiAudioHost.GetComponent<AudioListener>();
            if (uiListener != null) uiListener.enabled = true;

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            PauseChanged?.Invoke(true);
        }

        public static void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = _previousTimeScale <= 0f ? 1f : _previousTimeScale;

            if (_uiAudioHost != null) _uiAudioHost.SetActive(false);
            AudioListener main = Camera.main != null ? Camera.main.GetComponent<AudioListener>() : null;
            if (main != null && _gameplayListenerWasEnabled) main.enabled = true;

            // Only re-lock the cursor when we are actually in gameplay; the main
            // menu keeps a visible cursor (GameFlow also re-applies this per scene).
            if (Portals.Game.GameplayInput.InGameplayScene)
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }

            PauseChanged?.Invoke(false);
        }

        public static void Toggle()
        {
            if (IsPaused) Resume(); else Pause();
        }

        /// <summary>
        /// Used during scene transitions (return to main menu, loading a save):
        /// guarantees the game never stays accidentally paused across a scene
        /// change, restoring time scale, listeners and pause events.
        /// </summary>
        public static void ForceResumeForTransition()
        {
            if (IsPaused)
            {
                Resume();
                return;
            }
            // Not "paused" but time scale may have been left at 0 by a crash path.
            if (Time.timeScale <= 0f) Time.timeScale = 1f;
        }
    }
}
