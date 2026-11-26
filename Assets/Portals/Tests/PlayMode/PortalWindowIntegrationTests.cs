using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Portals.Core;
using Portals.Integration;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Portals.Tests
{
    /// <summary>
    /// End-to-end coverage of portal window integration. Loads MAINSCENE and verifies
    /// that each placed portal automatically receives a PortalWindowRenderer configured
    /// to render its paired destination's view, with correct lifecycle and cleanup.
    /// In headless batch mode (no GPU), portal rendering is skipped and the swirl
    /// fallback remains active; tests conditionally assert on that.
    /// </summary>
    public class PortalWindowIntegrationTests
    {
        private readonly List<GameObject> m_Tracked = new List<GameObject>();
        private bool m_Loaded;
        private bool m_IsBatchMode;
        private LocaversalPortal m_PortalA;
        private LocaversalPortal m_PortalB;

        private GameObject Track(GameObject gameObject)
        {
            m_Tracked.Add(gameObject);
            return gameObject;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            m_IsBatchMode = Application.isBatchMode;

            AsyncOperation load = SceneManager.LoadSceneAsync("MAINSCENE", LoadSceneMode.Single);
            Assert.IsNotNull(load, "MAINSCENE must be in Build Settings for this test.");
            yield return load;
            m_Loaded = true;

            // Let GameBootstrap / PortalSceneSetup settle.
            yield return null;
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject gameObject in m_Tracked)
                if (gameObject != null) UnityEngine.Object.DestroyImmediate(gameObject);
            m_Tracked.Clear();

            Scene cleanup = SceneManager.CreateScene("PortalWindowIntegrationCleanup");
            SceneManager.SetActiveScene(cleanup);

            if (m_Loaded)
            {
                Scene main = SceneManager.GetSceneByName("MAINSCENE");
                if (main.IsValid() && main.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(main);
            }
        }

        private IEnumerator ActivateAuthoredPair()
        {
            m_PortalA = GameObject.Find("Portal A")?.GetComponent<LocaversalPortal>();
            m_PortalB = GameObject.Find("Portal B")?.GetComponent<LocaversalPortal>();
            Assert.IsNotNull(m_PortalA, "Authored Portal A must exist after bootstrap.");
            Assert.IsNotNull(m_PortalB, "Authored Portal B must exist after bootstrap.");

            m_PortalA.transform.position = new Vector3(-5f, 2f, 0f);
            m_PortalB.transform.position = new Vector3(5f, 2f, 0f);
            m_PortalA.transform.rotation = Quaternion.identity;
            m_PortalB.transform.rotation = Quaternion.identity;

            m_PortalA.SetActive(true);
            m_PortalB.SetActive(true);

            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActivatedPortalsReceiveWindowRenderers()
        {
            yield return ActivateAuthoredPair();

            Assert.IsNotNull(m_PortalA.GetComponent<PortalWindowRenderer>(),
                "Portal A must have a PortalWindowRenderer when active.");
            Assert.IsNotNull(m_PortalB.GetComponent<PortalWindowRenderer>(),
                "Portal B must have a PortalWindowRenderer when active.");
        }

        [UnityTest]
        public IEnumerator PairedEndpointsReferenceCorrectDestination()
        {
            yield return ActivateAuthoredPair();

            var rendererA = m_PortalA.GetComponent<PortalWindowRenderer>();
            var rendererB = m_PortalB.GetComponent<PortalWindowRenderer>();

            Assert.IsNotNull(rendererA, "Portal A renderer must exist.");
            Assert.IsNotNull(rendererB, "Portal B renderer must exist.");

            Assert.AreSame(m_PortalB, rendererA.DestinationPortal,
                "Portal A's renderer must target Portal B.");
            Assert.AreSame(m_PortalA, rendererB.DestinationPortal,
                "Portal B's renderer must target Portal A.");
            Assert.AreSame(m_PortalA, rendererA.SourcePortal,
                "Portal A's renderer source must be Portal A.");
            Assert.AreSame(m_PortalB, rendererB.SourcePortal,
                "Portal B's renderer source must be Portal B.");
        }

        [UnityTest]
        public IEnumerator RenderTexturesAreCreatedAndAssigned()
        {
            yield return ActivateAuthoredPair();

            var rendererA = m_PortalA.GetComponent<PortalWindowRenderer>();
            var rendererB = m_PortalB.GetComponent<PortalWindowRenderer>();

            if (!m_IsBatchMode)
            {
                Assert.IsNotNull(rendererA.RenderTexture, "Portal A must have a RenderTexture.");
                Assert.IsNotNull(rendererB.RenderTexture, "Portal B must have a RenderTexture.");
                Assert.AreNotSame(rendererA.RenderTexture, rendererB.RenderTexture,
                    "Each portal must have its own RenderTexture.");
                Assert.IsTrue(rendererA.RenderTexture.IsCreated(),
                    "Portal A's RenderTexture must be created.");
                Assert.IsTrue(rendererB.RenderTexture.IsCreated(),
                    "Portal B's RenderTexture must be created.");
            }
            else
            {
                // In batch mode, rendering is skipped; RenderTexture may be null.
                // The renderer component still exists and is configured.
                Assert.IsNotNull(rendererA, "Portal A renderer must exist in batch mode.");
                Assert.IsNotNull(rendererB, "Portal B renderer must exist in batch mode.");
            }
        }

        [UnityTest]
        public IEnumerator HasPortalViewEnabledOnlyWithValidTexture()
        {
            yield return ActivateAuthoredPair();
            yield return null; // Let LateUpdate run at least once.

            var rendererA = m_PortalA.GetComponent<PortalWindowRenderer>();
            var rendererB = m_PortalB.GetComponent<PortalWindowRenderer>();

            var visualA = m_PortalA.GetComponent<PortalVisual>();
            var visualB = m_PortalB.GetComponent<PortalVisual>();

            var surfaceRendererA = visualA.SurfaceObject?.GetComponent<MeshRenderer>();
            var surfaceRendererB = visualB.SurfaceObject?.GetComponent<MeshRenderer>();

            Assert.IsNotNull(surfaceRendererA, "Portal A surface renderer must exist.");
            Assert.IsNotNull(surfaceRendererB, "Portal B surface renderer must exist.");

            float hasViewA = surfaceRendererA.sharedMaterial.GetFloat("_HasPortalView");
            float hasViewB = surfaceRendererB.sharedMaterial.GetFloat("_HasPortalView");

            if (!m_IsBatchMode)
            {
                Assert.Greater(hasViewA, 0.5f, "_HasPortalView must be enabled on Portal A.");
                Assert.Greater(hasViewB, 0.5f, "_HasPortalView must be enabled on Portal B.");

                Assert.AreSame(rendererA.RenderTexture,
                    surfaceRendererA.sharedMaterial.GetTexture("_PortalViewTex"),
                    "Portal A's surface must use its renderer's RenderTexture.");
                Assert.AreSame(rendererB.RenderTexture,
                    surfaceRendererB.sharedMaterial.GetTexture("_PortalViewTex"),
                    "Portal B's surface must use its renderer's RenderTexture.");
            }
            else
            {
                // In batch mode, rendering is skipped; swirl fallback remains active.
                Assert.AreEqual(0f, hasViewA,
                    "_HasPortalView must be 0 on Portal A in batch mode.");
                Assert.AreEqual(0f, hasViewB,
                    "_HasPortalView must be 0 on Portal B in batch mode.");
            }
        }

        [UnityTest]
        public IEnumerator ClosingPortalCleansUpRenderingResources()
        {
            yield return ActivateAuthoredPair();

            var rendererA = m_PortalA.GetComponent<PortalWindowRenderer>();
            var rendererB = m_PortalB.GetComponent<PortalWindowRenderer>();
            Assert.IsNotNull(rendererA, "Portal A renderer must exist before close.");
            Assert.IsNotNull(rendererB, "Portal B renderer must exist before close.");

            var visualA = m_PortalA.GetComponent<PortalVisual>();
            var surfaceRendererA = visualA.SurfaceObject?.GetComponent<MeshRenderer>();
            float hasViewBefore = surfaceRendererA.sharedMaterial.GetFloat("_HasPortalView");

            if (!m_IsBatchMode)
                Assert.Greater(hasViewBefore, 0.5f, "_HasPortalView must be enabled before close.");
            else
                Assert.AreEqual(0f, hasViewBefore,
                    "_HasPortalView must be 0 before close in batch mode.");

            m_PortalA.SetActive(false);
            yield return null;
            yield return null;

            Assert.IsFalse(rendererA.enabled,
                "Portal A's renderer must be disabled after close.");
            float hasViewAfter = surfaceRendererA.sharedMaterial.GetFloat("_HasPortalView");
            Assert.AreEqual(0f, hasViewAfter,
                "_HasPortalView must be reset to 0 after close.");

            Assert.IsNotNull(rendererB.RenderTexture,
                "Portal B's RenderTexture must persist after partner closes.");
        }

        [UnityTest]
        public IEnumerator ReusedPooledPortalsDontRetainStaleReferences()
        {
            var lifecycle = Object.FindFirstObjectByType<PortalLifecycleManager>();
            Assert.IsNotNull(lifecycle, "PortalLifecycleManager must exist.");

            lifecycle.CloseAll();
            yield return null;
            yield return null;

            var pair1 = lifecycle.GetOrCreatePair(PortalKind.Locaversal);
            Assert.IsNotNull(pair1, "First pair must be created.");

            var entrance1 = pair1.Entrance;
            var exit1 = pair1.Exit;

            entrance1.transform.position = new Vector3(-5f, 2f, 0f);
            exit1.transform.position = new Vector3(5f, 2f, 0f);
            entrance1.transform.rotation = Quaternion.identity;
            exit1.transform.rotation = Quaternion.identity;
            entrance1.SetActive(true);
            exit1.SetActive(true);
            yield return null;
            yield return null;

            var renderer1A = entrance1.GetComponent<PortalWindowRenderer>();
            var renderer1B = exit1.GetComponent<PortalWindowRenderer>();
            Assert.IsNotNull(renderer1A, "Entrance 1 must have a renderer.");
            Assert.IsNotNull(renderer1B, "Exit 1 must have a renderer.");

            lifecycle.ClosePair(pair1);
            yield return null;
            yield return null;

            var pair2 = lifecycle.GetOrCreatePair(PortalKind.Locaversal);
            Assert.IsNotNull(pair2, "Second pair must be created.");

            var entrance2 = pair2.Entrance;
            var exit2 = pair2.Exit;

            entrance2.transform.position = new Vector3(-3f, 2f, 0f);
            exit2.transform.position = new Vector3(3f, 2f, 0f);
            entrance2.transform.rotation = Quaternion.identity;
            exit2.transform.rotation = Quaternion.identity;
            entrance2.SetActive(true);
            exit2.SetActive(true);
            yield return null;
            yield return null;

            var renderer2A = entrance2.GetComponent<PortalWindowRenderer>();
            var renderer2B = exit2.GetComponent<PortalWindowRenderer>();
            Assert.IsNotNull(renderer2A, "Entrance 2 must have a renderer.");
            Assert.IsNotNull(renderer2B, "Exit 2 must have a renderer.");

            Assert.AreSame(exit2, renderer2A.DestinationPortal,
                "Reused entrance must target the new exit, not the old one.");
            Assert.AreSame(entrance2, renderer2B.DestinationPortal,
                "Reused exit must target the new entrance, not the old one.");
        }

        [UnityTest]
        public IEnumerator UnlinkedPortalHasNoWindowRenderer()
        {
            GameObject portalObject = Track(new GameObject("Standalone Portal"));
            portalObject.transform.position = new Vector3(0f, 2f, 0f);
            var portal = portalObject.AddComponent<LocaversalPortal>();
            portal.SetActive(true);

            yield return null;
            yield return null;

            Assert.IsNull(portal.GetComponent<PortalWindowRenderer>(),
                "An unlinked portal must not have a PortalWindowRenderer.");
        }

        [UnityTest]
        public IEnumerator BothDirectionsRenderCorrectly()
        {
            yield return ActivateAuthoredPair();
            yield return null; // Let LateUpdate run for both.

            var rendererA = m_PortalA.GetComponent<PortalWindowRenderer>();
            var rendererB = m_PortalB.GetComponent<PortalWindowRenderer>();

            Assert.AreSame(m_PortalB, rendererA.DestinationPortal,
                "Portal A renders Portal B's view.");
            Assert.AreSame(m_PortalA, rendererB.DestinationPortal,
                "Portal B renders Portal A's view.");

            var visualA = m_PortalA.GetComponent<PortalVisual>();
            var visualB = m_PortalB.GetComponent<PortalVisual>();
            var surfA = visualA.SurfaceObject?.GetComponent<MeshRenderer>();
            var surfB = visualB.SurfaceObject?.GetComponent<MeshRenderer>();

            Assert.IsNotNull(surfA, "Portal A surface must exist.");
            Assert.IsNotNull(surfB, "Portal B surface must exist.");

            if (!m_IsBatchMode)
            {
                Assert.AreSame(rendererA.RenderTexture, surfA.sharedMaterial.GetTexture("_PortalViewTex"),
                    "Portal A surface must show Portal A's render texture.");
                Assert.AreSame(rendererB.RenderTexture, surfB.sharedMaterial.GetTexture("_PortalViewTex"),
                    "Portal B surface must show Portal B's render texture.");

                Assert.Greater(surfA.sharedMaterial.GetFloat("_HasPortalView"), 0.5f,
                    "Portal A must show destination view.");
                Assert.Greater(surfB.sharedMaterial.GetFloat("_HasPortalView"), 0.5f,
                    "Portal B must show destination view.");
            }
            else
            {
                Assert.AreEqual(0f, surfA.sharedMaterial.GetFloat("_HasPortalView"),
                    "Portal A must show swirl fallback in batch mode.");
                Assert.AreEqual(0f, surfB.sharedMaterial.GetFloat("_HasPortalView"),
                    "Portal B must show swirl fallback in batch mode.");
            }
        }
    }
}
