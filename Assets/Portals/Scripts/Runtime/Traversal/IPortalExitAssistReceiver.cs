using UnityEngine;

namespace Portals.Traversal
{
    /// <summary>
    /// Implemented by player movement controllers that can accept an additive velocity
    /// impulse after a portal traversal. PortalTraverser never uses this for Rigidbody
    /// objects; it is strictly the opt-in hook for character-controller players.
    /// Implementations must ADD the impulse so existing momentum is preserved.
    /// </summary>
    public interface IPortalExitAssistReceiver
    {
        void ApplyExitImpulse(Vector3 velocityImpulse);
    }
}
