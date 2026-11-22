using Portals.Integration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Optional one-click authoring tool. The same objects are created automatically when
/// SampleScene plays, so this menu item is only needed if you want them saved in the
/// scene file and visible while editing.
/// </summary>
public static class PortalSceneSetupEditor
{
    [MenuItem("Tools/Portal/Setup Portals in Active Scene")]
    private static void SetupPortals()
    {
        bool created = PortalSceneSetup.EnsureSetup(out string message);
        Debug.Log($"[PortalSceneSetup] {message}");

        if (!created) return;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[PortalSceneSetup] Scene marked dirty - save it to persist these objects.");
    }
}
