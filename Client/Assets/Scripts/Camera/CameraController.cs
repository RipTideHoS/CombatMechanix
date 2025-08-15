using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour
{
    [Header("Follow Settings")]
    public Transform Target; // Will be set to LocalPlayer
    public Vector3 Offset = new Vector3(0, 35, -25); // Very high and far back for maximum wide view
    public float SmoothTime = 0.15f; // Faster response
    public bool FollowPlayer = true;

    [Header("Zoom Settings")]
    public float MinDistance = 5f;
    public float MaxDistance = 150f;
    public float ZoomSpeed = 10f;

    [Header("Rotation Settings")]
    public bool AllowRotation = true;
    public float RotationSpeed = 2f;
    public float VerticalRotationSpeed = 1f;
    public float MinVerticalAngle = -30f;
    public float MaxVerticalAngle = 80f;

    private Vector3 _velocity = Vector3.zero;
    private Camera _camera;
    private float _currentDistance;
    private float _horizontalAngle = 0f;
    private float _verticalAngle = 45f;

    private void Start()
    {
        _camera = GetComponent<Camera>();
        _currentDistance = Offset.magnitude;
        
        // Calculate initial angles from offset
        _horizontalAngle = Mathf.Atan2(Offset.x, Offset.z) * Mathf.Rad2Deg;
        _verticalAngle = Mathf.Asin(Offset.y / _currentDistance) * Mathf.Rad2Deg;
        
        // Force enable rotation for debugging
        AllowRotation = true;
        
        // Camera initialized successfully
        
        // Find the local player automatically
        if (Target == null)
        {
            var localPlayer = FindObjectOfType<PlayerController>();
            if (localPlayer != null)
            {
                Target = localPlayer.transform;
                // Target found and set
            }
            else
            {
                Debug.LogWarning("[Camera] No PlayerController found for target!");
            }
        }
        // Target configuration complete
    }

    private void LateUpdate()
    {
        if (!FollowPlayer || Target == null)
        {
            return;
        }

        HandleCameraFollow();
        HandleZoom();
        HandleRotation();
    }

    private void HandleCameraFollow()
    {
        // Calculate camera position based on angles and distance
        float horizontalRad = _horizontalAngle * Mathf.Deg2Rad;
        float verticalRad = _verticalAngle * Mathf.Deg2Rad;
        
        Vector3 offset = new Vector3(
            Mathf.Sin(horizontalRad) * Mathf.Cos(verticalRad) * _currentDistance,
            Mathf.Sin(verticalRad) * _currentDistance,
            Mathf.Cos(horizontalRad) * Mathf.Cos(verticalRad) * _currentDistance
        );
        
        Vector3 desiredPosition = Target.position + offset;
        
        // Direct position update for immediate response - no smoothing
        transform.position = desiredPosition;
        
        // Direct rotation update for immediate response
        Vector3 lookDirection = Target.position - transform.position;
        if (lookDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(lookDirection);
        }
    }

    private void HandleZoom()
    {
        // Mouse scroll wheel zoom - adjusts distance from player
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        _currentDistance -= scroll * ZoomSpeed;
        _currentDistance = Mathf.Clamp(_currentDistance, MinDistance, MaxDistance);
    }

    private void HandleRotation()
    {
        if (!AllowRotation)
        {
            return;
        }

        // Check if mouse is over UI element
        bool isOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (isOverUI)
        {
            return;
        }

        // Right mouse button to rotate camera
        if (Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            
            // Apply rotation
            _horizontalAngle += mouseX * RotationSpeed;
            _verticalAngle -= mouseY * VerticalRotationSpeed;
            _verticalAngle = Mathf.Clamp(_verticalAngle, MinVerticalAngle, MaxVerticalAngle);
        }
    }

    public void SetTarget(Transform newTarget)
    {
        Target = newTarget;
    }

    public void SetCameraStyle(CameraStyle style)
    {
        Vector3 newOffset;
        switch (style)
        {
            case CameraStyle.TopDown:
                newOffset = new Vector3(0, 40, -10); // Very high for maximum overview
                break;
            case CameraStyle.ThirdPerson:
                newOffset = new Vector3(0, 35, -25); // Maximum wide view
                break;
            case CameraStyle.Strategic:
                newOffset = new Vector3(0, 50, -15); // Ultra-wide strategic view
                break;
            default:
                newOffset = Offset;
                break;
        }
        
        // Update camera parameters based on new offset
        _currentDistance = newOffset.magnitude;
        _horizontalAngle = Mathf.Atan2(newOffset.x, newOffset.z) * Mathf.Rad2Deg;
        _verticalAngle = Mathf.Asin(newOffset.y / _currentDistance) * Mathf.Rad2Deg;
        
        // Camera style updated
    }
}

public enum CameraStyle
{
    TopDown,
    ThirdPerson,
    Strategic
}