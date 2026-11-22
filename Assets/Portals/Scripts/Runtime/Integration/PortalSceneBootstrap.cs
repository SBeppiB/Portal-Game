using UnityEngine;
using UnityEngine.SceneManagement;

namespace Portals.Integration
{
    /// <summary>
    /// Automatically wires the portal systems when SampleScene starts playing, so the
    /// scene works without any manual object creation. Existing objects are never
    /// duplicated. Scenes without a Player are ignored (this also keeps test scenes clean).
    /// </summary>
    public static class PortalSceneBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnSceneLoaded()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            bool looksLikeGameScene = sceneName == "SampleScene" || GameObject.Find("Player") != null;
            if (!looksLikeGameScene) return;

            PortalSceneSetup.EnsureSetup(out _);
        }
    }
}
