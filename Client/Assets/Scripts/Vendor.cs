using UnityEngine;

public class Vendor : MonoBehaviour
{
    [Header("Vendor Settings")]
    public float InteractionRange = 5f;
    public Color VendorColor = new Color(0.6f, 0.3f, 0.1f, 1f); // Brown color
    public string VendorName = "General Merchant";
    
    [Header("Visual Feedback")]
    public Color HighlightColor = new Color(0.8f, 0.5f, 0.2f, 1f); // Brighter brown when highlighted
    
    private Transform _playerTransform;
    private Renderer _renderer;
    private BoxCollider _collider;
    private Material _originalMaterial;
    private Material _highlightMaterial;
    private bool _isPlayerInRange = false;
    
    public static System.Action<Vendor> OnVendorInteracted;

    private void Start()
    {
        SetupVendor();
    }

    private void SetupVendor()
    {
        // Find player
        var playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            _playerTransform = playerController.transform;
        }

        // Create the visual vendor box (using a primitive cube)
        GameObject vendorBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
        vendorBox.transform.SetParent(transform);
        vendorBox.transform.localPosition = Vector3.zero;

        // Set vendor size - make it slightly larger than player for visibility
        vendorBox.transform.localScale = new Vector3(1.2f, 2.2f, 1.2f);

        // Get renderer and create materials
        _renderer = vendorBox.GetComponent<Renderer>();
        if (_renderer != null)
        {
            // Create original brown material
            _originalMaterial = new Material(Shader.Find("Standard"));
            _originalMaterial.color = VendorColor;
            
            // Create highlight material (brighter brown)
            _highlightMaterial = new Material(Shader.Find("Standard"));
            _highlightMaterial.color = HighlightColor;
            
            _renderer.material = _originalMaterial;
        }

        // Set up BoxCollider for interaction (make it a trigger)
        _collider = vendorBox.GetComponent<BoxCollider>();
        if (_collider == null)
        {
            _collider = vendorBox.AddComponent<BoxCollider>();
        }
        
        // Make it a trigger for interaction detection
        _collider.isTrigger = true;

        Debug.Log($"Vendor '{VendorName}' created with interaction range {InteractionRange}");
    }

    private void Update()
    {
        CheckPlayerProximity();
        HandleInput();
    }
    
    private void CheckPlayerProximity()
    {
        if (_playerTransform == null) return;
        
        float distance = Vector3.Distance(transform.position, _playerTransform.position);
        bool wasInRange = _isPlayerInRange;
        _isPlayerInRange = distance <= InteractionRange;
        
        // Visual feedback when player enters/exits range
        if (_isPlayerInRange != wasInRange)
        {
            UpdateVisualFeedback();
            
            if (_isPlayerInRange)
            {
                Debug.Log($"Player entered {VendorName} interaction range - Press V to interact");
                // Future: Show interaction prompt
            }
            else
            {
                Debug.Log($"Player left {VendorName} interaction range");
                // Future: Hide interaction prompt
            }
        }
    }
    
    private void UpdateVisualFeedback()
    {
        if (_renderer == null) return;
        
        if (_isPlayerInRange)
        {
            _renderer.material = _highlightMaterial;
        }
        else
        {
            _renderer.material = _originalMaterial;
        }
    }
    
    private void HandleInput()
    {
        if (!_isPlayerInRange) return;
        
        // V key only interaction to avoid conflicts with other systems
        if (Input.GetKeyDown(KeyCode.V))
        {
            InteractWithVendor();
        }
    }
    
    private void InteractWithVendor()
    {
        Debug.Log($"Player interacted with {VendorName}!");
        
        // Trigger vendor interaction event
        OnVendorInteracted?.Invoke(this);
    }
    
    // Public methods for external access
    public bool IsPlayerInRange()
    {
        return _isPlayerInRange;
    }
    
    public float GetDistanceToPlayer()
    {
        if (_playerTransform == null) return float.MaxValue;
        return Vector3.Distance(transform.position, _playerTransform.position);
    }
}