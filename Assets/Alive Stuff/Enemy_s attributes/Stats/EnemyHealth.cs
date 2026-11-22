using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class EnemyHealth : MonoBehaviour
{
    #region Inspector Fields
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private bool canRegenerate = false;
    [SerializeField] private float regenerationRate = 5f;
    [SerializeField] private float regenerationDelay = 3f;
    [SerializeField] private float regenerationCooldown = 5f;

    [Header("Damage Resistance")]
    [SerializeField] private float baseArmor = 0f;
    [SerializeField] private float maxArmor = 50f;
    [SerializeField] private float armorDamageReduction = 0.5f; // Percentage reduction per armor point
    [SerializeField] private bool armorDegrades = true;
    [SerializeField] private float armorDegradationRate = 0.1f;

    [Header("Invincibility")]
    [SerializeField] private bool hasInvincibilityFrames = true;
    [SerializeField] private float invincibilityDuration = 0.2f;
    [SerializeField] private bool flashOnDamage = true;
    [SerializeField] private Color damageFlashColor = Color.red;
    [SerializeField] private float flashDuration = 0.1f;

    [Header("Death Settings")]
    [SerializeField] private float deathDelay = 0f;
    [SerializeField] private bool destroyOnDeath = true;
    [SerializeField] private GameObject deathEffect;
    [SerializeField] private bool dropLoot = false;
    [SerializeField] private GameObject[] lootPrefabs;
    [SerializeField] private float lootDropForce = 5f;

    [Header("Damage Over Time")]
    [SerializeField] private bool canHaveDOT = true;
    [SerializeField] private int maxDOTStacks = 5;

    [Header("Events")]
    public UnityEvent<float> onDamageTaken; // Passes damage amount
    public UnityEvent<float> onHealthChanged; // Passes current health
    public UnityEvent<float> onHealthPercentageChanged; // Passes health percentage
    public UnityEvent onDeath;
    public UnityEvent onDeathComplete;
    public UnityEvent<float> onArmorChanged; // Passes current armor
    public UnityEvent onRegenerationStart;
    public UnityEvent onRegenerationStop;
    #endregion

    #region Private Fields
    private float currentHealth;
    private float currentArmor;
    private bool isDead = false;
    private bool isInvincible = false;
    private float lastDamageTime = -999f;
    private float regenerationTimer = 0f;
    private bool isRegenerating = false;

    // Damage over time
    private List<DOTEffect> activeDOTs = new List<DOTEffect>();
    private float nextDOTUpdate = 0f;
    private const float DOT_UPDATE_INTERVAL = 0.1f;

    // Visual feedback
    private MeshRenderer[] meshRenderers;
    private SkinnedMeshRenderer[] skinnedRenderers;
    private Material[] originalMaterials;
    private Coroutine flashCoroutine;

    // Performance
    private float cachedMaxHealth;
    private bool initialized = false;
    #endregion

    #region DOT Effect Class
    private class DOTEffect
    {
        public float damagePerSecond;
        public float duration;
        public float remainingTime;
        public float lastTickTime;
        public Color effectColor;

        public DOTEffect(float dps, float dur, Color color)
        {
            damagePerSecond = dps;
            duration = dur;
            remainingTime = dur;
            lastTickTime = Time.time;
            effectColor = color;
        }
    }
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        Initialize();
    }

    private void Start()
    {
        if (!initialized)
            Initialize();
    }

    private void Update()
    {
        if (isDead) return;

        UpdateRegeneration();
        UpdateDOTEffects();
        UpdateArmorDegradation();
    }

    private void OnDestroy()
    {
        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);
    }
    #endregion

    #region Initialization
    private void Initialize()
    {
        // UnityEvents added at runtime can be null in some editor versions.
        if (onDamageTaken == null) onDamageTaken = new UnityEvent<float>();
        if (onHealthChanged == null) onHealthChanged = new UnityEvent<float>();
        if (onHealthPercentageChanged == null) onHealthPercentageChanged = new UnityEvent<float>();
        if (onDeath == null) onDeath = new UnityEvent();
        if (onDeathComplete == null) onDeathComplete = new UnityEvent();
        if (onArmorChanged == null) onArmorChanged = new UnityEvent<float>();
        if (onRegenerationStart == null) onRegenerationStart = new UnityEvent();
        if (onRegenerationStop == null) onRegenerationStop = new UnityEvent();

        currentHealth = maxHealth;
        currentArmor = baseArmor;
        cachedMaxHealth = maxHealth;
        lastDamageTime = -regenerationDelay;

        // Cache renderers for visual feedback
        meshRenderers = GetComponentsInChildren<MeshRenderer>();
        skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();

        // Store original materials
        if (flashOnDamage)
        {
            List<Material> materials = new List<Material>();
            foreach (var renderer in meshRenderers)
            {
                if (renderer != null && renderer.material != null)
                    materials.Add(renderer.material);
            }
            foreach (var renderer in skinnedRenderers)
            {
                if (renderer != null && renderer.material != null)
                    materials.Add(renderer.material);
            }
            originalMaterials = materials.ToArray();
        }

        initialized = true;
    }
    #endregion

    #region Health Management
    public void TakeDamage(float damage, Vector3? damageSource = null, DamageType damageType = DamageType.Normal)
    {
        if (isDead || damage <= 0) return;
        if (isInvincible && hasInvincibilityFrames) return;

        // Apply armor reduction
        float finalDamage = CalculateDamageWithArmor(damage);

        // Apply damage type modifiers
        finalDamage = ApplyDamageTypeModifiers(finalDamage, damageType);

        currentHealth = Mathf.Max(currentHealth - finalDamage, 0f);
        lastDamageTime = Time.time;
        regenerationTimer = 0f;

        // Update armor if it degrades
        if (armorDegrades && currentArmor > 0f)
        {
            float armorDamage = damage * armorDegradationRate;
            currentArmor = Mathf.Max(currentArmor - armorDamage, 0f);
            onArmorChanged?.Invoke(currentArmor);
        }

        // Visual feedback
        if (flashOnDamage)
            FlashDamage();

        // Invincibility frames
        if (hasInvincibilityFrames)
            StartCoroutine(InvincibilityFrames());

        // Events
        onDamageTaken?.Invoke(finalDamage);
        onHealthChanged?.Invoke(currentHealth);
        onHealthPercentageChanged?.Invoke(GetHealthPercentage());

        // Check for death
        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (isDead || amount <= 0) return;

        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        onHealthChanged?.Invoke(currentHealth);
        onHealthPercentageChanged?.Invoke(GetHealthPercentage());
    }

    public void SetMaxHealth(float newMaxHealth, bool adjustCurrent = false)
    {
        float healthPercentage = GetHealthPercentage();
        maxHealth = Mathf.Max(newMaxHealth, 1f);
        cachedMaxHealth = maxHealth;

        if (adjustCurrent)
        {
            currentHealth = maxHealth * healthPercentage;
        }
        else
        {
            currentHealth = Mathf.Min(currentHealth, maxHealth);
        }

        onHealthChanged?.Invoke(currentHealth);
        onHealthPercentageChanged?.Invoke(GetHealthPercentage());
    }

    public void RestoreFullHealth()
    {
        currentHealth = maxHealth;
        onHealthChanged?.Invoke(currentHealth);
        onHealthPercentageChanged?.Invoke(GetHealthPercentage());
    }

    private float ApplyDamageTypeModifiers(float damage, DamageType damageType)
    {
        // Apply damage type multipliers
        // Override this method or extend it for specific enemy resistances/weaknesses
        switch (damageType)
        {
            case DamageType.Critical:
                return damage * 1.5f; // 50% bonus damage
            case DamageType.Fire:
                return damage * 1.0f; // Default - can be customized
            case DamageType.Ice:
                return damage * 1.0f; // Default - can be customized
            case DamageType.Poison:
                return damage * 1.0f; // Default - can be customized
            case DamageType.Electric:
                return damage * 1.0f; // Default - can be customized
            case DamageType.DOT:
                return damage * 1.0f; // Default - DOT already handled separately
            case DamageType.Normal:
            default:
                return damage;
        }
    }
    #endregion

    #region Armor System
    private float CalculateDamageWithArmor(float baseDamage)
    {
        if (currentArmor <= 0f) return baseDamage;

        float reduction = currentArmor * armorDamageReduction * 0.01f;
        reduction = Mathf.Clamp01(reduction);
        return baseDamage * (1f - reduction);
    }

    public void AddArmor(float amount)
    {
        currentArmor = Mathf.Min(currentArmor + amount, maxArmor);
        onArmorChanged?.Invoke(currentArmor);
    }

    public void SetArmor(float amount)
    {
        currentArmor = Mathf.Clamp(amount, 0f, maxArmor);
        onArmorChanged?.Invoke(currentArmor);
    }

    private void UpdateArmorDegradation()
    {
        if (!armorDegrades || currentArmor <= 0f) return;

        // Natural degradation over time
        currentArmor = Mathf.Max(currentArmor - armorDegradationRate * Time.deltaTime * 0.1f, 0f);
    }
    #endregion

    #region Regeneration
    private void UpdateRegeneration()
    {
        if (!canRegenerate || isDead) return;

        float timeSinceDamage = Time.time - lastDamageTime;

        if (timeSinceDamage >= regenerationDelay)
        {
            if (!isRegenerating)
            {
                isRegenerating = true;
                onRegenerationStart?.Invoke();
            }

            regenerationTimer += Time.deltaTime;

            if (regenerationTimer >= regenerationCooldown)
            {
                if (currentHealth < maxHealth)
                {
                    float regenAmount = regenerationRate * Time.deltaTime;
                    Heal(regenAmount);
                }
            }
        }
        else
        {
            if (isRegenerating)
            {
                isRegenerating = false;
                regenerationTimer = 0f;
                onRegenerationStop?.Invoke();
            }
        }
    }
    #endregion

    #region Damage Over Time
    public void ApplyDOT(float damagePerSecond, float duration, Color? effectColor = null)
    {
        if (!canHaveDOT || isDead) return;

        // Check if we should stack or replace
        if (activeDOTs.Count >= maxDOTStacks)
        {
            // Remove oldest DOT
            activeDOTs.RemoveAt(0);
        }

        Color color = effectColor ?? damageFlashColor;
        activeDOTs.Add(new DOTEffect(damagePerSecond, duration, color));
    }

    private void UpdateDOTEffects()
    {
        if (activeDOTs.Count == 0) return;

        if (Time.time < nextDOTUpdate) return;
        nextDOTUpdate = Time.time + DOT_UPDATE_INTERVAL;

        for (int i = activeDOTs.Count - 1; i >= 0; i--)
        {
            DOTEffect dot = activeDOTs[i];
            dot.remainingTime -= DOT_UPDATE_INTERVAL;

            // Apply damage
            float damage = dot.damagePerSecond * DOT_UPDATE_INTERVAL;
            TakeDamage(damage, null, DamageType.DOT);

            // Remove expired DOTs
            if (dot.remainingTime <= 0f)
            {
                activeDOTs.RemoveAt(i);
            }
        }
    }

    public void ClearAllDOTs()
    {
        activeDOTs.Clear();
    }
    #endregion

    #region Invincibility
    private IEnumerator InvincibilityFrames()
    {
        isInvincible = true;
        yield return new WaitForSeconds(invincibilityDuration);
        isInvincible = false;
    }

    public bool IsInvincible()
    {
        return isInvincible;
    }
    #endregion

    #region Visual Feedback
    private void FlashDamage()
    {
        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);

        flashCoroutine = StartCoroutine(FlashCoroutine());
    }

    private IEnumerator FlashCoroutine()
    {
        // Set flash color
        foreach (var renderer in meshRenderers)
        {
            if (renderer != null && renderer.material != null)
                renderer.material.color = damageFlashColor;
        }
        foreach (var renderer in skinnedRenderers)
        {
            if (renderer != null && renderer.material != null)
                renderer.material.color = damageFlashColor;
        }

        yield return new WaitForSeconds(flashDuration);

        // Restore original colors
        int matIndex = 0;
        foreach (var renderer in meshRenderers)
        {
            if (renderer != null && matIndex < originalMaterials.Length)
            {
                renderer.material.color = originalMaterials[matIndex].color;
                matIndex++;
            }
        }
        foreach (var renderer in skinnedRenderers)
        {
            if (renderer != null && matIndex < originalMaterials.Length)
            {
                renderer.material.color = originalMaterials[matIndex].color;
                matIndex++;
            }
        }

        flashCoroutine = null;
    }
    #endregion

    #region Death
    private void Die()
    {
        if (isDead) return;

        isDead = true;
        currentHealth = 0f;

        // Stop all coroutines
        StopAllCoroutines();

        // Events
        onDeath?.Invoke();
        onHealthChanged?.Invoke(0f);
        onHealthPercentageChanged?.Invoke(0f);

        // Simple immediate destruction if no death effects or loot
        if (deathDelay <= 0f && deathEffect == null && !dropLoot)
        {
            if (destroyOnDeath)
            {
                Destroy(gameObject);
            }
            return;
        }

        // Death delay for animations/effects
        if (deathDelay > 0f)
        {
            StartCoroutine(DeathDelayCoroutine());
        }
        else
        {
            CompleteDeath();
        }
    }

    private IEnumerator DeathDelayCoroutine()
    {
        yield return new WaitForSeconds(deathDelay);
        CompleteDeath();
    }

    private void CompleteDeath()
    {
        // Spawn death effect
        if (deathEffect != null)
        {
            Instantiate(deathEffect, transform.position, transform.rotation);
        }

        // Drop loot
        if (dropLoot && lootPrefabs != null && lootPrefabs.Length > 0)
        {
            foreach (var loot in lootPrefabs)
            {
                if (loot != null)
                {
                    GameObject dropped = Instantiate(loot, transform.position + Vector3.up, Random.rotation);
                    Rigidbody rb = dropped.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        Vector3 randomDirection = new Vector3(
                            Random.Range(-1f, 1f),
                            Random.Range(0.5f, 1f),
                            Random.Range(-1f, 1f)
                        ).normalized;
                        rb.AddForce(randomDirection * lootDropForce, ForceMode.Impulse);
                    }
                }
            }
        }

        onDeathComplete?.Invoke();

        if (destroyOnDeath)
        {
            Destroy(gameObject);
        }
    }
    #endregion

    #region Public Getters
    public float GetCurrentHealth()
    {
        return currentHealth;
    }

    public float GetMaxHealth()
    {
        return maxHealth;
    }

    public float GetHealthPercentage()
    {
        if (cachedMaxHealth <= 0f) return 0f;
        return currentHealth / cachedMaxHealth;
    }

    public float GetCurrentArmor()
    {
        return currentArmor;
    }

    public float GetMaxArmor()
    {
        return maxArmor;
    }

    public bool IsDead()
    {
        return isDead;
    }

    public bool IsRegenerating()
    {
        return isRegenerating;
    }

    public int GetActiveDOTCount()
    {
        return activeDOTs.Count;
    }
    #endregion
}

#region Damage Type Enum
public enum DamageType
{
    Normal,
    Fire,
    Ice,
    Poison,
    Electric,
    DOT,
    Critical
}
#endregion
