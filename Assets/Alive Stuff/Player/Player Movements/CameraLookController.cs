using UnityEngine;

public class CameraLookController : MonoBehaviour
{
    [Header("Camera Settings")]
    public Camera playerCamera;
    public float lookSpeed = 2f;
    public float lookXLimit = 45f;

    private float rotationX = 0;
    private bool canLook = true;

    void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (!canLook) return;

        HandleCameraLook();
    }

    private void HandleCameraLook()
    {
        rotationX += -Input.GetAxis("Mouse Y") * lookSpeed;
        rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
        
        playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
        transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeed, 0);
    }

    public void SetLookEnabled(bool enabled)
    {
        canLook = enabled;
    }

    public void SetCursorState(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    public void SetLookSpeed(float newSpeed)
    {
        lookSpeed = newSpeed;
    }

    public void ResetRotation()
    {
        rotationX = 0;
        transform.rotation = Quaternion.identity;
    }
}
