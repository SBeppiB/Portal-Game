using Portals.Integration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor entry point for the greybox test chamber generator. The actual layout lives in
/// TestChamberBuilder so it can be reused and unit-tested outside the editor menu.
/// </summary>
public static class PortalTestChamberGeneratorEditor
{
    private const string MaterialsFolder = "Assets/Portals/Materials";
    private const string StructureMaterialPath = MaterialsFolder + "/TestChamber_Structure.mat";
    private const string PropMaterialPath = MaterialsFolder + "/TestChamber_Props.mat";

    [MenuItem("Tools/Portal/Generate Test Chamber")]
    private static void GenerateTestChamber()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[TestChamber] Exit Play Mode before generating a test chamber.");
            return;
        }

        if (TestChamberBuilder.ChamberExists())
        {
            bool replace = EditorUtility.DisplayDialog(
                "Replace Test Chamber?",
                "A TestChamber already exists in this scene. Regenerating will delete it and all of its children.",
                "Replace", "Cancel");
            if (!replace) return;
        }

        Material structureMaterial = EnsureMaterial(StructureMaterialPath,
            "TestChamber Structure", new Color(0.45f, 0.46f, 0.48f, 1f));
        Material propMaterial = EnsureMaterial(PropMaterialPath,
            "TestChamber Props", new Color(0.62f, 0.63f, 0.65f, 1f));

        TestChamberGenerationResult result = TestChamberBuilder.Generate(structureMaterial, propMaterial);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log($"[TestChamber] Generated '{TestChamberBuilder.RootName}': " +
                  $"{result.StructureCount} structures, {result.PropCount} props, " +
                  $"{result.RigidbodyCount} rigidbodies, {result.ColliderCount} colliders, " +
                  $"SpawnPoint={(result.SpawnPointCreated ? "created" : "existing")}, " +
                  $"portal systems {(result.PortalSystemsAlreadyPresent ? "reused" : "checked")}.");

        foreach (string warning in result.Warnings) Debug.LogWarning("[TestChamber] " + warning);

        Debug.Log("[TestChamber] Save the scene to persist the generated geometry.");
    }

    private static Material EnsureMaterial(string path, string name, Color colour)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;

        if (!AssetDatabase.IsValidFolder(MaterialsFolder))
            AssetDatabase.CreateFolder("Assets/Portals", "Materials");

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        material = new Material(shader) { name = name };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
        if (material.HasProperty("_Color")) material.SetColor("_Color", colour);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0f);

        AssetDatabase.CreateAsset(material, path);
        AssetDatabase.SaveAssets();
        return material;
    }
}
