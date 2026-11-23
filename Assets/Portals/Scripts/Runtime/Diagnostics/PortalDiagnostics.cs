using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Portals.Diagnostics
{
    public static class PortalDiagnostics
    {
        public static DiagnosticsService Service { get; private set; }
        public static DiagnosticsOverlay Overlay { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInit()
        {
            if (Service != null) return;
            try
            {
                Service = new DiagnosticsService(DiagnosticsService.ResolveLogRoot());
                Service.Info("Bootstrap", "Application started", "scene=" + SceneManager.GetActiveScene().name);

                SceneManager.sceneLoaded += (s, m) => Service?.Info("Scene", "Scene loaded", s.name);
                SceneManager.sceneUnloaded += (s) => Service?.Info("Scene", "Scene unloaded", s.name);

                Application.logMessageReceivedThreaded += OnLogMessage;

                // Host object for overlay (survives scene loads).
                GameObject host = new GameObject("Portal Diagnostics");
                UnityEngine.Object.DontDestroyOnLoad(host);
                Overlay = host.AddComponent<DiagnosticsOverlay>();

                // Attach input diagnostics to any player found now and on scene load.
                SceneManager.sceneLoaded += (s, m) => AttachInputDiagnostics();
                AttachInputDiagnostics();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[PortalDiagnostics] Init failed: " + e);
            }
        }

        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (Service == null) return;
            // Avoid recursion: don't re-log our own diagnostics output.
            if (condition != null && condition.StartsWith("[PortalDiagnostics]")) return;
            LogLevel level = type == LogType.Error || type == LogType.Exception ? LogLevel.Error :
                            type == LogType.Warning ? LogLevel.Warning : LogLevel.Info;
            if (type == LogType.Exception || type == LogType.Error)
                Service.Log(level, "Unity", condition, null, stackTrace);
            else
                Service.Log(level, "Unity", condition);
        }

        private static void AttachInputDiagnostics()
        {
            try
            {
                GameObject player = GameObject.Find("Player");
                if (player == null) return;
                if (player.GetComponent<PortalInputDiagnostics>() == null)
                    player.AddComponent<PortalInputDiagnostics>();
            }
            catch { }
        }

        public static void Shutdown()
        {
            try { Service?.Info("Bootstrap", "Application shutting down", null); } catch { }
            try { Service?.Dispose(); } catch { }
            Service = null;
        }
    }
}
