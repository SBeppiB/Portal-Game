using System;
using System.Collections.Generic;
using System.Reflection;
using Portals.Core;
using UnityEngine;

namespace Portals.Integration
{
    /// <summary>Summary of a generated test chamber, used for editor logging and tests.</summary>
    public sealed class TestChamberGenerationResult
    {
        public GameObject Root;
        public int StructureCount;
        public int PropCount;
        public int RigidbodyCount;
        public int ColliderCount;
        public bool SpawnPointCreated;
        public bool PortalSystemsAlreadyPresent;
        public readonly List<string> Warnings = new List<string>();
    }

    /// <summary>
    /// Builds a functional greybox chamber for portal placement/traversal testing.
    /// Reusable from Editor tooling or runtime; it only creates geometry and a spawn point.
    /// It never creates or modifies the player, camera or portal system objects.
    /// </summary>
    public static class TestChamberBuilder
    {
        public const string RootName = "TestChamber";
        public const string SpawnPointName = "SpawnPoint";

        private static readonly Color DefaultStructureColour = new Color(0.45f, 0.46f, 0.48f, 1f);
        private static readonly Color DefaultPropColour = new Color(0.62f, 0.63f, 0.65f, 1f);

        public static bool ChamberExists() => GameObject.Find(RootName) != null;

        /// <summary>
        /// Removes any previous TestChamber and builds a fresh one. Returns a report with
        /// counts and warnings so the editor tooling and tests can verify the result.
        /// </summary>
        public static TestChamberGenerationResult Generate(Material structureMaterial, Material propMaterial)
        {
            TestChamberGenerationResult result = new TestChamberGenerationResult();

            GameObject existing = GameObject.Find(RootName);
            if (existing != null) DestroyObject(existing);

            if (structureMaterial == null) structureMaterial = CreateFallbackMaterial(DefaultStructureColour);
            if (propMaterial == null) propMaterial = CreateFallbackMaterial(DefaultPropColour);

            GameObject root = new GameObject(RootName);
            result.Root = root;

            BuildStartingArea(root, structureMaterial, result);
            BuildFirstGap(root, structureMaterial, result);
            BuildElevatedArea(root, structureMaterial, result);
            BuildMidAirSection(root, structureMaterial, result);
            BuildPuzzleSection(root, structureMaterial, result);
            BuildPhysicsProps(root, propMaterial, result);

            EnsurePortalSystems(result);
            EnsureSpawnPointAssigned(result);

            return result;
        }

        private static void BuildStartingArea(GameObject root, Material material, TestChamberGenerationResult result)
        {
            CreateBox("Start Platform", new Vector3(0f, -0.5f, 0f), new Vector3(10f, 1f, 10f), material, root, result);

            GameObject spawn = new GameObject(SpawnPointName);
            spawn.transform.SetParent(root.transform, false);
            spawn.transform.position = new Vector3(0f, 0.05f, -2f);
            result.SpawnPointCreated = true;
        }

        private static void BuildFirstGap(GameObject root, Material material, TestChamberGenerationResult result)
        {
            // A 20 m void: the walls on both sides are tall enough for the 2 x 3 m aperture.
            CreateBox("Gap Entrance Wall", new Vector3(0f, 4f, 4.8f),
                new Vector3(10f, 8f, 0.4f), material, root, result);
            CreateBox("Gap Exit Wall", new Vector3(0f, 4f, 25.2f),
                new Vector3(10f, 8f, 0.4f), material, root, result);
            CreateBox("Landing Platform", new Vector3(0f, -0.5f, 30f),
                new Vector3(12f, 1f, 10f), material, root, result);
            CreateBox("Gap Ceiling", new Vector3(0f, 7f, 15f),
                new Vector3(12f, 0.4f, 24f), material, root, result);
        }

        private static void BuildElevatedArea(GameObject root, Material material, TestChamberGenerationResult result)
        {
            CreateBox("Elevated Platform", new Vector3(0f, 3.5f, 40f),
                new Vector3(10f, 1f, 8f), material, root, result);
            CreateBox("Low Ledge", new Vector3(-7f, 1f, 38f),
                new Vector3(5f, 1f, 6f), material, root, result);
            CreateBox("High Balcony", new Vector3(7f, 6f, 42f),
                new Vector3(5f, 1f, 6f), material, root, result);
            CreateBox("Elevated Ceiling", new Vector3(0f, 10f, 40f),
                new Vector3(14f, 0.4f, 12f), material, root, result);
            CreateBox("Elevated Back Wall", new Vector3(0f, 4f, 44.8f),
                new Vector3(14f, 8f, 0.4f), material, root, result);
        }

        private static void BuildMidAirSection(GameObject root, Material material, TestChamberGenerationResult result)
        {
            CreateBox("Mid-Air Launch Platform", new Vector3(0f, -0.5f, 50f),
                new Vector3(8f, 1f, 8f), material, root, result);
            // Open space from z ~54 to z ~86 (nothing within the 20 m shot range).
            CreateBox("Mid-Air Landing Platform", new Vector3(0f, -0.5f, 90f),
                new Vector3(8f, 1f, 8f), material, root, result);
            CreateBox("Mid-Air Aim Wall", new Vector3(0f, 4f, 94f),
                new Vector3(10f, 8f, 0.4f), material, root, result);
        }

        private static void BuildPuzzleSection(GameObject root, Material material, TestChamberGenerationResult result)
        {
            CreateBox("Puzzle Central Platform", new Vector3(18f, -0.5f, 60f),
                new Vector3(10f, 1f, 10f), material, root, result);
            CreateBox("Locaversal Target Wall", new Vector3(13f, 4f, 60f),
                new Vector3(0.4f, 8f, 10f), material, root, result);
            CreateBox("Interversal Target Wall", new Vector3(23f, 4f, 60f),
                new Vector3(0.4f, 8f, 10f), material, root, result);
            CreateBox("Puzzle Destination Platform", new Vector3(18f, -0.5f, 74f),
                new Vector3(10f, 1f, 10f), material, root, result);
            CreateBox("Puzzle Ceiling", new Vector3(18f, 7f, 67f),
                new Vector3(12f, 0.4f, 26f), material, root, result);
        }

        private static void BuildPhysicsProps(GameObject root, Material material, TestChamberGenerationResult result)
        {
            // Different sizes test aperture validation (the 2.8 m cube cannot fit a 2 m wide aperture).
            CreateDynamicCube("Prop Cube Small", new Vector3(-2.5f, 0.5f, 2f), 0.8f,
                Quaternion.identity, 1f, material, root, result);
            CreateDynamicCube("Prop Cube Medium", new Vector3(0f, 0.9f, 2f), 1.6f,
                Quaternion.Euler(0f, 30f, 0f), 2f, material, root, result);
            CreateDynamicCube("Prop Cube Oversized", new Vector3(2.8f, 1.6f, 2f), 2.8f,
                Quaternion.identity, 4f, material, root, result);

            CreateDynamicCapsule("Prop Capsule Standard", new Vector3(-2f, 1f, 30f),
                0.4f, 1.6f, Quaternion.identity, 1f, material, root, result);
            CreateDynamicCapsule("Prop Capsule Large", new Vector3(2f, 1.6f, 30f),
                0.8f, 3f, Quaternion.identity, 3f, material, root, result);
            CreateDynamicCapsule("Prop Capsule Angled", new Vector3(0f, 1.2f, 34f),
                0.4f, 2f, Quaternion.Euler(30f, 20f, 15f), 1f, material, root, result);

            CreateDynamicCube("Prop Cube Angled Entry", new Vector3(-3f, 1f, 3f), 1f,
                Quaternion.Euler(0f, 45f, 20f), 1f, material, root, result);
            CreateDynamicCube("Prop Cube Far Angle", new Vector3(3f, 1f, 27f), 1.2f,
                Quaternion.Euler(15f, 0f, 45f), 1.5f, material, root, result);
        }

        private static GameObject CreateBox(string name, Vector3 position, Vector3 scale,
            Material material, GameObject parent, TestChamberGenerationResult result = null)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent.transform, false);
            box.transform.position = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;

            if (result != null)
            {
                result.StructureCount++;
                if (box.GetComponent<Collider>() != null) result.ColliderCount++;
            }
            return box;
        }

        private static void CreateDynamicCube(string name, Vector3 position, float size,
            Quaternion rotation, float mass, Material material, GameObject parent,
            TestChamberGenerationResult result)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent.transform, false);
            cube.transform.SetPositionAndRotation(position, rotation);
            cube.transform.localScale = Vector3.one * size;
            cube.GetComponent<Renderer>().sharedMaterial = material;

            Rigidbody body = cube.AddComponent<Rigidbody>();
            body.mass = mass;
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            body.linearDamping = 0.05f;
            body.angularDamping = 0.05f;

            result.PropCount++;
            result.RigidbodyCount++;
            if (cube.GetComponent<Collider>() != null) result.ColliderCount++;
        }

        private static void CreateDynamicCapsule(string name, Vector3 position, float radius, float height,
            Quaternion rotation, float mass, Material material, GameObject parent,
            TestChamberGenerationResult result)
        {
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = name;
            capsule.transform.SetParent(parent.transform, false);
            capsule.transform.SetPositionAndRotation(position, rotation);

            CapsuleCollider collider = capsule.GetComponent<CapsuleCollider>();
            collider.radius = radius;
            collider.height = height;

            Rigidbody body = capsule.AddComponent<Rigidbody>();
            body.mass = mass;
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            body.linearDamping = 0.05f;
            body.angularDamping = 0.05f;

            result.PropCount++;
            result.RigidbodyCount++;
            if (collider != null) result.ColliderCount++;
        }

        private static void EnsurePortalSystems(TestChamberGenerationResult result)
        {
            result.PortalSystemsAlreadyPresent =
                UnityEngine.Object.FindFirstObjectByType<PortalLifecycleManager>() != null &&
                UnityEngine.Object.FindFirstObjectByType<PortalPlacer>() != null;

            if (result.PortalSystemsAlreadyPresent) return;

            // Reuse the existing setup routine if a Player is available; it already guards
            // against duplicate managers and portals.
            PortalSceneSetup.EnsureSetup(out string message);
            result.Warnings.Add("Portal systems were not present; setup reported: " + message);
        }

        private static void EnsureSpawnPointAssigned(TestChamberGenerationResult result)
        {
            GameObject player = GameObject.Find("Player");
            if (player == null) return;

            Component movement = player.GetComponent("PlayerMovement");
            if (movement == null) return;

            FieldInfo field = movement.GetType().GetField("spawnPoint",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) return;

            GameObject spawn = GameObject.Find(SpawnPointName);
            if (spawn == null) return;

            field.SetValue(movement, spawn.transform);
        }

        private static Material CreateFallbackMaterial(Color colour)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material material = new Material(shader) { name = "TestChamber Greybox" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Color")) material.SetColor("_Color", colour);
            return material;
        }

        private static void DestroyObject(GameObject target)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
