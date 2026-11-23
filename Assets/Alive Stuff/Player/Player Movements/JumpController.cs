using UnityEngine;

[RequireComponent(typeof(BasicMovementController))]
public class JumpController : MonoBehaviour
{
    [Header("Jump Settings")]
    public float jumpPower = 7f;
    public KeyCode jumpKey = KeyCode.Space;
    public bool enableDoubleJump = false;
    public float doubleJumpPower = 7f;

    [Header("Ground Check")]
    public float groundCheckDistance = 0.1f;
    public LayerMask groundLayer = -1;

    private BasicMovementController movementController;
    private bool isGrounded;
    private bool hasDoubleJumped;
    private int jumpCount = 0;
    private int maxJumps = 1;

    public bool IsGrounded => isGrounded;
    public bool CanJump => isGrounded || (enableDoubleJump && !hasDoubleJumped);
    public int JumpCount => jumpCount;

    void Start()
    {
        movementController = GetComponent<BasicMovementController>();
        maxJumps = enableDoubleJump ? 2 : 1;
    }

    void Update()
    {
        if (Portals.Core.PauseManager.IsPaused) return;
        CheckGrounded();
        HandleJump();
    }

    private void CheckGrounded()
    {
        bool wasGrounded = isGrounded;
        isGrounded = movementController.IsGrounded;

        // Reset double jump when landing
        if (isGrounded && !wasGrounded)
        {
            hasDoubleJumped = false;
            jumpCount = 0;
        }
    }

    private void HandleJump()
    {
        if (Input.GetKeyDown(jumpKey) && CanJump)
        {
            PerformJump();
        }
    }

    private void PerformJump()
    {
        Vector3 jumpForce = Vector3.up * jumpPower;
        
        if (!isGrounded && enableDoubleJump && !hasDoubleJumped)
        {
            // Double jump
            jumpForce = Vector3.up * doubleJumpPower;
            hasDoubleJumped = true;
        }

        movementController.AddForce(jumpForce);
        jumpCount++;
    }

    public void SetJumpPower(float newPower)
    {
        jumpPower = newPower;
    }

    public void SetDoubleJumpEnabled(bool enabled)
    {
        enableDoubleJump = enabled;
        maxJumps = enabled ? 2 : 1;
    }

    public void ResetJumpState()
    {
        hasDoubleJumped = false;
        jumpCount = 0;
    }

    public void ForceJump(float power = -1f)
    {
        float jumpForce = power > 0 ? power : jumpPower;
        movementController.AddForce(Vector3.up * jumpForce);
        jumpCount++;
    }
}
