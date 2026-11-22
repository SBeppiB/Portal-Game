using NUnit.Framework;
using Portals.Core;
using Portals.Integration;
using UnityEngine;

namespace Portals.Tests
{
    public class TestChamberBuilderTests
    {
        [TearDown]
        public void TearDown()
        {
            GameObject existing = GameObject.Find(TestChamberBuilder.RootName);
            if (existing != null) Object.DestroyImmediate(existing);
        }

        [Test]
        public void Generate_CreatesExpectedGeometryCollidersAndSpawnPoint()
        {
            TestChamberGenerationResult result = TestChamberBuilder.Generate(null, null);

            Assert.That(result.Root, Is.Not.Null);
            Assert.That(result.Root.name, Is.EqualTo(TestChamberBuilder.RootName));
            Assert.That(result.StructureCount, Is.GreaterThanOrEqualTo(15));
            Assert.That(result.PropCount, Is.EqualTo(8));
            Assert.That(result.RigidbodyCount, Is.EqualTo(8));
            Assert.That(result.ColliderCount, Is.EqualTo(result.StructureCount + result.PropCount));

            string[] expected =
            {
                "Start Platform", "Gap Entrance Wall", "Gap Exit Wall", "Landing Platform",
                "Gap Ceiling", "Elevated Platform", "Low Ledge", "High Balcony",
                "Elevated Ceiling", "Elevated Back Wall", "Mid-Air Launch Platform",
                "Mid-Air Landing Platform", "Mid-Air Aim Wall", "Puzzle Central Platform",
                "Locaversal Target Wall", "Interversal Target Wall",
                "Puzzle Destination Platform", "Puzzle Ceiling"
            };

            for (int i = 0; i < expected.Length; i++)
                Assert.That(result.Root.transform.Find(expected[i]), Is.Not.Null, $"Missing {expected[i]}");

            GameObject spawn = GameObject.Find(TestChamberBuilder.SpawnPointName);
            Assert.That(spawn, Is.Not.Null);
            Assert.That(spawn.transform.parent, Is.SameAs(result.Root.transform));
        }

        [Test]
        public void GeneratedProps_HaveValidRigidbodySettingsAndVariedSizes()
        {
            TestChamberBuilder.Generate(null, null);

            Rigidbody[] bodies = Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
            Assert.That(bodies.Length, Is.EqualTo(8));

            bool foundOversized = false;
            bool foundCapsule = false;
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody body = bodies[i];
                Assert.That(body.useGravity, Is.True, body.name);
                Assert.That(body.GetComponent<Collider>(), Is.Not.Null, body.name);
                Assert.That(body.mass, Is.GreaterThan(0f));
                if (body.name == "Prop Cube Oversized" && body.transform.localScale.x > 2f)
                    foundOversized = true;
                if (body.GetComponent<CapsuleCollider>() != null) foundCapsule = true;
            }

            Assert.That(foundOversized, Is.True, "Need an object too large for the default aperture.");
            Assert.That(foundCapsule, Is.True, "Need capsule-shaped physics props.");
        }

        [Test]
        public void SpawnPoint_IsAccessibleFromStartingPlatform()
        {
            TestChamberBuilder.Generate(null, null);
            GameObject spawn = GameObject.Find(TestChamberBuilder.SpawnPointName);
            Physics.SyncTransforms();

            Ray downward = new Ray(spawn.transform.position + Vector3.up, Vector3.down);
            Assert.That(Physics.Raycast(downward, out RaycastHit hit, 3f), Is.True,
                "Spawn point must stand on a collider.");
            Assert.That(hit.collider.name, Is.EqualTo("Start Platform"));
        }

        [Test]
        public void Regenerate_ReplacesChamberWithoutDuplicatingGeometry()
        {
            TestChamberGenerationResult first = TestChamberBuilder.Generate(null, null);
            int firstChildCount = first.Root.transform.childCount;

            TestChamberGenerationResult second = TestChamberBuilder.Generate(null, null);

            Assert.That(second.Root, Is.Not.SameAs(first.Root));
            Assert.That(second.Root.transform.childCount, Is.EqualTo(firstChildCount));

            int chamberRoots = 0;
            GameObject[] all = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == TestChamberBuilder.RootName) chamberRoots++;

            Assert.That(chamberRoots, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<PortalLifecycleManager>(FindObjectsSortMode.None).Length,
                Is.LessThanOrEqualTo(1), "Generator must not duplicate portal managers.");
        }
    }
}
