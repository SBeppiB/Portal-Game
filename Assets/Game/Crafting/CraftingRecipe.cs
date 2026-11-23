using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    [Serializable]
    public class CraftingIngredient
    {
        public ResourceDefinition resource;
        public int amount;
    }

    [CreateAssetMenu(menuName = "Game/Crafting Recipe", fileName = "Recipe_")]
    public class CraftingRecipe : ScriptableObject
    {
        [SerializeField] private string m_Id;
        [SerializeField] private string m_DisplayName;
        [SerializeField] private List<CraftingIngredient> m_Inputs = new List<CraftingIngredient>();
        [SerializeField] private ResourceDefinition m_Output;
        [SerializeField, Min(1)] private int m_OutputAmount = 1;
        [SerializeField] private int m_RequiredRing;
        [SerializeField] private int m_RequiredLayer;
        [SerializeField] private int m_UnlockRingAccess;
        [SerializeField] private int m_UnlockLayerAccess;

        public string Id => string.IsNullOrEmpty(m_Id) ? name : m_Id;
        public string DisplayName => string.IsNullOrEmpty(m_DisplayName) ? name : m_DisplayName;
        public IReadOnlyList<CraftingIngredient> Inputs => m_Inputs;
        public ResourceDefinition Output => m_Output;
        public int OutputAmount => m_OutputAmount;
        public int RequiredRing => m_RequiredRing;
        public int RequiredLayer => m_RequiredLayer;
        public int UnlockRingAccess => m_UnlockRingAccess;
        public int UnlockLayerAccess => m_UnlockLayerAccess;

        public bool CanCraft(ResourceInventory inventory, PortalTuner tuner)
        {
            if (inventory == null) return false;
            if (tuner != null && !tuner.CanAccess(m_RequiredRing, m_RequiredLayer)) return false;
            foreach (var ing in m_Inputs)
            {
                if (ing == null || ing.resource == null) return false;
                if (inventory.GetAmount(ing.resource) < ing.amount) return false;
            }
            return true;
        }

        public bool Craft(ResourceInventory inventory, PortalTuner tuner)
        {
            if (!CanCraft(inventory, tuner)) return false;
            foreach (var ing in m_Inputs) inventory.Remove(ing.resource, ing.amount);
            if (m_Output != null) inventory.Add(m_Output, m_OutputAmount);
            return true;
        }
    }
}