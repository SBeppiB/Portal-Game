using System;
using System.Collections.Generic;
using System.Reflection;
using Game;
using NUnit.Framework;
using Portals.Save;
using UnityEngine;

namespace Portals.Tests
{
    /// <summary>
    /// EditMode coverage for the gameplay layer: crafting, home base, universe
    /// selection and save/load round-trips. Game components live in Game.Runtime,
    /// which this assembly references.
    /// </summary>
    public class GameSystemsEditModeTests
    {
        private GameObject m_Root;
        private readonly List<ScriptableObject> m_Assets = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            m_Root = new GameObject("GameSystemsTestRoot");
            SaveStateRegistry.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            SaveStateRegistry.Clear();
            if (m_Root != null) UnityEngine.Object.DestroyImmediate(m_Root);
            foreach (ScriptableObject asset in m_Assets)
                if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            m_Assets.Clear();
        }

        // ------------------------------------------------------------------
        // Crafting
        // ------------------------------------------------------------------

        [Test]
        public void Crafting_ConsumesInputsAndUnlocksTuner()
        {
            var scrap = CreateResource("scrap", "Scrap Metal");
            var copper = CreateResource("copper", "Copper Wire");
            var crystal = CreateResource("crystal", "Portal Crystal");
            var recipe = CreateRecipe("TunerMk2", "Portal Tuner Mk2",
                new[] { (scrap, 20), (copper, 10), (crystal, 5) }, unlockRing: 2);

            var station = NewComponent<CraftingStation>("Station");
            var inventory = NewComponent<ResourceInventory>("HomeStorage");
            var tuner = NewComponent<PortalTuner>("Tuner");

            SetField(station, "m_Inventory", inventory);
            SetField(station, "m_Tuner", tuner);
            SetField(station, "m_Recipes", new List<CraftingRecipe> { recipe });

            Assert.IsFalse(station.CanCraft(recipe), "Empty inventory must not be craftable.");

            inventory.Add(scrap, 20);
            inventory.Add(copper, 10);
            inventory.Add(crystal, 5);

            Assert.IsTrue(station.CanCraft(recipe), "Full materials should be craftable.");
            Assert.IsTrue(station.Craft(recipe), "Craft should succeed with materials.");

            Assert.AreEqual(0, inventory.GetAmount(scrap));
            Assert.AreEqual(0, inventory.GetAmount(copper));
            Assert.AreEqual(0, inventory.GetAmount(crystal));
            Assert.AreEqual(PortalTuner.MaxRingForTier(2), tuner.MaxRing,
                "Crafting the Mk2 recipe should unlock ring tier 2.");
        }

        [Test]
        public void Crafting_BlockedByLockedTuner()
        {
            var crystal = CreateResource("crystal", "Portal Crystal");
            var recipe = CreateRecipe("TunerMk3", "Portal Tuner Mk3",
                new[] { (crystal, 10) }, unlockRing: 3);
            SetField(recipe, "m_RequiredRing", 3);

            var station = NewComponent<CraftingStation>("Station");
            var inventory = NewComponent<ResourceInventory>("HomeStorage");
            var tuner = NewComponent<PortalTuner>("Tuner");

            SetField(station, "m_Inventory", inventory);
            SetField(station, "m_Tuner", tuner);
            SetField(station, "m_Recipes", new List<CraftingRecipe> { recipe });

            inventory.Add(crystal, 10);
            Assert.IsFalse(station.CanCraft(recipe), "A locked tuner ring must block crafting.");

            tuner.UnlockRingTier(3);
            Assert.AreEqual(PortalTuner.MaxRingForTier(3), tuner.MaxRing);
            Assert.IsTrue(station.CanCraft(recipe), "Unlocking the ring should allow crafting.");
        }

        // ------------------------------------------------------------------
        // Home base
        // ------------------------------------------------------------------

        [Test]
        public void HomeBase_DepositsInventoryAndTracksStateVersion()
        {
            var resource = CreateResource("scrap", "Scrap Metal");

            var home = NewComponent<HomeBaseManager>("Home Base");
            var storage = NewComponent<ResourceInventory>("HomeStorage");
            SetField(home, "m_Storage", storage);

            var playerInventory = NewComponent<ResourceInventory>("Player");
            playerInventory.Add(resource, 7);

            int before = home.StateVersion;
            home.Deposit(playerInventory);

            Assert.AreEqual(7, storage.GetAmount(resource), "Deposited resources must reach storage.");
            Assert.AreEqual(0, playerInventory.GetAmount(resource), "Source inventory must be emptied.");
            Assert.AreNotEqual(before, home.StateVersion, "Storage version must change after a deposit.");

            int afterDeposit = home.StateVersion;
            home.Discover(new UniverseCoordinate(2, 3, 4));
            Assert.AreEqual(1, home.Discovered.Count);
            Assert.AreNotEqual(afterDeposit, home.StateVersion, "Discovering a coordinate must change the signature.");
        }

        // ------------------------------------------------------------------
        // Save / load
        // ------------------------------------------------------------------

        [Test]
        public void ResourceInventory_RoundTripsWithPerOwnerIsolation()
        {
            var scrap = CreateResource("scrap", "Scrap Metal");
            var copper = CreateResource("copper", "Copper Wire");
            var database = CreateDatabase(scrap, copper);

            var player = NewComponent<ResourceInventory>("Player");
            var home = NewComponent<ResourceInventory>("Home Base");
            SetField(player, "m_Database", database);
            SetField(home, "m_Database", database);

            player.Add(scrap, 3);
            home.Add(copper, 7);

            var data = new SaveGameData();
            player.CaptureInto(data);
            home.CaptureInto(data);

            var player2 = NewComponent<ResourceInventory>("Player");
            var home2 = NewComponent<ResourceInventory>("Home Base");
            SetField(player2, "m_Database", database);
            SetField(home2, "m_Database", database);

            Assert.IsTrue(player2.ApplyFrom(data));
            Assert.IsTrue(home2.ApplyFrom(data));

            Assert.AreEqual(3, player2.GetAmount(scrap), "Player inventory must restore its own scrap.");
            Assert.AreEqual(0, player2.GetAmount(copper), "Player inventory must not inherit home copper.");
            Assert.AreEqual(7, home2.GetAmount(copper), "Home storage must restore its own copper.");
            Assert.AreEqual(0, home2.GetAmount(scrap), "Home storage must not inherit player scrap.");
        }

        [Test]
        public void PortalTuner_RoundTripsProgression()
        {
            var tuner = NewComponent<PortalTuner>("Tuner");
            tuner.UnlockRingTier(3);
            tuner.UnlockLayer(2);

            var data = new SaveGameData();
            tuner.CaptureInto(data);

            var restored = NewComponent<PortalTuner>("Tuner");
            Assert.IsTrue(restored.ApplyFrom(data));

            Assert.AreEqual(tuner.MaxRing, restored.MaxRing);
            Assert.AreEqual(tuner.MaxLayer, restored.MaxLayer);
            Assert.IsTrue(restored.CanAccess(6, 2));
        }

        [Test]
        public void HomeBase_DiscoveredCoordinatesRoundTrip()
        {
            var home = NewComponent<HomeBaseManager>("Home Base");
            home.Discover(new UniverseCoordinate(2, 3, 4));

            var data = new SaveGameData();
            home.CaptureInto(data);

            var restored = NewComponent<HomeBaseManager>("Home Base");
            Assert.IsTrue(restored.ApplyFrom(data));

            Assert.AreEqual(1, restored.Discovered.Count);
            Assert.AreEqual(new UniverseCoordinate(2, 3, 4).ToString(), restored.Discovered[0]);
        }

        // ------------------------------------------------------------------
        // Universe selection
        // ------------------------------------------------------------------

        [Test]
        public void UniverseSelection_RejectsLockedCoordinatesThenAllowsUnlocked()
        {
            var tuner = NewComponent<PortalTuner>("Tuner");
            var manager = NewComponent<UniverseManager>("Universe Manager");
            var ui = NewComponent<UniverseSelectionUI>("Universe Selection UI");

            SetField(ui, "m_Tuner", tuner);
            SetField(ui, "m_UniverseManager", manager);
            SetField(manager, "m_ContentParent", m_Root.transform);

            Assert.IsFalse(ui.TrySelect(0, 5, 0), "Ring 5 is locked at MaxRing 2.");
            Assert.AreEqual(UniverseCoordinate.Prime, manager.Current);

            tuner.UnlockRingTier(3);
            Assert.IsTrue(ui.TrySelect(0, 5, 0), "Ring 5 should be reachable at MaxRing 6.");
            Assert.AreEqual(new UniverseCoordinate(0, 5, 0), manager.Current);
        }

        [Test]
        public void UniverseManager_EntersCoordinatesAndSwitchesRoots()
        {
            var manager = NewComponent<UniverseManager>("Universe Manager");
            var generator = NewComponent<UniverseContentGenerator>("Generator");
            var home = NewComponent<HomeBaseManager>("Home Base");
            SetField(manager, "m_Generator", generator);
            SetField(manager, "m_HomeBase", home);
            SetField(manager, "m_ContentParent", m_Root.transform);

            manager.EnterUniverse("L01-R01-P001");
            Assert.AreEqual(new UniverseCoordinate(1, 1, 1), manager.Current);

            manager.EnterUniverse("L01-R02-P002");
            Assert.AreEqual(new UniverseCoordinate(1, 2, 2), manager.Current);
            Assert.AreEqual(2, home.Discovered.Count);
        }

        // ------------------------------------------------------------------
        // Menu host
        // ------------------------------------------------------------------

        [Test]
        public void GameMenuController_EnsureAttachedIsIdempotent()
        {
            var host = new GameObject("Portal Game");
            try
            {
                GameMenuController.EnsureAttached();
                GameMenuController.EnsureAttached();

                Assert.AreEqual(1, host.GetComponents<GameMenuController>().Length,
                    "EnsureAttached must not add duplicate menu controllers.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private T NewComponent<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(m_Root.transform, false);
            return go.AddComponent<T>();
        }

        private ResourceDefinition CreateResource(string id, string displayName)
        {
            var resource = ScriptableObject.CreateInstance<ResourceDefinition>();
            SetField(resource, "m_Id", id);
            SetField(resource, "m_DisplayName", displayName);
            SetField(resource, "m_Tier", 0);
            m_Assets.Add(resource);
            return resource;
        }

        private CraftingRecipe CreateRecipe(string id, string displayName,
            (ResourceDefinition resource, int amount)[] inputs, int unlockRing)
        {
            var recipe = ScriptableObject.CreateInstance<CraftingRecipe>();
            SetField(recipe, "m_Id", id);
            SetField(recipe, "m_DisplayName", displayName);
            SetField(recipe, "m_OutputAmount", 1);
            SetField(recipe, "m_UnlockRingAccess", unlockRing);

            var list = new List<CraftingIngredient>();
            foreach (var (resource, amount) in inputs)
                list.Add(new CraftingIngredient { resource = resource, amount = amount });
            SetField(recipe, "m_Inputs", list);

            m_Assets.Add(recipe);
            return recipe;
        }

        private ResourceDatabase CreateDatabase(params ResourceDefinition[] resources)
        {
            var database = ScriptableObject.CreateInstance<ResourceDatabase>();
            SetField(database, "m_Resources", new List<ResourceDefinition>(resources));
            m_Assets.Add(database);
            return database;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            Assert.IsNotNull(target, "Cannot set field on a null target.");
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}