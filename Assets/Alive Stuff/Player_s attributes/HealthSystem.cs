using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Minimal player health component.
///
/// NOTE: This class was referenced by Pain.cs but was missing from the project,
/// which prevented Assembly-CSharp from compiling. It is intentionally small and
/// independent of the portal systems. Replace it with your own health solution if
/// you already have one - Pain.cs only needs TakeDamage(float).
/// </summary>
public sealed class HealthSystem : MonoBehaviour
{
    [Header("Health")]
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [SerializeField, Min(0f)] private float startingHealth = 100f;
    [SerializeField] private bool startInvulnerable = false;

    [Header("Events")]
    public UnityEvent<float> onHealthChanged = new UnityEvent<float>();
    public UnityEvent onDeath = new UnityEvent();

    private float currentHealth;
    private bool isDead;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public float HealthRatio => maxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maxHealth);
    public bool IsDead => isDead;
    public bool IsInvulnerable { get; private set; }

    private void Awake()
    {
        currentHealth = Mathf.Clamp(startingHealth, 0f, maxHealth);
        IsInvulnerable = startInvulnerable;
        isDead = currentHealth <= 0f;
    }

    public void TakeDamage(float amount)
    {
        if (isDead || IsInvulnerable || amount <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        onHealthChanged?.Invoke(currentHealth);

        if (currentHealth <= 0f)
        {
            isDead = true;
            onDeath?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        if (isDead || amount <= 0f) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        onHealthChanged?.Invoke(currentHealth);
    }

    public void SetInvulnerable(bool value) => IsInvulnerable = value;

    public void ResetHealth()
    {
        currentHealth = Mathf.Clamp(startingHealth, 0f, maxHealth);
        isDead = currentHealth <= 0f;
        onHealthChanged?.Invoke(currentHealth);
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        startingHealth = Mathf.Clamp(startingHealth, 0f, maxHealth);
    }
}
