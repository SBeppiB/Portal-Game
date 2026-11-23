using System.Collections;
using System.Reflection;
using Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Portals.Tests
{
    /// <summary>
    /// PlayMode coverage for the self-instantiating gameplay UI. Verifies that the
    /// crafting panel toggles, the home readout builds its own Canvas, the universe
    /// selection panel builds its Canvas and drives a transition, and the menu host
    /// attaches exactly once.
    /// </summary>
    public class GameUIFlowTests
    {
        private GameObject m_Root;

        [SetUp]
        public void SetUp()
        {
            m_Root = new GameObject("GameUIFlowTestRoot");
        }

        [TearDown]
        public void TearDown()
        {
            if (m_Root != null) Object.Destroy(m_Root);
        }

        [UnityTest]
        public IEnumerator CraftingUI_OpensAndCloses()
        {
            var stationGo = new GameObject("Station");
            stationGo.AddComponent<CraftingStation>();

            var uiGo = new GameObject("CraftingUI");
            var ui = uiGo.AddComponent<CraftingUI>();
            yield return null;

            ui.SetOpen(true);
            Assert.IsTrue(ui.IsOpen, "Crafting UI should report open.");
            yield return null;

            ui.SetOpen(false);
            Assert.IsFalse(ui.IsOpen, "Crafting UI should report closed.");

            Object.Destroy(uiGo);
            Object.Destroy(stationGo);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HomeBaseUI_SelfInstantiatesCanvas()
        {
            var homeGo = new GameObject("Home Base");
            homeGo.AddComponent<HomeBaseManager>();

            HomeBaseUI ui = HomeBaseUI.Create();
            Assert.IsNotNull(ui, "HomeBaseUI.Create must return an instance.");

            yield return null;
            Assert.IsNotNull(ui.GetComponentInChildren<Canvas>(),
                "HomeBaseUI must build its own Canvas when not wired.");

            Object.Destroy(ui.gameObject);
            Object.Destroy(homeGo);
            yield return null;
        }

        [UnityTest]
        public IEnumerator UniverseSelectionUI_BuildsCanvasAndDrivesTransition()
        {
            var tunerGo = new GameObject("Tuner");
            tunerGo.AddComponent<PortalTuner>();

            var managerGo = new GameObject("Universe Manager");
            var manager = managerGo.AddComponent<UniverseManager>();
            SetField(manager, "m_ContentParent", m_Root.transform);

            UniverseSelectionUI ui = UniverseSelectionUI.Create();
            SetField(ui, "m_Tuner", tunerGo.GetComponent<PortalTuner>());
            SetField(ui, "m_UniverseManager", manager);

            yield return null;
            Assert.IsNotNull(ui.GetComponentInChildren<Canvas>(true),
                "UniverseSelectionUI must build its own Canvas when not wired.");

            bool ok = ui.TrySelect(0, 1, 2);
            Assert.IsTrue(ok, "An accessible coordinate should be selectable.");
            Assert.AreEqual(new UniverseCoordinate(0, 1, 2), manager.Current);

            Object.Destroy(ui.gameObject);
            Object.Destroy(tunerGo);
            Object.Destroy(managerGo);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameMenuController_AttachesOnceToHost()
        {
            var host = GameObject.Find("Portal Game");
            if (host == null)
            {
                host = new GameObject("Portal Game");
                Object.DontDestroyOnLoad(host);
            }

            GameMenuController.EnsureAttached();
            GameMenuController.EnsureAttached();

            Assert.AreEqual(1, host.GetComponents<GameMenuController>().Length,
                "EnsureAttached must not add duplicate menu controllers.");

            Object.Destroy(host.GetComponent<GameMenuController>());
            yield return null;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}