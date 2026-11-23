using System;
using UnityEngine;
using Portals.Core;

namespace Portals.Game
{
    /// <summary>
    /// Central gameplay-input gate. Every gameplay system reads input through this
    /// class instead of touching UnityEngine.Input directly, so menus, pause and the
    /// diagnostics overlay can block gameplay input from a single place instead of
    /// scattering pause checks through unrelated scripts.
    ///
    /// UI code (menus, diagnostics) must keep using Input.* directly - it is never
    /// gated. Audio listeners are swapped by PauseManager so menu sounds keep
    /// working while gameplay audio is silenced.
    /// </summary>
    public static class GameplayInput
    {
        /// <summary>Set by the diagnostics overlay while it is on screen.</summary>
        public static bool DiagnosticsVisible { get; set; }

        /// <summary>Set by GameFlow whenever the active scene changes.</summary>
        public static bool InGameplayScene { get; set; }

        private static int s_UiBlockers;

        /// <summary>True while a gameplay UI panel (crafting, universe selection) is open.</summary>
        public static bool UiBlocked => s_UiBlockers > 0;

        /// <summary>Called by UI panels when they open. Must be balanced by <see cref="PopUiBlocker"/>.</summary>
        public static void PushUiBlocker() => s_UiBlockers++;

        /// <summary>Called by UI panels when they close. Never goes below zero.</summary>
        public static void PopUiBlocker()
        {
            if (s_UiBlockers > 0) s_UiBlockers--;
        }

        /// <summary>Drops all UI blockers (used on scene transitions so a destroyed panel cannot leak one).</summary>
        public static void ClearUiBlockers() => s_UiBlockers = 0;

        /// <summary>True while any menu/pause/diagnostics/UI-panel state owns the input.</summary>
        public static bool Blocked =>
            PauseManager.IsPaused || !InGameplayScene || DiagnosticsVisible || UiBlocked;

        /// <summary>Raised whenever the blocked state flips; useful for HUD hints.</summary>
        public static event Action<bool> BlockedChanged;

        private static bool _lastBlocked = true;

        /// <summary>Called once per frame by the game host to detect block transitions.</summary>
        public static void PollBlockState()
        {
            bool blocked = Blocked;
            if (blocked == _lastBlocked) return;
            _lastBlocked = blocked;
            BlockedChanged?.Invoke(blocked);
        }

        // -----------------------------------------------------------------
        // Gated wrappers. When blocked they return neutral values without
        // touching UnityEngine.Input at all, so no key press leaks through.
        // -----------------------------------------------------------------

        public static bool GetKey(KeyCode key) => !Blocked && Input.GetKey(key);
        public static bool GetKeyDown(KeyCode key) => !Blocked && Input.GetKeyDown(key);
        public static bool GetKeyUp(KeyCode key) => !Blocked && Input.GetKeyUp(key);

        public static bool GetButton(string button) => !Blocked && Input.GetButton(button);
        public static bool GetButtonDown(string button) => !Blocked && Input.GetButtonDown(button);
        public static bool GetButtonUp(string button) => !Blocked && Input.GetButtonUp(button);

        public static float GetAxis(string axis) => Blocked ? 0f : Input.GetAxis(axis);
        public static float GetAxisRaw(string axis) => Blocked ? 0f : Input.GetAxisRaw(axis);

        public static bool GetMouseButton(int button) => !Blocked && Input.GetMouseButton(button);
        public static bool GetMouseButtonDown(int button) => !Blocked && Input.GetMouseButtonDown(button);
        public static bool GetMouseButtonUp(int button) => !Blocked && Input.GetMouseButtonUp(button);

        public static Vector3 mousePosition => Blocked ? Vector3.zero : Input.mousePosition;
    }
}
