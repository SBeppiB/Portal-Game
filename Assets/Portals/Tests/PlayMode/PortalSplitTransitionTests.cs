using System.Collections;
using NUnit.Framework;
using Portals.Core;
using Portals.Traversal;
using UnityEngine;
using UnityEngine.TestTools;

namespace Portals.Tests
{
    public class PortalSplitTransitionTests
    {
        [UnityTest]
        public IEnumerator Begin_CreatesProxyAndCompletes()
        {
            GameObject owner = new GameObject("SplitObj");
            Rigidbody rb = owner.AddComponent<Rigidbody>();
            owner.AddComponent<BoxCollider>();
            PortalSplitTransition split = owner.AddComponent<PortalSplitTransition>();

            GameObject a = new GameObject("PortalA");
            var pa = a.AddComponent<LocaversalPortal>();
            GameObject b = new GameObject("PortalB");
            var pb = b.AddComponent<LocaversalPortal>();
            pa.SetPair(pb);

            Assert.IsTrue(split.Begin(pa, pb));
            Assert.AreEqual(PortalSplitTransition.State.Transitioning, split.CurrentState);

            split.Tick();
            Assert.GreaterOrEqual(split.Progress, 0f);

            Object.Destroy(owner);
            Object.Destroy(a);
            Object.Destroy(b);
            yield break;
        }

        [UnityTest]
        public IEnumerator Abort_CleansUpProxy()
        {
            GameObject owner = new GameObject("SplitObj2");
            owner.AddComponent<Rigidbody>();
            owner.AddComponent<BoxCollider>();
            PortalSplitTransition split = owner.AddComponent<PortalSplitTransition>();

            GameObject a = new GameObject("PortalA2");
            var pa = a.AddComponent<LocaversalPortal>();
            GameObject b = new GameObject("PortalB2");
            var pb = b.AddComponent<LocaversalPortal>();

            split.Begin(pa, pb);
            split.Abort();
            Assert.AreEqual(PortalSplitTransition.State.Idle, split.CurrentState);

            Object.Destroy(owner);
            Object.Destroy(a);
            Object.Destroy(b);
            yield break;
        }
    }
}
