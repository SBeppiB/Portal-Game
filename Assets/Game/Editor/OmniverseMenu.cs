#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Game.Omniverse;

namespace Game.Editor
{
    public static class OmniverseMenu
    {
        [MenuItem("Tools/Portal/Omniverse/Create Universe Asset")]
        public static void CreateUniverseAsset()
        {
            var coord = OmniverseCoordinate.Prime;
            var go = new GameObject($"Universe_{coord}");
            Undo.RegisterCreatedObjectUndo(go, "Create Universe");
            Selection.activeGameObject = go;
        }

        [MenuItem("Tools/Portal/Omniverse/Generate Ring (Log)")]
        public static void GenerateRing()
        {
            Debug.Log("[Omniverse] Generate Ring: author DivergenceProfile assets per ring 0-10.");
        }

        [MenuItem("Tools/Portal/Omniverse/Preview Divergence")]
        public static void PreviewDivergence()
        {
            Debug.Log("[Omniverse] Preview Divergence: select a DivergenceProfile asset.");
        }

        [MenuItem("Tools/Portal/Omniverse/Simulate Drift")]
        public static void SimulateDrift()
        {
            Debug.Log("[Omniverse] Simulate Drift: add UniverseDriftManager to scene.");
        }

        [MenuItem("Tools/Portal/Omniverse/Simulate Collapse")]
        public static void SimulateCollapse()
        {
            Debug.Log("[Omniverse] Simulate Collapse: add CollapseController to scene.");
        }
    }
}
#endif