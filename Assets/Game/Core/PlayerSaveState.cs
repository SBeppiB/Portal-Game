using UnityEngine;
using Portals.Save;

namespace Game
{
    /// <summary>
    /// Persists the player's world position and rotation. Attached to the player by
    /// <see cref="GameBootstrap"/> so the player is restored exactly where they saved.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerSaveState : MonoBehaviour, IGameplaySaveState
    {
        private void OnEnable() => SaveStateRegistry.Register(this);
        private void OnDisable() => SaveStateRegistry.Unregister(this);

        public void CaptureInto(SaveGameData data)
        {
            if (data.player == null) data.player = new PlayerStateData();

            Vector3 position = transform.position;
            Quaternion rotation = transform.rotation;

            data.player.hasTransform = true;
            data.player.px = position.x;
            data.player.py = position.y;
            data.player.pz = position.z;
            data.player.rx = rotation.x;
            data.player.ry = rotation.y;
            data.player.rz = rotation.z;
            data.player.rw = rotation.w;
        }

        public bool ApplyFrom(SaveGameData data)
        {
            if (data == null || data.player == null || !data.player.hasTransform) return true;

            var controller = GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;

            transform.SetPositionAndRotation(
                new Vector3(data.player.px, data.player.py, data.player.pz),
                new Quaternion(data.player.rx, data.player.ry, data.player.rz, data.player.rw));

            if (controller != null) controller.enabled = true;
            Physics.SyncTransforms();
            return true;
        }
    }
}