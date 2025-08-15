using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Follow Settings")]
    public Transform Target; // Will be set to LocalPlayer
    public Vector3 Offset = new Vector3(0, 35, -25); // Very high and far back for maximum wide view
    public float SmoothTime = 0.15f; // Faster response
    public bool FollowPlayer = true;

    [Header("Zoom Settings")]
    public float MinZoom = 5f;
    public float MaxZoom = 60f; // Allow zooming out much further for wide view
    public float ZoomSpeed = 2f;

    [Header("Rotation Settings")]
    public bool AllowRotation = false;
    public float RotationSpeed = 2f;

    private Vector3 _velocity = Vector3.zero;
    private Camera _camera;
    private float _currentZoom;

    private void Start()
    {
        _camera = GetComponent<Camera>();
        _currentZoom = _camera.fieldOfView;
        
        // Find the local player automatically
        if (Target == null)
        {
            var localPlayer = FindObjectOfType<PlayerController>();
            if (localPlayer != null)
            {
                Target = localPlayer.transform;
            }
        }
    }

    private void LateUpdate()
    {
        if (!FollowPlayer || Target == null) return;

        HandleCameraFollow();
        HandleZoom();
        HandleRotation();
    }

    private void HandleCameraFollow()
    {
        // Calculate desired position
        Vector3 desiredPosition = Target.position + Offset;
        
        // Smoothly move camera
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _velocity, SmoothTime);
        
        // Look at the player
        Vector3 lookDirection = Target.position - transform.position;
        if (lookDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 2f);
        }
    }

    private void HandleZoom()
    {
        // Mouse scroll wheel zoom
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        _currentZoom -= scroll * ZoomSpeed;
        _currentZoom = Mathf.Clamp(_currentZoom, MinZoom, MaxZoom);
        
        _camera.fieldOfView = _currentZoom;
    }

    private void HandleRotation()
    {
        if (!AllowRotation) return;

        // Middle mouse button to rotate camera
        if (Input.GetMouseButton(2))
        {
            float mouseX = Input.GetAxis("Mouse X") * RotationSpeed;
            transform.RotateAround(Target.position, Vector3.up, mouseX);
            
            // Update offset to maintain relative position
            Offset = transform.position - Target.position;
        }
    }

    public void SetTarget(Transform newTarget)
    {
        Target = newTarget;
    }

    public void SetCameraStyle(CameraStyle style)
    {
        switch (style)
        {
            case CameraStyle.TopDown:
                Offset = new Vector3(0, 40, -10); // Very high for maximum overview
                break;
            case CameraStyle.ThirdPerson:
                Offset = new Vector3(0, 35, -25); // Maximum wide view
                break;
            case CameraStyle.Strategic:
                Offset = new Vector3(0, 50, -15); // Ultra-wide strategic view
                break;
        }
        
        // Camera will automatically look at player due to HandleCameraFollow changes
        Debug.Log($"Camera style set to: {style} with offset: {Offset}");
    }
}

public enum CameraStyle
{
    TopDown,
    ThirdPerson,
    Strategic
}