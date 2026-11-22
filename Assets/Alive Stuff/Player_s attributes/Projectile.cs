using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private float speed = 20f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float explosionRadius = 0f; // 0 = no explosion, just direct hit
    [SerializeField] private bool destroyOnHit = true;
    [SerializeField] private GameObject hitEffect;
    [SerializeField] private GameObject explosionEffect;

    [Header("Visual")]
    [SerializeField] private Color projectileColor = Color.white;
    [SerializeField] private bool showTrail = true;
    [SerializeField] private TrailRenderer trailRenderer;

    private Rigidbody rb;
    private float spawnTime;
    private bool hasHit = false;
    private Vector3 lastPosition;

    void Start()
    {
        spawnTime = Time.time;
        lastPosition = transform.position;
        rb = GetComponent<Rigidbody>();

        // Setup visual
        SetupVisuals();
        
        // Debug log
        Debug.Log($"Projectile spawned at {transform.position}, has Rigidbody: {rb != null}, has Renderer: {GetComponent<Renderer>() != null}");
    }

    public void Initialize(float dmg, float spd, float life, Color color, bool trail)
    {
        damage = dmg;
        speed = spd;
        lifetime = life;
        projectileColor = color;
        showTrail = trail;
    }

    void Update()
    {
        // Lifetime check
        if (Time.time - spawnTime >= lifetime)
        {
            DestroyProjectile();
            return;
        }

        // Check for collisions using raycast (more reliable than OnTriggerEnter)
        if (!hasHit)
        {
            CheckCollisions();
        }
    }

    void CheckCollisions()
    {
        Vector3 currentPosition = transform.position;
        Vector3 direction = currentPosition - lastPosition;
        float distance = direction.magnitude;

        if (distance > 0.01f)
        {
            RaycastHit hit;
            if (Physics.Raycast(lastPosition, direction.normalized, out hit, distance))
            {
                OnHit(hit.collider, hit.point);
            }
        }

        lastPosition = currentPosition;
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;

        // Ignore player
        if (other.CompareTag("Player"))
            return;

        OnHit(other, other.ClosestPoint(transform.position));
    }

    void OnHit(Collider hitCollider, Vector3 hitPoint)
    {
        if (hasHit) return;
        hasHit = true;

        Debug.Log($"Projectile hit: {hitCollider.name} at {hitPoint}");

        // Deal damage
        if (explosionRadius > 0f)
        {
            // Explosive projectile
            DealExplosiveDamage(hitPoint);
        }
        else
        {
            // Direct hit
            DealDirectDamage(hitCollider);
        }

        // Spawn hit effect
        if (hitEffect != null)
        {
            Instantiate(hitEffect, hitPoint, Quaternion.LookRotation(-transform.forward));
        }

        // Spawn explosion effect if applicable
        if (explosionRadius > 0f && explosionEffect != null)
        {
            Instantiate(explosionEffect, hitPoint, Quaternion.identity);
        }

        // Destroy projectile
        if (destroyOnHit)
        {
            DestroyProjectile();
        }
        else
        {
            // Disable collider and renderer but keep object for trail
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            Renderer rend = GetComponent<Renderer>();
            if (rend != null) rend.enabled = false;

            // Destroy after trail fades
            Destroy(gameObject, lifetime);
        }
    }

    void DealDirectDamage(Collider hitCollider)
    {
        EnemyHealth enemyHealth = hitCollider.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            Vector3 damageSource = transform.position;
            enemyHealth.TakeDamage(damage, damageSource, DamageType.Normal);
        }
    }

    void DealExplosiveDamage(Vector3 explosionCenter)
    {
        Collider[] hitColliders = Physics.OverlapSphere(explosionCenter, explosionRadius);
        
        foreach (Collider hit in hitColliders)
        {
            if (hit.CompareTag("Player")) continue;

            // Calculate distance and damage falloff
            float distance = Vector3.Distance(explosionCenter, hit.transform.position);
            float damageMultiplier = 1f - (distance / explosionRadius);
            damageMultiplier = Mathf.Clamp01(damageMultiplier);

            // Deal damage to enemies
            EnemyHealth enemyHealth = hit.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                float finalDamage = damage * damageMultiplier;
                enemyHealth.TakeDamage(finalDamage, explosionCenter, DamageType.Normal);
            }

            // Apply explosion force to rigidbodies (closer = more force)
            Rigidbody rb = hit.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 explosionDirection = (hit.transform.position - explosionCenter).normalized;
                float force = damageMultiplier * 10f; // More force for closer enemies
                rb.AddForce(explosionDirection * force, ForceMode.Impulse);
            }
        }
    }

    void SetupVisuals()
    {
        // Set color
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            if (renderer.material != null)
            {
                renderer.material.color = projectileColor;
            }
            else
            {
                // Create a material if none exists
                Material mat = new Material(Shader.Find("Standard"));
                mat.color = projectileColor;
                renderer.material = mat;
            }
            Debug.Log($"Projectile renderer found: {renderer.GetType().Name}, enabled: {renderer.enabled}");
        }
        else
        {
            Debug.LogWarning("Projectile has no Renderer component! Adding a default sphere.");
            // Add a visible mesh if none exists
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.SetParent(transform);
            sphere.transform.localPosition = Vector3.zero;
            sphere.transform.localScale = Vector3.one * 0.2f;
            
            // Remove the collider from the sphere (we use the main collider)
            Collider sphereCol = sphere.GetComponent<Collider>();
            if (sphereCol != null)
                Destroy(sphereCol);
            
            // Set color
            Renderer sphereRenderer = sphere.GetComponent<Renderer>();
            if (sphereRenderer != null)
            {
                Material mat = new Material(Shader.Find("Standard"));
                mat.color = projectileColor;
                sphereRenderer.material = mat;
            }
        }

        // Setup trail
        if (showTrail)
        {
            if (trailRenderer == null)
            {
                trailRenderer = GetComponent<TrailRenderer>();
            }

            if (trailRenderer == null)
            {
                GameObject trailObj = new GameObject("Trail");
                trailObj.transform.SetParent(transform);
                trailObj.transform.localPosition = Vector3.zero;
                trailRenderer = trailObj.AddComponent<TrailRenderer>();
            }

            if (trailRenderer != null)
            {
                trailRenderer.startColor = projectileColor;
                trailRenderer.endColor = new Color(projectileColor.r, projectileColor.g, projectileColor.b, 0f);
                trailRenderer.time = 0.3f;
                trailRenderer.startWidth = 0.1f;
                trailRenderer.endWidth = 0.05f;
            }
        }
    }

    void DestroyProjectile()
    {
        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        if (explosionRadius > 0f)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}
