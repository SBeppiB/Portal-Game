using UnityEngine;

namespace Portals.Traversal
{
    /// <summary>
    /// Implement this on a movement controller so portal traversal can rotate its
    /// internal velocity/momentum state. The portal system handles the Transform and
    /// Rigidbody; the controller handles everything it cached privately.
    /// </summary>
    public interface IPortalMomentumReceiver
    {
        /// <summary>
        /// Called immediately after a teleport. Rotate every internal direction,
        /// velocity and momentum vector by <paramref name="rotation"/>.
        /// </summary>
        void ApplyPortalRotation(Quaternion rotation);
    }
}
