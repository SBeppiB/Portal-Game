using UnityEngine;

namespace Portals.Diagnostics
{
    public class DiagnosticsOverlay : MonoBehaviour
    {
        private bool _visible = true;
        private Vector2 _scroll;
        private string _status = "";
        private string m_CachedReport;
        private float m_NextReportRefresh;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F12)) _visible = !_visible;
        }

        private void OnGUI()
        {
            if (!_visible || PortalDiagnostics.Service == null) return;

            // GetReport() rebuilds a string from the whole in-memory buffer (up to
            // 1000 entries). Calling it several times per OnGUI event (and OnGUI runs
            // multiple times per frame) caused heavy per-frame allocation and GC, so
            // cache it and refresh only a few times per second.
            if (m_CachedReport == null || Time.time >= m_NextReportRefresh)
            {
                m_CachedReport = PortalDiagnostics.Service.GetReport();
                m_NextReportRefresh = Time.time + 0.25f;
            }
            _status = FirstLine(m_CachedReport);

            GUILayout.BeginArea(new Rect(10, 200, 420, 320), GUI.skin.box);
            GUILayout.Label("Portal Diagnostics", GUI.skin.label);
            GUILayout.Label(_status);

            if (GUILayout.Button("Copy All Diagnostics")) PortalDiagnostics.Service.CopyReportToClipboard();
            if (GUILayout.Button("Copy Errors Only")) PortalDiagnostics.Service.CopyErrorsToClipboard();
            if (GUILayout.Button("Export Session Log")) { /* already persisted */ }
            if (GUILayout.Button("Open Log Folder")) PortalDiagnostics.Service.OpenLogFolder();
            if (GUILayout.Button("Clear Old Logs")) PortalDiagnostics.Service.ClearOldLogs();

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(180));
            GUILayout.TextArea(m_CachedReport, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private static string FirstLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int nl = s.IndexOf('\n');
            return nl >= 0 ? s.Substring(0, nl) : s;
        }

        private void OnApplicationQuit()
        {
            PortalDiagnostics.Shutdown();
        }
    }
}