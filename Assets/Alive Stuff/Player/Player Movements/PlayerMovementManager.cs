using UnityEngine;

public class PlayerMovementManager : MonoBehaviour
{
    [Header("Component References")]
    public BasicMovementController basicMovement;
    public CameraLookController cameraLook;
    public JumpController jumpController;
    public DashController dashController;

    [Header("Movement State")]
    public bool movementEnabled = true;
    public bool lookEnabled = true;

    void Start()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        // Get components if not assigned
        if (basicMovement == null)
            basicMovement = GetComponent<BasicMovementController>();
        
        if (cameraLook == null)
            cameraLook = GetComponent<CameraLookController>();
        
        if (jumpController == null)
            jumpController = GetComponent<JumpController>();
        
        if (dashController == null)
            dashController = GetComponent<DashController>();

        // Validate components
        if (basicMovement == null)
            Debug.LogError("PlayerMovementManager: BasicMovementController not found!");
        
        if (cameraLook == null)
            Debug.LogError("PlayerMovementManager: CameraLookController not found!");
    }

    void Update()
    {
        // Central update logic can go here if needed
        UpdateMovementState();
    }

    private void UpdateMovementState()
    {
        // Sync movement state across all controllers
        if (basicMovement != null)
            basicMovement.SetMovementEnabled(movementEnabled);
        
        if (cameraLook != null)
            cameraLook.SetLookEnabled(lookEnabled);
    }

    // Public methods for external control
    public void EnableAllMovement()
    {
        movementEnabled = true;
        lookEnabled = true;
    }

    public void DisableAllMovement()
    {
        movementEnabled = false;
        lookEnabled = false;
    }

    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
    }

    public void SetLookEnabled(bool enabled)
    {
        lookEnabled = enabled;
    }

    // Getters for movement state
    public bool IsGrounded()
    {
        return jumpController != null ? jumpController.IsGrounded : false;
    }

    public bool IsDashing()
    {
        return dashController != null ? dashController.IsDashing : false;
    }

    public bool CanDash()
    {
        return dashController != null ? dashController.CanDash : false;
    }

    public bool CanJump()
    {
        return jumpController != null ? jumpController.CanJump : false;
    }

    // Movement configuration methods
    public void SetMovementSpeeds(float walkSpeed, float runSpeed)
    {
        if (basicMovement != null)
            basicMovement.SetMovementSpeed(walkSpeed, runSpeed);
    }

    public void SetJumpPower(float power)
    {
        if (jumpController != null)
            jumpController.SetJumpPower(power);
    }

    public void SetDashStats(float distance, float speed, float duration, float cooldown)
    {
        if (dashController != null)
            dashController.SetDashStats(distance, speed, duration, cooldown);
    }

    // Reset methods
    public void ResetAllMovement()
    {
        if (jumpController != null)
            jumpController.ResetJumpState();
        
        if (dashController != null)
            dashController.ResetDash();
        
        if (cameraLook != null)
            cameraLook.ResetRotation();
    }

    // Force movement methods
    public void ForceJump(float power = -1f)
    {
        if (jumpController != null)
            jumpController.ForceJump(power);
    }

    public void AddForce(Vector3 force)
    {
        if (basicMovement != null)
            basicMovement.AddForce(force);
    }
}
