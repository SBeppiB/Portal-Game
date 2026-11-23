using System;
using UnityEngine;

namespace Portals.Menus
{
    /// <summary>
    /// User settings persisted in PlayerPrefs (kept separate from save files).
    /// Applies immediately so sliders/toggles give live feedback.
    /// </summary>
    public static class SettingsService
    {
        private const string KeySensitivity = "PortalGame.Settings.Sensitivity";
        private const string KeyVolume = "PortalGame.Settings.Volume";
        private const string KeyFullscreen = "PortalGame.Settings.Fullscreen";
        private const string KeyPerfHud = "PortalGame.Settings.PerfHud";

        public const float DefaultSensitivity = 2f;

        public static float Sensitivity { get; private set; } = DefaultSensitivity;
        public static float Volume { get; private set; } = 1f;
        public static bool Fullscreen { get; private set; }
        public static bool PerfHud { get; private set; } = true;

        /// <summary>Raised when sensitivity changes; PlayerMovement subscribes
        /// (it lives in Assembly-CSharp, which can reference this assembly but
        /// not the other way round).</summary>
        public static event Action<float> SensitivityChanged;

        private static bool _loaded;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            Sensitivity = PlayerPrefs.GetFloat(KeySensitivity, DefaultSensitivity);
            Volume = PlayerPrefs.GetFloat(KeyVolume, 1f);
            Fullscreen = PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1;
            PerfHud = PlayerPrefs.GetInt(KeyPerfHud, 1) == 1;
            Apply();
        }

        public static void SetSensitivity(float value)
        {
            Sensitivity = Mathf.Clamp(value, 0.2f, 8f);
            PlayerPrefs.SetFloat(KeySensitivity, Sensitivity);
            SensitivityChanged?.Invoke(Sensitivity);
        }

        public static void SetVolume(float value)
        {
            Volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(KeyVolume, Volume);
            Apply();
        }

        public static void SetFullscreen(bool value)
        {
            Fullscreen = value;
            PlayerPrefs.SetInt(KeyFullscreen, value ? 1 : 0);
            Apply();
        }

        public static void SetPerfHud(bool value)
        {
            PerfHud = value;
            PlayerPrefs.SetInt(KeyPerfHud, value ? 1 : 0);
        }

        public static void Save() => PlayerPrefs.Save();

        private static void Apply()
        {
            AudioListener.volume = Volume;
            if (Screen.fullScreen != Fullscreen) Screen.fullScreen = Fullscreen;
        }
    }
}
