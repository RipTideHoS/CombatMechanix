using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Individual equipment slot that can hold an equipped item and handle clicks and hover events
/// Similar to InventorySlot but specialized for equipment with slot type validation
/// </summary>
public class EquipmentSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Slot Properties")]
    public int SlotIndex = -1;
    public string SlotType = string.Empty; // "Helmet", "Chest", "Legs", "Weapon", "Offhand", "Accessory"
    
    [Header("UI Components")]
    public Image SlotBackground;
    public Image ItemIcon;
    public Text SlotLabel; // Shows slot type when empty
    
    [Header("Visual Settings")]
    public Color EmptySlotColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
    public Color OccupiedSlotColor = new Color(0.4f, 0.4f, 0.4f, 0.9f);
    public Color HighlightColor = new Color(0.6f, 0.6f, 0.8f, 1f);
    
    // Current equipped item data
    private EquippedItem _currentItem;
    private bool _isOccupied = false;
    
    // Events
    public System.Action<int, EquippedItem> OnSlotClicked;
    public System.Action<int, EquippedItem> OnSlotRightClicked;
    public System.Action<int, EquippedItem> OnSlotHoverEnter;
    public System.Action<int, EquippedItem> OnSlotHoverExit;
    
    private void Awake()
    {
        // Ensure we have required components
        if (SlotBackground == null)
            SlotBackground = GetComponent<Image>();
        
        if (ItemIcon == null)
            ItemIcon = transform.Find("ItemIcon")?.GetComponent<Image>();
        
        if (SlotLabel == null)
            SlotLabel = transform.Find("SlotLabel")?.GetComponent<Text>();
            
        // Start with empty slot appearance
        SetSlotAppearance(false);
    }
    
    /// <summary>
    /// Set equipped item in this slot
    /// </summary>
    public void SetItem(EquippedItem item, Sprite itemIcon = null)
    {
        _currentItem = item;
        _isOccupied = item != null;
        
        if (_isOccupied)
        {
            // Hide slot label and show item icon
            if (SlotLabel != null)
                SlotLabel.gameObject.SetActive(false);
            
            if (ItemIcon != null)
            {
                ItemIcon.gameObject.SetActive(true);
                ItemIcon.sprite = itemIcon;
                
                // If no icon provided, show placeholder with rarity color
                if (itemIcon == null)
                {
                    ItemIcon.color = GetRarityColor(item.Rarity);
                }
                else
                {
                    ItemIcon.color = Color.white;
                }
            }
            
            SetSlotAppearance(true);
            Debug.Log($"[EquipmentSlot] Set item in {SlotType} slot: {item.ItemName}");
        }
        else
        {
            ClearSlot();
        }
    }
    
    /// <summary>
    /// Clear this equipment slot
    /// </summary>
    public void ClearSlot()
    {
        _currentItem = null;
        _isOccupied = false;
        
        // Show slot label and hide item icon
        if (SlotLabel != null)
            SlotLabel.gameObject.SetActive(true);
            
        if (ItemIcon != null)
            ItemIcon.gameObject.SetActive(false);
        
        SetSlotAppearance(false);
    }
    
    /// <summary>
    /// Update slot visual appearance
    /// </summary>
    private void SetSlotAppearance(bool occupied)
    {
        if (SlotBackground != null)
        {
            SlotBackground.color = occupied ? OccupiedSlotColor : EmptySlotColor;
        }
    }
    
    /// <summary>
    /// Get color based on item rarity for placeholder icons
    /// </summary>
    private Color GetRarityColor(string rarity)
    {
        string expandedRarity = InventorySlot.ExpandRarityCode(rarity);
        
        switch (expandedRarity?.ToLower())
        {
            case "common":
                return Color.white;
            case "uncommon":
                return Color.green;
            case "rare":
                return Color.blue;
            case "epic":
                return Color.magenta;
            case "legendary":
                return Color.yellow;
            default:
                return Color.gray;
        }
    }
    
    /// <summary>
    /// Handle mouse clicks on this slot
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            // Left click
            OnSlotClicked?.Invoke(SlotIndex, _currentItem);
            
            // Brief highlight effect
            StartCoroutine(HighlightSlot());
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            // Right click (unequip if occupied)
            OnSlotRightClicked?.Invoke(SlotIndex, _currentItem);
            
            // Brief highlight effect
            StartCoroutine(HighlightSlot());
        }
    }
    
    /// <summary>
    /// Brief highlight effect when slot is clicked
    /// </summary>
    private System.Collections.IEnumerator HighlightSlot()
    {
        Color originalColor = SlotBackground.color;
        SlotBackground.color = HighlightColor;
        
        yield return new WaitForSeconds(0.1f);
        
        SlotBackground.color = originalColor;
    }
    
    /// <summary>
    /// Get current equipped item in this slot
    /// </summary>
    public EquippedItem GetItem()
    {
        return _currentItem;
    }
    
    /// <summary>
    /// Check if slot is occupied
    /// </summary>
    public bool IsOccupied()
    {
        return _isOccupied;
    }
    
    /// <summary>
    /// Check if this slot can accept a specific item type
    /// </summary>
    public bool CanAcceptItemType(string itemCategory, string itemType)
    {
        if (_isOccupied)
            return false; // Slot already occupied
            
        // Validate item can be equipped to this slot type
        return ValidateItemForSlot(itemCategory, itemType, SlotType);
    }
    
    /// <summary>
    /// Validate that an item category/type can be equipped to a specific slot type
    /// Mirrors the server-side validation logic
    /// </summary>
    private bool ValidateItemForSlot(string itemCategory, string itemType, string slotType)
    {
        if (string.IsNullOrEmpty(itemCategory) || string.IsNullOrEmpty(slotType))
            return false;
        
        // Define allowed categories for each slot type (matching server logic)
        switch (slotType)
        {
            case "Helmet":
                return itemCategory.Equals("Helmet", System.StringComparison.OrdinalIgnoreCase) ||
                       itemCategory.Equals("Armor", System.StringComparison.OrdinalIgnoreCase);
                       
            case "Chest":
                return itemCategory.Equals("Chest", System.StringComparison.OrdinalIgnoreCase) ||
                       itemCategory.Equals("Armor", System.StringComparison.OrdinalIgnoreCase);
                       
            case "Legs":
                return itemCategory.Equals("Legs", System.StringComparison.OrdinalIgnoreCase) ||
                       itemCategory.Equals("Armor", System.StringComparison.OrdinalIgnoreCase);
                       
            case "Weapon":
                return itemCategory.Equals("Weapon", System.StringComparison.OrdinalIgnoreCase) ||
                       itemCategory.Equals("Sword", System.StringComparison.OrdinalIgnoreCase) ||
                       itemCategory.Equals("Axe", System.StringComparison.OrdinalIgnoreCase) ||
                       itemCategory.Equals("Bow", System.StringComparison.OrdinalIgnoreCase) ||
                       itemCategory.Equals("Staff", System.StringComparison.OrdinalIgnoreCase);
                       
            case "Offhand":
                return itemCategory.Equals("Shield", System.StringComparison.OrdinalIgnoreCase) ||
                       itemCategory.Equals("Offhand", System.StringComparison.OrdinalIgnoreCase) ||
                       itemCategory.Equals("Weapon", System.StringComparison.OrdinalIgnoreCase);
                       
            case "Accessory":
                return itemCategory.Equals("Ring", System.StringComparison.OrdinalIgnoreCase) ||
                       itemCategory.Equals("Amulet", System.StringComparison.OrdinalIgnoreCase) ||
                       itemCategory.Equals("Accessory", System.StringComparison.OrdinalIgnoreCase);
                       
            default:
                return false;
        }
    }
    
    /// <summary>
    /// Handle mouse enter hover event
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        // Trigger hover event (for both occupied and empty slots)
        OnSlotHoverEnter?.Invoke(SlotIndex, _currentItem);
        
        // Visual feedback for empty slots
        if (!_isOccupied && SlotLabel != null)
        {
            SlotLabel.color = new Color(1f, 1f, 1f, 1f); // Brighten label on hover
        }
    }
    
    /// <summary>
    /// Handle mouse exit hover event
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        // Always trigger hover exit to hide details panel
        OnSlotHoverExit?.Invoke(SlotIndex, _currentItem);
        
        // Reset visual feedback for empty slots
        if (!_isOccupied && SlotLabel != null)
        {
            SlotLabel.color = new Color(0.7f, 0.7f, 0.7f, 0.8f); // Return to normal label color
        }
    }
    
    /// <summary>
    /// Get user-friendly slot type display name
    /// </summary>
    public string GetSlotDisplayName()
    {
        return SlotType switch
        {
            "Helmet" => "Helmet",
            "Chest" => "Chest Armor",
            "Legs" => "Leg Armor",
            "Weapon" => "Main Weapon",
            "Offhand" => "Offhand/Shield",
            "Accessory" => "Accessory",
            _ => SlotType
        };
    }
    
    /// <summary>
    /// Get a tooltip description for this slot type
    /// </summary>
    public string GetSlotTooltip()
    {
        return SlotType switch
        {
            "Helmet" => "Head protection - Helmets and Armor",
            "Chest" => "Body protection - Chest Armor",
            "Legs" => "Leg protection - Leg Armor",
            "Weapon" => "Primary weapon - Swords, Axes, Bows, Staves",
            "Offhand" => "Secondary item - Shields, Offhand weapons",
            "Accessory" => "Magical items - Rings, Amulets",
            _ => $"Equipment slot: {SlotType}"
        };
    }
}