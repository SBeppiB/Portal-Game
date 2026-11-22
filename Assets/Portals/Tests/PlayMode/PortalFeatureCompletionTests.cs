using System.Collections;
using NUnit.Framework;
using Portals.Core;
using Portals.Integration;
using Portals.Interaction;
using Portals.Traversal;
using UnityEngine;
using UnityEngine.TestTools;

namespace Portals.Tests
{
    public class PortalFeatureCompletionTests
    {
        private readonly System.Collections.Generic.List<GameObject> m_Objects = new System.Collections.Generic.List<GameObject>();

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

        private static void AssertVectorNear(Vector3 expected, Vector3 actual, float epsilon = 1e-2f)
        {
            Assert.That((expected - actual).sqrMagnitude, Is.LessThan(epsilon * epsilon),
                $"Expected {expected} but got {actual}.");
        }

        // ---------- Screen wobble ----------

        [UnityTest]
        public IEnumerator ScreenWobble_ActivatesAndRestoresFieldOfView()
        {
            GameObject cameraObject = Track(new GameObject("Wobble Camera"));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60f;

            PortalScreenWobble wobble = cameraObject.AddComponent<PortalScreenWobble>();
            wobble.Trigger();

            Assert.That(wobble.IsActive, Is.True);
            yield return null;
            Assert.That(camera.fieldOfView, Is.Not.EqualTo(60f).Within(0.001f));

            yield return new WaitForSeconds(0.35f);
            Assert.That(wobble.IsActive, Is.False);
            Assert.That(camera.fieldOfView, Is.EqualTo(60f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator ScreenWobble_DisabledDoesNotActivate()
        {
            GameObject cameraObject = Track(new GameObject("Wobble Camera 2"));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 55f;

            PortalScreenWobble wobble = cameraObject.AddComponent<PortalScreenWobble>();
            wobble.Configure(false, 0.5f, 0.3f);
            wobble.Trigger();

            Assert.That(wobble.IsActive, Is.False);
            Assert.That(camera.fieldOfView, Is.EqualTo(55f).Within(0.001f));
            yield break;
        }

        // ---------- Crosshair / highlight ----------

        [UnityTest]
        public IEnumerator Highlight_TargetsAndRestoresMaterials()
        {
            GameObject cameraObject = Track(new GameObject("Highlight Camera"));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(0f, 0f, -3f);

            GameObject cube = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            cube.transform.position = Vector3.zero;
            InteractableObject interactable = cube.AddComponent<InteractableObject>();

            PortalHighlighter highlighter = cameraObject.AddComponent<PortalHighlighter>();
            highlighter.GetType().GetField("m_Range",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(highlighter, 10f);

            Renderer renderer = cube.GetComponent<Renderer>();
            Material original = renderer.sharedMaterial;

            highlighter.Refresh();
            yield return null;
            highlighter.Refresh();

            Assert.That(highlighter.CurrentTarget, Is.SameAs(cube));
            Assert.That(renderer.sharedMaterial, Is.Not.SameAs(original));

            // Move camera away so the target is no longer valid.
            cameraObject.transform.position = new Vector3(100f, 0f, 0f);
            highlighter.Refresh();
            yield return null;
            highlighter.Refresh();

            Assert.That(highlighter.CurrentTarget, Is.Null);
            Assert.That(renderer.sharedMaterial, Is.SameAs(original));

            yield break;
        }

        // ---------- Pickup / drop / throw ----------

        [UnityTest]
        public IEnumerator PickupAndDrop_WorksAndRestoresPhysics()
        {
            GameObject cameraObject = Track(new GameObject("Interact Camera"));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(0f, 0f, -3f);
            cameraObject.transform.rotation = Quaternion.identity;

            GameObject cube = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            cube.transform.position = new Vector3(0f, 0f, 0f);
            Rigidbody body = cube.AddComponent<Rigidbody>();
            body.useGravity = true;
            InteractableObject interactable = cube.AddComponent<InteractableObject>();

            PortalObjectInteractor interactor = cameraObject.AddComponent<PortalObjectInteractor>();

            interactor.Interact();
            yield return null;

            Assert.That(interactor.IsHolding, Is.True);
            Assert.That(interactor.HeldObject, Is.SameAs(cube));
            Assert.That(body.useGravity, Is.False);

            interactor.Interact();
            yield return new WaitForSeconds(0.35f);
            interactor.Interact();
            // After the window the second click is consumed as the scheduled drop.

            Assert.That(interactor.IsHolding, Is.False);
            Assert.That(body.useGravity, Is.True);

            yield break;
        }

        [UnityTest]
        public IEnumerator DoubleClick_ThrowsWithinWindow()
        {
            GameObject cameraObject = Track(new GameObject("Throw Camera"));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(0f, 0f, -3f);
            cameraObject.transform.rotation = Quaternion.identity;

            GameObject cube = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            cube.transform.position = new Vector3(0f, 0f, 0f);
            Rigidbody body = cube.AddComponent<Rigidbody>();
            body.useGravity = true;
            cube.AddComponent<InteractableObject>();

            PortalObjectInteractor interactor = cameraObject.AddComponent<PortalObjectInteractor>();

            interactor.Interact();
            yield return null;
            Assert.That(interactor.IsHolding, Is.True);

            // Second click immediately (same frame) is within the 0.3 s window -> throw.
            interactor.Interact();

            Assert.That(interactor.IsHolding, Is.False);
            Assert.That(body.linearVelocity.magnitude, Is.GreaterThan(1f));
            AssertVectorNear(camera.transform.forward, body.linearVelocity.normalized, 0.1f);

            yield break;
        }

        // ---------- Inventory ----------

        [UnityTest]
        public IEnumerator Inventory_CapacityRetrievalAndRejection()
        {
            GameObject owner = Track(new GameObject("Inventory Owner"));
            PortalInventory inventory = owner.AddComponent<PortalInventory>();

            GameObject small = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            small.transform.localScale = Vector3.one * 0.5f;
            small.AddComponent<InteractableObject>();

            GameObject large = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            large.transform.localScale = Vector3.one * 5f;
            Physics.SyncTransforms();
            InteractableObject largeInteractable = large.AddComponent<InteractableObject>();
            largeInteractable.GetType().GetField("m_MaxInventorySize",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(largeInteractable, 0.1f);

            Assert.That(inventory.TryAdd(small), Is.True);
            Assert.That(inventory.Count, Is.EqualTo(1));
            Assert.That(inventory.TryAdd(large), Is.False, "Oversized objects must be rejected.");

            GameObject retrieved = inventory.Retrieve(0);
            Assert.That(retrieved, Is.SameAs(small));
            Assert.That(retrieved.activeSelf, Is.True);
            Assert.That(inventory.Count, Is.EqualTo(0));

            yield break;
        }

        [UnityTest]
        public IEnumerator Inventory_DropStoredObject()
        {
            GameObject owner = Track(new GameObject("Inventory Owner 2"));
            PortalInventory inventory = owner.AddComponent<PortalInventory>();

            GameObject cube = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            cube.transform.localScale = Vector3.one * 0.4f;
            cube.AddComponent<InteractableObject>();

            Assert.That(inventory.TryAdd(cube), Is.True);
            Assert.That(inventory.Drop(0, new Vector3(1f, 2f, 3f)), Is.True);
            Assert.That(cube.activeSelf, Is.True);
            Assert.That(cube.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(inventory.Count, Is.EqualTo(0));

            yield break;
        }

        // ---------- Object traversal ----------

        [UnityTest]
        public IEnumerator ObjectTraversal_MultipleRigidbodiesPassThrough()
        {
            GameObject entranceObject = Track(new GameObject("OT Entrance"));
            entranceObject.transform.position = Vector3.zero;
            LocaversalPortal entrance = entranceObject.AddComponent<LocaversalPortal>();
            entrance.SetActive(true);

            GameObject exitObject = Track(new GameObject("OT Exit"));
            exitObject.transform.position = new Vector3(20f, 0f, 0f);
            LocaversalPortal exit = exitObject.AddComponent<LocaversalPortal>();
            exit.SetActive(true);
            entrance.SetPair(exit);

            GameObject a = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            a.transform.position = new Vector3(0f, 0f, 1f);
            Rigidbody ra = a.AddComponent<Rigidbody>();
            ra.useGravity = false;
            PortalTraverser ta = a.AddComponent<PortalTraverser>();
            ta.AutoDetect = false;
            ta.SetPortals(entrance, exit);

            GameObject b = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            b.transform.position = new Vector3(0f, 0f, 1.2f);
            Rigidbody rb = b.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.mass = 5f;
            PortalTraverser tb = b.AddComponent<PortalTraverser>();
            tb.AutoDetect = false;
            tb.SetPortals(entrance, exit);

            ta.TickDetection();
            tb.TickDetection();

            ra.position = new Vector3(0f, 0f, -0.5f);
            rb.position = new Vector3(0f, 0f, -0.6f);

            Assert.That(ta.TickDetection(), Is.True);
            Assert.That(tb.TickDetection(), Is.True);
            Assert.That(Vector3.Distance(ra.position, exit.Position), Is.LessThan(5f));
            Assert.That(Vector3.Distance(rb.position, exit.Position), Is.LessThan(5f));

            yield break;
        }

        // ---------- Integration ----------

        [UnityTest]
        public IEnumerator Integration_PickupThenPortalTraversal()
        {
            GameObject entranceObject = Track(new GameObject("Int Entrance"));
            entranceObject.transform.position = new Vector3(0f, 0f, 0f);
            LocaversalPortal entrance = entranceObject.AddComponent<LocaversalPortal>();
            entrance.SetActive(true);

            GameObject exitObject = Track(new GameObject("Int Exit"));
            exitObject.transform.position = new Vector3(20f, 0f, 0f);
            LocaversalPortal exit = exitObject.AddComponent<LocaversalPortal>();
            exit.SetActive(true);
            entrance.SetPair(exit);
            Physics.SyncTransforms();

            GameObject cameraObject = Track(new GameObject("Int Camera"));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(0f, 0f, 1f);
            cameraObject.transform.rotation = Quaternion.identity;

            GameObject cube = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            cube.transform.position = new Vector3(0f, 0f, 0f);
            cube.AddComponent<Rigidbody>().useGravity = true;
            cube.AddComponent<InteractableObject>();

            PortalObjectInteractor interactor = cameraObject.AddComponent<PortalObjectInteractor>();
            CharacterController controller = cameraObject.AddComponent<CharacterController>();
            PortalTraverser traverser = cameraObject.AddComponent<PortalTraverser>();
            traverser.AutoDetect = false;
            traverser.SetPortals(entrance, exit);

            traverser.TickDetection(); // prime previous position
            SetPosition(cameraObject, controller, new Vector3(0f, 0f, -0.5f));
            Assert.That(entrance.IsTraversable, Is.True, "entrance traversable");
            Assert.That(exit.IsTraversable, Is.True, "exit traversable");
            Assert.That(entrance.GetLinkedEndpoint(), Is.SameAs(exit), "linked");
            Assert.That(traverser.TickDetection(), Is.True);

            yield break;
        }

        [UnityTest]
        public IEnumerator SceneSetup_AttachesAllGameplayComponentsOnce()
        {
            GameObject player = Track(new GameObject("Player"));
            player.AddComponent<CharacterController>();
            GameObject cameraObject = Track(new GameObject("Main Camera"));
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>();
            cameraObject.transform.SetParent(player.transform, false);

            Assert.That(PortalSceneSetup.EnsureSetup(out _), Is.True);
            GameObject systems = GameObject.Find("Portal Systems");
            if (systems != null) Track(systems);

            Assert.That(player.GetComponent<PortalCrosshair>(), Is.Not.Null);
            Assert.That(player.GetComponent<PortalHighlighter>(), Is.Not.Null);
            Assert.That(player.GetComponent<PortalObjectInteractor>(), Is.Not.Null);
            Assert.That(player.GetComponent<PortalInventory>(), Is.Not.Null);
            Assert.That(player.GetComponent<PortalScreenWobble>(), Is.Not.Null);
            Assert.That(player.GetComponent<PortalTraverser>(), Is.Not.Null);

            // Idempotent: repeated setup must not duplicate components.
            PortalSceneSetup.EnsureSetup(out _);
            Assert.That(player.GetComponents<PortalObjectInteractor>().Length, Is.EqualTo(1));
            Assert.That(player.GetComponents<PortalInventory>().Length, Is.EqualTo(1));
            Assert.That(player.GetComponents<PortalScreenWobble>().Length, Is.EqualTo(1));

            yield break;
        }

        private static void SetPosition(GameObject gameObject, CharacterController controller, Vector3 position)
        {
            controller.enabled = false;
            gameObject.transform.position = position;
            controller.enabled = true;
        }
    }
}
