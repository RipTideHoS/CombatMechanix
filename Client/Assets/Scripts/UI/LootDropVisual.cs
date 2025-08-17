using UnityEngine;
using System.Collections;

/// <summary>
/// Individual loot drop visual component
/// Handles bobbing animation, mouse interaction, and visual effects
/// </summary>
public class LootDropVisual : MonoBehaviour
{
    // Loot data
    private NetworkMessages.LootDropMessage _lootData;
    private LootDropManager _lootManager;
    
    // Animation properties
    private Vector3 _basePosition;
    private float _bobTimer = 0f;
    private float _rotationSpeed = 45f; // Degrees per second
    
    // Mouse interaction
    private bool _isHighlighted = false;
    private Renderer _renderer;
    private Color _originalColor;
    private Color _highlightColor;

    /// <summary>
    /// Initialize the loot drop visual with data from server
    /// </summary>
    public void Initialize(NetworkMessages.LootDropMessage lootData, LootDropManager lootManager)
    {
        _lootData = lootData;
        _lootManager = lootManager;
        _basePosition = transform.position;
        
        // Get renderer for color changes
        _renderer = GetComponent<Renderer>();
        if (_renderer != null)
        {
            _originalColor = _renderer.material.color;
            _highlightColor = _originalColor * 1.3f; // Brighter version for highlight
        }

        Debug.Log($"[LootDropVisual] Initialized: {_lootData.Item.ItemName} (ID: {_lootData.LootId})");
    }

    private void Update()
    {
        // Bobbing animation
        _bobTimer += Time.deltaTime * _lootManager.BobSpeed;
        float bobOffset = Mathf.Sin(_bobTimer) * _lootManager.BobAmount;
        transform.position = _basePosition + Vector3.up * bobOffset;
        
        // Rotation animation
        transform.Rotate(Vector3.up, _rotationSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Handle mouse enter (highlight effect)
    /// </summary>
    private void OnMouseEnter()
    {
        if (!_isHighlighted && _renderer != null)
        {
            _isHighlighted = true;
            _renderer.material.color = _highlightColor;
            
            // Show tooltip/info (placeholder for now)
            Debug.Log($"[LootDropVisual] Hovering over: {_lootData.Item.ItemName} ({_lootData.Item.Rarity})");
        }
    }

    /// <summary>
    /// Handle mouse exit (remove highlight)
    /// </summary>
    private void OnMouseExit()
    {
        if (_isHighlighted && _renderer != null)
        {
            _isHighlighted = false;
            _renderer.material.color = _originalColor;
        }
    }

    /// <summary>
    /// Handle mouse click (attempt pickup)
    /// </summary>
    private void OnMouseDown()
    {
        Debug.Log($"[LootDropVisual] Clicked on loot: {_lootData.Item.ItemName}");
        
        if (_lootManager != null)
        {
            _lootManager.AttemptPickup(_lootData.LootId);
        }
        else
        {
            Debug.LogError("[LootDropVisual] LootManager reference is null!");
        }
    }

    /// <summary>
    /// Get the loot data for this visual
    /// </summary>
    public NetworkMessages.LootDropMessage GetLootData()
    {
        return _lootData;
    }

    /// <summary>
    /// Get the distance to a specific position
    /// </summary>
    public float GetDistanceTo(Vector3 position)
    {
        return Vector3.Distance(transform.position, position);
    }

    /// <summary>
    /// Start a pickup animation (called when pickup is successful)
    /// </summary>
    public void StartPickupAnimation()
    {
        StartCoroutine(PickupAnimationCoroutine());
    }

    /// <summary>
    /// Animate the loot being picked up
    /// </summary>
    private IEnumerator PickupAnimationCoroutine()
    {
        Debug.Log($"[LootDropVisual] Starting pickup animation for {_lootData.Item.ItemName}");
        
        float animationTime = 0.5f;
        Vector3 startPosition = transform.position;
        Vector3 endPosition = startPosition + Vector3.up * 2f; // Move up
        Vector3 startScale = transform.localScale;
        Vector3 endScale = Vector3.zero; // Shrink to nothing
        
        float elapsed = 0f;
        while (elapsed < animationTime)
        {
            float t = elapsed / animationTime;
            
            // Move up and shrink
            transform.position = Vector3.Lerp(startPosition, endPosition, t);
            transform.localScale = Vector3.Lerp(startScale, endScale, t);
            
            // Fade out if we have a renderer
            if (_renderer != null)
            {
                Color color = _renderer.material.color;
                color.a = 1f - t;
                _renderer.material.color = color;
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Animation complete, destroy the object
        Destroy(gameObject);
    }

    /// <summary>
    /// Display item information in the console (for debugging)
    /// </summary>
    public void DisplayItemInfo()
    {
        var item = _lootData.Item;
        Debug.Log($"=== Loot Item Info ===\n" +
                  $"Name: {item.ItemName}\n" +
                  $"Type: {item.ItemType}\n" +
                  $"Rarity: {item.Rarity}\n" +
                  $"Description: {item.ItemDescription}\n" +
                  $"Value: {item.Value}\n" +
                  $"Attack Power: {item.AttackPower}\n" +
                  $"Defense Power: {item.DefensePower}\n" +
                  $"Stackable: {item.IsStackable} (Max: {item.MaxStackSize})");
    }

    /// <summary>
    /// Handle trigger enter for alternative interaction method
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // If player walks into the loot, we could auto-highlight or show info
        if (other.CompareTag("Player"))
        {
            Debug.Log($"[LootDropVisual] Player near loot: {_lootData.Item.ItemName}");
        }
    }

    /// <summary>
    /// Handle trigger exit
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log($"[LootDropVisual] Player left loot area: {_lootData.Item.ItemName}");
        }
    }
}