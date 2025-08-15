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
        
        Debug.Log($"[Camera] Initialized - Distance: {_currentDistance}, Horizontal: {_horizontalAngle}, Vertical: {_verticalAngle}");
        Debug.Log($"[Camera] AllowRotation: {AllowRotation}, FollowPlayer: {FollowPlayer}");
        
        // Find the local player automatically
        if (Target == null)
        {
            var localPlayer = FindObjectOfType<PlayerController>();
            if (localPlayer != null)
            {
                Target = localPlayer.transform;
                Debug.Log($"[Camera] Target found: {localPlayer.name}");
            }
            else
            {
                Debug.LogWarning("[Camera] No PlayerController found for target!");
            }
        }
        else
        {
            Debug.Log($"[Camera] Target already set: {Target.name}");
        }
    }

    private void LateUpdate()
    {
        if (!FollowPlayer || Target == null)
        {
            if (!FollowPlayer)
                Debug.Log("[Camera] LateUpdate skipped - FollowPlayer is false");
            if (Target == null)
                Debug.Log("[Camera] LateUpdate skipped - Target is null");
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
        if (Mathf.Abs(scroll) > 0.01f)
        {
            Debug.Log($"[Camera] Mouse scroll detected: {scroll}, current distance: {_currentDistance}");
        }
        
        _currentDistance -= scroll * ZoomSpeed;
        _currentDistance = Mathf.Clamp(_currentDistance, MinDistance, MaxDistance);
        
        if (Mathf.Abs(scroll) > 0.01f)
        {
            Debug.Log($"[Camera] New distance after zoom: {_currentDistance}");
        }
    }

    private void HandleRotation()
    {
        // Debug every few frames to avoid spam
        if (Time.frameCount % 60 == 0)
        {
            Debug.Log($"[Camera] HandleRotation called - AllowRotation: {AllowRotation}");
        }
        
        if (!AllowRotation)
        {
            return;
        }

        // Check if mouse is over UI element
        bool isOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (isOverUI)
        {
            if (Time.frameCount % 120 == 0)
            {
                Debug.Log("[Camera] Mouse is over UI - skipping camera rotation");
            }
            return;
        }

        // Check for right mouse button
        bool rightMouseDown = Input.GetMouseButton(1);
        
        // Debug input detection every few frames
        if (Time.frameCount % 30 == 0)
        {
            Debug.Log($"[Camera] Right mouse button state: {rightMouseDown}, Over UI: {isOverUI}");
        }

        // Right mouse button to rotate camera
        if (rightMouseDown)
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            
            // Only log when there's actual mouse movement
            if (Mathf.Abs(mouseX) > 0.01f || Mathf.Abs(mouseY) > 0.01f)
            {
                Debug.Log($"[Camera] Mouse movement detected - X: {mouseX}, Y: {mouseY}");
                Debug.Log($"[Camera] Current angles - Horizontal: {_horizontalAngle:F2}, Vertical: {_verticalAngle:F2}");
                
                // Apply rotation
                _horizontalAngle += mouseX * RotationSpeed;
                _verticalAngle -= mouseY * VerticalRotationSpeed;
                _verticalAngle = Mathf.Clamp(_verticalAngle, MinVerticalAngle, MaxVerticalAngle);
                
                Debug.Log($"[Camera] New angles - Horizontal: {_horizontalAngle:F2}, Vertical: {_verticalAngle:F2}");
            }
            else if (rightMouseDown)
            {
                // Log when right mouse is down but no movement is detected
                if (Time.frameCount % 30 == 0)
                {
                    Debug.Log("[Camera] Right mouse down but no movement detected");
                }
            }
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
        
        Debug.Log($"Camera style set to: {style} with distance: {_currentDistance}");
    }
}

public enum CameraStyle
{
    TopDown,
    ThirdPerson,
    Strategic
}