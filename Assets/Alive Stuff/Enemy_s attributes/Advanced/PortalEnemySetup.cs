using Portals.Enemy;
using Portals.Traversal;
using UnityEngine;

namespace Portals.Integration
{
    /// <summary>
    /// Creates a fully wired advanced portal enemy for scene authoring and tests.
    /// Reuses the existing enemy movement/health components; no duplicate systems.
    /// </summary>
    public static class PortalEnemySetup
    {
        public static AdvancedPortalEnemy CreateEnemy(string name, Vector3 position, Transform player)
        {
            GameObject root = new GameObject(name);
            root.transform.SetPositionAndRotation(position, Quaternion.identity);

            CharacterController controller = root.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.4f;
            controller.center = new Vector3(0f, 1f, 0f);

            root.AddComponent<AudioSource>();
            EnemyMovement movement = root.AddComponent<EnemyMovement>();
            if (player != null) movement.player = player;
            root.AddComponent<EnemyHealth>();
            root.AddComponent<Pain>();
            root.AddComponent<PortalTraverser>();

            AdvancedPortalEnemy advanced = root.AddComponent<AdvancedPortalEnemy>();
            return advanced;
        }
    }
}
