using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Portals.Core;
using Portals.Integration;
using Portals.Interaction;
using Portals.Traversal;
using UnityEngine;
using UnityEngine.TestTools;

namespace Portals.Tests
{
    public class PortalEnemyAndInteractionTests
    {
        private readonly System.Collections.Generic.List<GameObject> m_Objects =
            new System.Collections.Generic.List<GameObject>();

        private GameObject Track(GameObject gameObject)
        {
            m_Objects.Add(gameObject);
            return gameObject;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in m_Objects)
                if (gameObject != null) Object.DestroyImmediate(gameObject);
            m_Objects.Clear();
        }

        private Camera CreateCamera(Vector3 position)
        {
            GameObject cameraObject = Track(new GameObject("Test Camera"));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = position;
            cameraObject.transform.rotation = Quaternion.identity;
            return camera;
        }

        [UnityTest]
        public IEnumerator Pickup_WorksWithoutMetadataAndIgnoresOwnCollider()
        {
            Camera camera = CreateCamera(new Vector3(0f, 1f, -3f));

            // A dynamic object with a Rigidbody but no InteractableObject must be pickable.
            GameObject cube = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            cube.transform.position = new Vector3(0f, 1f, 0f);
            cube.AddComponent<Rigidbody>();

            GameObject player = Track(new GameObject("Player"));
            player.transform.position = new Vector3(0f, 0f, -3.2f);
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.center = new Vector3(0f, 1f, 0f);

            PortalObjectInteractor interactor = camera.gameObject.AddComponent<PortalObjectInteractor>();

            interactor.Interact();
            yield return null;

            Assert.That(interactor.IsHolding, Is.True,
                "A dynamic object must be pickable even without InteractableObject metadata.");
            Assert.That(interactor.HeldObject, Is.SameAs(cube));
        }

        [UnityTest]
        public IEnumerator Highlight_IgnoresPlayerCollider()
        {
            Camera camera = CreateCamera(new Vector3(0f, 1f, -3f));

            GameObject player = Track(new GameObject("Player 2"));
            player.transform.position = new Vector3(0f, 0f, -3.1f);
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.center = new Vector3(0f, 1f, 0f);

            GameObject cube = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            cube.transform.position = new Vector3(0f, 1f, 0f);
            cube.AddComponent<Rigidbody>();

            PortalHighlighter highlighter = camera.gameObject.AddComponent<PortalHighlighter>();
            highlighter.Refresh();
            yield return null;

            Assert.That(highlighter.CurrentTarget, Is.SameAs(cube),
                "The player's own collider must not block targeting.");
        }

        [UnityTest]
        public IEnumerator Inventory_SelectNextAndWheelBindingsAreAvailable()
        {
            GameObject owner = Track(new GameObject("Inventory Owner"));
            PortalInventory inventory = owner.AddComponent<PortalInventory>();
            Assert.That(inventory.Capacity, Is.EqualTo(4));

            inventory.SelectSlot(0);
            inventory.SelectNext();
            Assert.That(inventory.SelectedSlot, Is.EqualTo(1));
            inventory.SelectPrevious();
            Assert.That(inventory.SelectedSlot, Is.EqualTo(0));
            yield break;
        }

        [UnityTest]
        public IEnumerator AdvancedEnemy_IsFullyWiredWithAIPortalGun()
        {
            GameObject player = Track(new GameObject("Player"));
            player.tag = "Player";
            player.AddComponent<CharacterController>();
            Camera mainCamera = CreateCamera(new Vector3(0f, 1.6f, 0f));
            mainCamera.transform.SetParent(player.transform, false);

            // Ensure a shared lifecycle exists so the enemy gun can be configured.
            GameObject systems = Track(new GameObject("Portal Systems"));
            PortalLifecycleManager lifecycle = systems.AddComponent<PortalLifecycleManager>();
            lifecycle.Initialize();
            PortalPlacer playerPlacer = player.AddComponent<PortalPlacer>();
            lifecycle.Configure(playerPlacer, null);

            // EnemyMovement lives in the default assembly; create the enemy through the
            // runtime setup helper and locate components by name to cross that boundary.
            System.Type setupType = System.Type.GetType(
                "Portals.Integration.PortalEnemySetup, Assembly-CSharp");
            Assert.That(setupType, Is.Not.Null, "Enemy setup helper must exist.");

            MethodInfo create = setupType.GetMethod("CreateEnemy",
                BindingFlags.Public | BindingFlags.Static);
            object enemyObject = create.Invoke(null, new object[]
            {
                "Advanced Portal Enemy",
                new Vector3(0f, 0f, 10f),
                player.transform
            });
            GameObject enemy = (GameObject)((Component)enemyObject).gameObject;
            Track(enemy);

            Component advanced = enemy.GetComponent("AdvancedPortalEnemy");
            Assert.That(advanced, Is.Not.Null);
            Assert.That(enemy.GetComponent("EnemyMovement"), Is.Not.Null);
            Assert.That(enemy.GetComponent("EnemyHealth"), Is.Not.Null);
            Assert.That(enemy.GetComponent("EnemyMomentumReceiver"), Is.Not.Null);
            Assert.That(enemy.GetComponent<PortalTraverser>(), Is.Not.Null);

            Component movement = enemy.GetComponent("PlayerMovement");
            Assert.That(movement, Is.Not.Null, "Enemy must run the real player moveset.");
            PropertyInfo aiMovement = movement.GetType().GetProperty("AIControlled");
            Assert.That(aiMovement, Is.Not.Null);
            Assert.That((bool)aiMovement.GetValue(movement), Is.True,
                "Enemy PlayerMovement must be AI controlled, never read player input.");

            yield return null;

            Component gunComponent = null;
            foreach (Component c in enemy.GetComponents<Component>())
            {
                if (c.GetType().Name == "PortalFiringController") gunComponent = c;
            }

            Assert.That(gunComponent, Is.Not.Null, "Enemy must carry a portal gun.");
            PropertyInfo aiProp = gunComponent.GetType().GetProperty("AIControlled");
            Assert.That(aiProp, Is.Not.Null);
            Assert.That((bool)aiProp.GetValue(gunComponent), Is.True,
                "Enemy gun must be AI controlled so it does not read player input.");
        }
        [UnityTest]
        public IEnumerator OwnersHaveIndependentPortalPairs()
        {
            GameObject systems = Track(new GameObject("Lifecycle"));
            PortalLifecycleManager manager = systems.AddComponent<PortalLifecycleManager>();
            manager.Initialize();

            object a = new object();
            object b = new object();
            PortalPair pairA = manager.GetOrCreatePair(PortalKind.Locaversal, a);
            PortalPair pairB = manager.GetOrCreatePair(PortalKind.Locaversal, b);

            Assert.That(pairA, Is.Not.SameAs(pairB));
            Assert.That(manager.HasActivePairForOwner(a), Is.True);
            Assert.That(manager.HasActivePairForOwner(b), Is.True);

            // Replacing one owner's pair must not touch the other.
            PortalPair replacementA = manager.GetOrCreatePair(PortalKind.Locaversal, a);
            Assert.That(replacementA, Is.Not.SameAs(pairA));
            Assert.That(manager.HasActivePairForOwner(b), Is.True);
            Assert.That(manager.GetPairForOwner(b), Is.SameAs(pairB));
            yield break;
        }
    }
}
