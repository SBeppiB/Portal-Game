using UnityEditor;
using UnityEngine;

namespace Portals.Diagnostics
{
    public static class PortalDiagnosticsMenu
    {
        [MenuItem("Portal/Diagnostics/Open Log Folder")]
        public static void OpenLogFolder()
        {
            string root = DiagnosticsService.ResolveLogRoot();
            if (!System.IO.Directory.Exists(root)) System.IO.Directory.CreateDirectory(root);
#if UNITY_EDITOR
            EditorUtility.RevealInFinder(root);
#endif
        }

        [MenuItem("Portal/Diagnostics/Copy Latest Report")]
        public static void CopyLatestReport()
        {
            string path = System.IO.Path.Combine(DiagnosticsService.ResolveLogRoot(), "latest.log");
            if (System.IO.File.Exists(path))
                GUIUtility.systemCopyBuffer = System.IO.File.ReadAllText(path);
            else
                GUIUtility.systemCopyBuffer = "No latest log found.";
        }

        [MenuItem("Portal/Diagnostics/Clear Old Logs")]
        public static void ClearOldLogs()
        {
            // Delete session logs beyond retention by instantiating a service then disposing.
            var svc = new DiagnosticsService(DiagnosticsService.ResolveLogRoot());
            svc.ClearOldLogs();
            svc.Dispose();
        }
    }
}