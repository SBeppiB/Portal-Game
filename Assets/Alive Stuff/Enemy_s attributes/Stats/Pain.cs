using UnityEngine;

public class Pain : MonoBehaviour
{
    [Header("Damage Settings")]
    [SerializeField] private float damageAmount = 10f;
    [SerializeField] private float damageCooldown = 0.5f; // Prevents spam damage
    
    private static readonly string PlayerTag = "Player";
    private float lastDamageTime;
    private HealthSystem cachedPlayerHealth;
    
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag(PlayerTag))
        {
            DealDamage(collision.gameObject);
        }
    }
    
    void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag(PlayerTag))
        {
            DealDamage(collision.gameObject);
        }
    }
    
    private void DealDamage(GameObject playerObject)
    {
        // Cooldown check to prevent spam damage
        if (Time.time - lastDamageTime < damageCooldown)
            return;
        
        // Try to get or cache the HealthSystem component
        if (cachedPlayerHealth == null)
        {
            if (!playerObject.TryGetComponent<HealthSystem>(out cachedPlayerHealth))
                return;
        }
        
        // Deal damage and update cooldown
        cachedPlayerHealth.TakeDamage(damageAmount);
        lastDamageTime = Time.time;
    }
    
    void OnCollisionExit(Collision collision)
    {
        // Clear cached reference when player leaves collision
        if (collision.gameObject.CompareTag(PlayerTag))
        {
            cachedPlayerHealth = null;
        }
    }
}