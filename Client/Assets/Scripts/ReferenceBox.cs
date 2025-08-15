using UnityEngine;

public class ReferenceBox : MonoBehaviour
{
    [Header("Reference Box Settings")]
    public float DistanceFromPlayer = 10f;
    public Color BoxColor = new Color(0.6f, 0.3f, 0.1f, 1f); // Brown color

    private Transform _playerTransform;
    private Camera _mainCamera;
    private Renderer _renderer;
    private BoxCollider _collider;

    private void Start()
    {
        SetupReferenceBox();
        PositionBox();
    }

    private void SetupReferenceBox()
    {
        // Find player and camera
        var playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            _playerTransform = playerController.transform;
        }

        _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            _mainCamera = FindObjectOfType<Camera>();
        }

        // Create the visual box (using a primitive cube)
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.transform.SetParent(transform);
        box.transform.localPosition = Vector3.zero;

        // Size the box to match player size (CharacterController dimensions)
        if (_playerTransform != null)
        {
            var characterController = _playerTransform.GetComponent<CharacterController>();
            if (characterController != null)
            {
                // Match the CharacterController size
                float diameter = characterController.radius * 2f;
                float height = characterController.height;
                box.transform.localScale = new Vector3(diameter, height, diameter);
            }
            else
            {
                // Default size if no CharacterController
                box.transform.localScale = new Vector3(1f, 2f, 1f);
            }
        }
        else
        {
            // Fallback size
            box.transform.localScale = new Vector3(1f, 2f, 1f);
        }

        // Get renderer and apply brown material
        _renderer = box.GetComponent<Renderer>();
        if (_renderer != null)
        {
            Material brownMaterial = new Material(Shader.Find("Standard"));
            brownMaterial.color = BoxColor;
            _renderer.material = brownMaterial;
        }

        // Add BoxCollider for collision (the primitive already has one, but let's ensure it's set up properly)
        _collider = box.GetComponent<BoxCollider>();
        if (_collider == null)
        {
            _collider = box.AddComponent<BoxCollider>();
        }
        
        // Make sure it's solid (not a trigger)
        _collider.isTrigger = false;

        Debug.Log("Reference box created with collision and brown material");
    }

    private void PositionBox()
    {
        if (_playerTransform == null || _mainCamera == null) return;

        // Get camera forward direction (but keep it on the ground plane)
        Vector3 cameraForward = _mainCamera.transform.forward;
        cameraForward.y = 0; // Remove vertical component
        cameraForward.Normalize();

        // Position the box 10 units away from player in camera direction
        Vector3 targetPosition = _playerTransform.position + (cameraForward * DistanceFromPlayer);
        
        // Place the box on the ground (assuming ground is at y=0, adjust if needed)
        targetPosition.y = 0;
        
        transform.position = targetPosition;

        Debug.Log($"Reference box positioned at: {targetPosition} (camera forward: {cameraForward})");
    }

    // Optional: Update position if you want the box to move with the player/camera
    private void Update()
    {
        // Uncomment this if you want the box to always stay 10 units in front of the player
        // PositionBox();
    }

    // Method to reposition the box manually
    public void UpdatePosition()
    {
        PositionBox();
    }

    // Method to change the distance
    public void SetDistance(float newDistance)
    {
        DistanceFromPlayer = newDistance;
        PositionBox();
    }
}