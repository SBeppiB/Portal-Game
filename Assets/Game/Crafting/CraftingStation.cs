using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public class CraftingStation : MonoBehaviour
    {
        [SerializeField] private List<CraftingRecipe> m_Recipes = new List<CraftingRecipe>();
        [SerializeField] private ResourceInventory m_Inventory;
        [SerializeField] private PortalTuner m_Tuner;

        public IReadOnlyList<CraftingRecipe> Recipes => m_Recipes;

        public ResourceInventory Inventory => m_Inventory;

        public PortalTuner Tuner => m_Tuner;

        public bool CanCraft(CraftingRecipe recipe) =>
            recipe != null && recipe.CanCraft(m_Inventory, m_Tuner);

        public bool Craft(CraftingRecipe recipe)
        {
            if (!CanCraft(recipe)) return false;
            bool ok = recipe.Craft(m_Inventory, m_Tuner);
            if (ok && m_Tuner != null)
            {
                if (recipe.UnlockRingAccess > 0) m_Tuner.UnlockRingTier(recipe.UnlockRingAccess);
                if (recipe.UnlockLayerAccess > 0) m_Tuner.UnlockLayer(recipe.UnlockLayerAccess);
            }
            return ok;
        }
    }
}