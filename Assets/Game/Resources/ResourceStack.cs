using System;
using UnityEngine;

namespace Game
{
    [Serializable]
    public class ResourceStack
    {
        public ResourceDefinition resource;
        public int amount;

        public ResourceStack() { }

        public ResourceStack(ResourceDefinition resource, int amount)
        {
            this.resource = resource;
            this.amount = Mathf.Max(0, amount);
        }

        public string Id => resource != null ? resource.Id : string.Empty;
    }
}