using UnityEngine;
using System.Collections;
using Portals.Traversal;

[RequireComponent(typeof(BasicMovementController))]
public class DashController : MonoBehaviour, IPortalMomentumReceiver
{
    [Header("Dash Settings")]
    public float dashDistance = 8f;
    public float dashSpeed = 30f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;
    public KeyCode dashKey = KeyCode.LeftAlt;

    [Header("Audio")]
    public AudioClip dashSFX;
    public AudioClip[] dashSFXVariations;
    [Range(0f, 1f)] public float dashVolume = 0.7f;

    private BasicMovementController movementController;
    private bool canDash = true;
    private bool isDashing = false;
    private Vector3 dashDirection;
    private float dashStartTime;
    private Vector3 originalPosition;
    private AudioSource audioSource;

    public bool IsDashing => isDashing;
    public bool CanDash => canDash && !isDashing;

    void Start()
    {
        movementController = GetComponent<BasicMovementController>();
        audioSource = GetComponent<AudioSource>();
        
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(dashKey) && CanDash)
        {
            StartDash();
        }

        if (isDashing)
        {
            UpdateDash();
        }
    }

    private void StartDash()
    {
        // Get dash direction from input
        Vector3 inputDirection = GetInputDirection();
        dashDirection = inputDirection.magnitude > 0.1f ? inputDirection.normalized : transform.TransformDirection(Vector3.forward);

        // Debug logging
        Debug.Log($"Dash started! Input direction: {inputDirection}, Final dash direction: {dashDirection}");

        // Initialize dash
        isDashing = true;
        canDash = false;
        dashStartTime = Time.time;
        originalPosition = transform.position;

        // Disable normal movement during dash
        movementController.SetMovementEnabled(false);

        // Play effects
        PlayDashSound();
    }

    private void UpdateDash()
    {
        float dashProgress = (Time.time - dashStartTime) / dashDuration;
        
        if (dashProgress >= 1f)
        {
            EndDash();
            return;
        }

        // Calculate dash movement and apply through CharacterController
        Vector3 dashMovement = dashDirection * dashSpeed * Time.deltaTime;
        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.Move(dashMovement);
        }
        else
        {
            transform.position += dashMovement;
        }
    }

    private void EndDash()
    {
        isDashing = false;
        movementController.SetMovementEnabled(true);

        // Start cooldown
        StartCoroutine(DashCooldown());
    }

    private Vector3 GetInputDirection()
    {
        // Use the same coordinate system as BasicMovementController
        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);
        
        Vector3 direction = Vector3.zero;

        if (Input.GetKey(KeyCode.W)) direction += forward;
        if (Input.GetKey(KeyCode.S)) direction -= forward;
        if (Input.GetKey(KeyCode.D)) direction += right;
        if (Input.GetKey(KeyCode.A)) direction -= right;

        return direction;
    }

    private Vector3 GetCameraForward()
    {
        Camera playerCamera = Camera.main;
        if (playerCamera == null)
            playerCamera = FindObjectOfType<Camera>();

        if (playerCamera != null)
        {
            Vector3 forward = playerCamera.transform.forward;
            forward.y = 0; // Keep horizontal
            return forward.normalized;
        }
        
        return transform.forward; // Fallback
    }

    private void PlayDashSound()
    {
        if (dashSFXVariations != null && dashSFXVariations.Length > 0)
        {
            AudioClip randomClip = dashSFXVariations[Random.Range(0, dashSFXVariations.Length)];
            audioSource.PlayOneShot(randomClip, dashVolume);
        }
        else if (dashSFX != null)
        {
            audioSource.PlayOneShot(dashSFX, dashVolume);
        }
    }

    private IEnumerator DashCooldown()
    {
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    public void ResetDash()
    {
        canDash = true;
        isDashing = false;
        movementController.SetMovementEnabled(true);
    }

    public void SetDashStats(float distance, float speed, float duration, float cooldown)
    {
        dashDistance = distance;
        dashSpeed = speed;
        dashDuration = duration;
        dashCooldown = cooldown;
    }

    /// <summary>Keeps an in-flight dash aligned after portal traversal.</summary>
    public void ApplyPortalRotation(Quaternion rotation)
    {
        dashDirection = rotation * dashDirection;
    }
}
