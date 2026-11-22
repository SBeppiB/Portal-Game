using UnityEngine;
using Portals.Traversal;

[RequireComponent(typeof(CharacterController))]
public class BasicMovementController : MonoBehaviour, IPortalMomentumReceiver, IPortalExitAssistReceiver
{
    [Header("Movement Settings")]
    public float walkSpeed = 6f;
    public float runSpeed = 12f;
    public float gravity = 20f;
    public float defaultHeight = 2f;
    public float crouchHeight = 1f;
    public float crouchSpeed = 3f;

    [Header("Input Settings")]
    public KeyCode forwardKey = KeyCode.W;
    public KeyCode backKey = KeyCode.S;
    public KeyCode leftKey = KeyCode.A;
    public KeyCode rightKey = KeyCode.D;
    public KeyCode runKey = KeyCode.LeftShift;
    public KeyCode crouchKey = KeyCode.LeftControl;

    private Vector3 moveDirection = Vector3.zero;
    private CharacterController characterController;
    private bool canMove = true;

    public Vector3 MoveDirection => moveDirection;
    public bool CanMove => canMove;
    public bool IsGrounded => characterController.isGrounded;
    public bool IsRunning => Input.GetKey(runKey);
    public bool IsCrouching => Input.GetKey(crouchKey) && canMove;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (!canMove) return;

        HandleMovement();
        HandleCrouch();
        ApplyGravity();
        characterController.Move(moveDirection * Time.deltaTime);
    }

    private void HandleMovement()
    {
        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);

        float moveZ = 0;
        float moveX = 0;

        if (Input.GetKey(forwardKey)) moveZ += 1;
        if (Input.GetKey(backKey)) moveZ -= 1;
        if (Input.GetKey(rightKey)) moveX += 1;
        if (Input.GetKey(leftKey)) moveX -= 1;

        bool isRunning = Input.GetKey(runKey) && !IsCrouching;
        float speed = isRunning ? runSpeed : (IsCrouching ? crouchSpeed : walkSpeed);

        float movementDirectionY = moveDirection.y;
        moveDirection = (forward * moveZ + right * moveX).normalized * speed;
        moveDirection.y = movementDirectionY;
    }

    private void HandleCrouch()
    {
        if (IsCrouching)
        {
            characterController.height = crouchHeight;
        }
        else
        {
            characterController.height = defaultHeight;
        }
    }

    private void ApplyGravity()
    {
        if (!characterController.isGrounded)
        {
            moveDirection.y -= gravity * Time.deltaTime;
        }
        else
        {
            moveDirection.y = 0;
        }
    }

    public void SetMovementEnabled(bool enabled)
    {
        canMove = enabled;
    }

    public void AddForce(Vector3 force)
    {
        moveDirection += force;
    }

    public void SetMovementSpeed(float newWalkSpeed, float newRunSpeed)
    {
        walkSpeed = newWalkSpeed;
        runSpeed = newRunSpeed;
    }

    /// <summary>Rotates cached momentum when PortalTraverser teleports this entity.</summary>
    public void ApplyPortalRotation(Quaternion rotation)
    {
        moveDirection = rotation * moveDirection;
    }

    /// <summary>
    /// Adds a floor-exit assist impulse on top of existing momentum. Additive by design so
    /// traversal momentum is never replaced.
    /// </summary>
    public void ApplyExitImpulse(Vector3 velocityImpulse)
    {
        moveDirection += velocityImpulse;
    }
}
