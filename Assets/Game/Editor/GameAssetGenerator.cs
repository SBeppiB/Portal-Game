#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Game;
using System.IO;

namespace Game.Editor
{
    public static class GameAssetGenerator
    {
        [MenuItem("Tools/Portal/Generate Gameplay Assets")]
        public static void Generate()
        {
            string dir = "Assets/Resources/Game/Generated";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();

            // Resources
            var scrap = CreateAsset<ResourceDefinition>(dir, "Resource_ScrapMetal", r => { r.GetType().GetField("m_Id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(r, "scrap"); r.GetType().GetField("m_DisplayName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(r, "Scrap Metal"); r.GetType().GetField("m_Tier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(r, 0); });
            var copper = CreateAsset<ResourceDefinition>(dir, "Resource_CopperWire", r => { r.GetType().GetField("m_Id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(r, "copper"); r.GetType().GetField("m_DisplayName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(r, "Copper Wire"); r.GetType().GetField("m_Tier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(r, 1); });
            var crystal = CreateAsset<ResourceDefinition>(dir, "Resource_PortalCrystal", r => { r.GetType().GetField("m_Id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(r, "crystal"); r.GetType().GetField("m_DisplayName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(r, "Portal Crystal"); r.GetType().GetField("m_Tier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(r, 2); });

            // Database
            var db = CreateAsset<ResourceDatabase>(dir, "ResourceDatabase", d =>
            {
                var f = d.GetType().GetField("m_Resources", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var list = f.GetValue(d) as System.Collections.IList;
                list.Add(scrap); list.Add(copper); list.Add(crystal);
            });

            // Recipes
            CreateRecipe(dir, "Recipe_TunerMk2", "Portal Tuner Mk2", new[] { (scrap, 20), (copper, 10), (crystal, 5) }, null, 0, 0, unlockRing: 2);
            CreateRecipe(dir, "Recipe_TunerMk3", "Portal Tuner Mk3", new[] { (crystal, 10) }, null, 0, 0, unlockRing: 3);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[GameAssetGenerator] Generated gameplay assets in " + dir);
        }

        private static T CreateAsset<T>(string dir, string name, System.Action<T> configure) where T : ScriptableObject
        {
            string path = Path.Combine(dir, name + ".asset").Replace("\\", "/");
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) { configure(existing); EditorUtility.SetDirty(existing); return existing; }
            var asset = ScriptableObject.CreateInstance<T>();
            configure(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void CreateRecipe(string dir, string name, string displayName, (ResourceDefinition, int)[] inputs, ResourceDefinition output, int reqRing, int reqLayer, int unlockRing)
        {
            var recipe = CreateAsset<CraftingRecipe>(dir, name, r =>
            {
                var fId = r.GetType().GetField("m_Id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); fId.SetValue(r, name);
                var fDisp = r.GetType().GetField("m_DisplayName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); fDisp.SetValue(r, displayName);
                var fReqRing = r.GetType().GetField("m_RequiredRing", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); fReqRing.SetValue(r, reqRing);
                var fReqLayer = r.GetType().GetField("m_RequiredLayer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); fReqLayer.SetValue(r, reqLayer);
                var fUnlock = r.GetType().GetField("m_UnlockRingAccess", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); fUnlock.SetValue(r, unlockRing);
                var fInputs = r.GetType().GetField("m_Inputs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var list = fInputs.GetValue(r) as System.Collections.IList;
                list.Clear();
                foreach (var (res, amt) in inputs)
                {
                    var ing = new CraftingIngredient { resource = res, amount = amt };
                    list.Add(ing);
                }
                var fOut = r.GetType().GetField("m_Output", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); fOut.SetValue(r, output);
                var fOutAmt = r.GetType().GetField("m_OutputAmount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); fOutAmt.SetValue(r, 1);
            });
        }
    }
}
#endif