using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Follow Settings")]
    public Transform Target; // Will be set to LocalPlayer
    public Vector3 Offset = new Vector3(0, 3, -8); // Better for 3rd person
    public float SmoothTime = 0.2f; // Faster response
    public bool FollowPlayer = true;

    [Header("Zoom Settings")]
    public float MinZoom = 5f;
    public float MaxZoom = 20f;
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
                Offset = new Vector3(0, 15, -10);
                transform.rotation = Quaternion.Euler(60, 0, 0);
                break;
            case CameraStyle.ThirdPerson:
                Offset = new Vector3(0, 3, -8);
                transform.rotation = Quaternion.Euler(15, 0, 0);
                break;
            case CameraStyle.Strategic:
                Offset = new Vector3(0, 20, -5);
                transform.rotation = Quaternion.Euler(75, 0, 0);
                break;
        }
    }
}

public enum CameraStyle
{
    TopDown,
    ThirdPerson,
    Strategic
}