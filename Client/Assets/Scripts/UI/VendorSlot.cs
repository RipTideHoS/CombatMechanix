using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Handles individual vendor slot interactions and display
/// Shows item icons, names, and sell prices
/// </summary>
public class VendorSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Slot Components")]
    public Image SlotBackground;
    public Image ItemIcon;
    public Text PriceText;
    
    [Header("Visual Settings")]
    public Color EmptySlotColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
    public Color OccupiedSlotColor = new Color(0.4f, 0.5f, 0.4f, 0.9f); // Slightly green for sellable items
    public Color HighlightColor = new Color(0.6f, 0.7f, 0.6f, 1f);
    
    // Current item data
    public InventoryItem CurrentItem { get; private set; }
    public int SellPrice { get; private set; }
    public int SlotIndex { get; set; } = -1;
    
    // Events
    public System.Action<int> OnSlotClicked;
    public System.Action<int, InventoryItem> OnSlotHoverEnter;
    public System.Action<int, InventoryItem> OnSlotHoverExit;
    
    private ItemIconManager _iconManager;
    private bool _isInitialized = false;
    
    private void Awake()
    {
        // Component references will be set directly by VendorUI
        // Just initialize visual state when components are available
    }
    
    private void Start()
    {
        // Find IconManager if not set
        if (_iconManager == null)
        {
            _iconManager = FindObjectOfType<ItemIconManager>();
        }
        
        _isInitialized = true;
    }
    
    /// <summary>
    /// Set the ItemIconManager reference (called by VendorUI)
    /// </summary>
    public void SetIconManager(ItemIconManager iconManager)
    {
        _iconManager = iconManager;
    }
    
    private void InitializeComponents()
    {
        Debug.Log($"[VendorSlot] InitializeComponents called for {gameObject.name}");
        
        // Auto-find components if not assigned
        if (SlotBackground == null)
        {
            SlotBackground = GetComponent<Image>();
            Debug.Log($"[VendorSlot] SlotBackground found: {SlotBackground != null}");
        }
        
        // Use more robust component finding that waits for child creation
        StartCoroutine(FindChildComponentsWhenReady());
    }
    
    private System.Collections.IEnumerator FindChildComponentsWhenReady()
    {
        // Wait a frame to ensure child objects are created
        yield return null;
        
        if (ItemIcon == null)
        {
            ItemIcon = transform.Find("ItemIcon")?.GetComponent<Image>();
            Debug.Log($"[VendorSlot] ItemIcon found after delay: {ItemIcon != null}");
        }
            
        if (PriceText == null)
        {
            PriceText = transform.Find("PriceText")?.GetComponent<Text>();
            Debug.Log($"[VendorSlot] PriceText found after delay: {PriceText != null}");
        }
        
        // Start with empty appearance
        UpdateVisualState(false);
    }
    
    public void UpdateSlot(InventoryItem item, int sellPrice)
    {
        CurrentItem = item;
        SellPrice = sellPrice;
        
        // Emergency component finder - ensure components are available
        EnsureComponentsAreFound();
        
        Debug.Log($"[VendorSlot] UpdateSlot called - Item: {item?.ItemName}, Price: {sellPrice}g, ItemIcon: {ItemIcon != null}, PriceText: {PriceText != null}");
        
        if (item != null && !string.IsNullOrEmpty(item.ItemType))
        {
            // Show item (simplified approach matching InventorySlot)
            UpdateItemDisplay(item);
            UpdatePriceDisplay(sellPrice);
            UpdateVisualState(true);
            
            // Show the slot contents
            if (ItemIcon != null) ItemIcon.gameObject.SetActive(true);
            if (PriceText != null) PriceText.gameObject.SetActive(true);
        }
        else
        {
            ClearSlot();
        }
    }
    
    private void EnsureComponentsAreFound()
    {
        if (SlotBackground == null)
        {
            SlotBackground = GetComponent<Image>();
            // Ensure raycast target is enabled for pointer events
            if (SlotBackground != null)
                SlotBackground.raycastTarget = true;
        }
            
        if (ItemIcon == null)
        {
            Transform iconTransform = transform.Find("ItemIcon");
            if (iconTransform != null)
                ItemIcon = iconTransform.GetComponent<Image>();
        }
        
        if (PriceText == null)
        {
            Transform priceTransform = transform.Find("PriceText");
            if (priceTransform != null)
                PriceText = priceTransform.GetComponent<Text>();
        }
    }
    
    public void ClearSlot()
    {
        CurrentItem = null;
        SellPrice = 0;
        
        // Clear item icon
        if (ItemIcon != null)
        {
            ItemIcon.sprite = null;
            ItemIcon.color = Color.clear;
        }
        
        // Clear price text
        if (PriceText != null)
        {
            PriceText.text = "";
        }
        
        UpdateVisualState(false);
    }
    
    private void UpdateItemDisplay(InventoryItem item)
    {
        if (ItemIcon == null || item == null) 
        {
            Debug.LogError($"[VendorSlot] Cannot update item display - ItemIcon null: {ItemIcon == null}, item null: {item == null}");
            return;
        }
        
        // Get icon from IconManager if available (matching InventorySlot approach)
        Sprite itemSprite = null;
        if (_iconManager != null)
        {
            itemSprite = _iconManager.GetItemIcon(item.IconName);
        }
        
        if (itemSprite != null)
        {
            // Use actual icon sprite
            ItemIcon.sprite = itemSprite;
            ItemIcon.color = Color.white;
            Debug.Log($"[VendorSlot] Set item sprite from IconManager for {item.ItemName}");
        }
        else
        {
            // Use placeholder with rarity color (matching InventorySlot logic)
            ItemIcon.sprite = null;
            ItemIcon.color = GetRarityColor(item.Rarity);
            Debug.Log($"[VendorSlot] Set placeholder color for {item.ItemName} - Color: {ItemIcon.color}");
        }
        
        ItemIcon.enabled = true;
    }
    
    private void UpdatePriceDisplay(int price)
    {
        if (PriceText != null)
        {
            string priceText = price > 0 ? $"{price}g" : "";
            PriceText.text = priceText;
            Debug.Log($"[VendorSlot] Updated price display to: '{priceText}'");
        }
        else
        {
            Debug.LogError("[VendorSlot] PriceText is null - cannot update price display");
        }
    }
    
    private void UpdateVisualState(bool hasItem)
    {
        if (SlotBackground == null) return;
        
        SlotBackground.color = hasItem ? OccupiedSlotColor : EmptySlotColor;
    }
    
    /// <summary>
    /// Get color based on item rarity for placeholder icons (matching InventorySlot)
    /// </summary>
    private Color GetRarityColor(string rarity)
    {
        string expandedRarity = ExpandRarityCode(rarity);
        
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
    /// Expand single-character rarity codes to full text (matching InventorySlot)
    /// </summary>
    public static string ExpandRarityCode(string rarityCode)
    {
        switch (rarityCode?.ToUpper())
        {
            case "C":
                return "Common";
            case "U":
                return "Uncommon";
            case "R":
                return "Rare";
            case "E":
                return "Epic";
            case "L":
                return "Legendary";
            default:
                return rarityCode ?? "Unknown";
        }
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            HandleLeftClick();
        }
    }
    
    private void HandleLeftClick()
    {
        if (CurrentItem != null && !string.IsNullOrEmpty(CurrentItem.ItemType))
        {
            Debug.Log($"[VendorSlot] Clicked slot {SlotIndex} - {CurrentItem.ItemName} for {SellPrice}g");
            
            // Confirm sell with user
            string confirmMessage = $"Sell {CurrentItem.ItemName} for {SellPrice} gold?";
            
            // For now, auto-confirm (later could add confirmation dialog)
            Debug.Log($"[VendorSlot] {confirmMessage} - Auto-confirming");
            OnSlotClicked?.Invoke(SlotIndex);
        }
        else
        {
            Debug.Log($"[VendorSlot] Clicked empty slot {SlotIndex}");
        }
    }
    
    /// <summary>
    /// Handle mouse enter hover event
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log($"[VendorSlot] OnPointerEnter - Slot {SlotIndex}, HasItem: {CurrentItem != null}, ItemName: {CurrentItem?.ItemName}");
        
        // Visual feedback for hover
        if (SlotBackground != null && CurrentItem != null)
        {
            SlotBackground.color = HighlightColor;
        }
        
        // Only trigger hover if slot has an item
        if (CurrentItem != null)
        {
            Debug.Log($"[VendorSlot] Triggering OnSlotHoverEnter for {CurrentItem.ItemName}");
            OnSlotHoverEnter?.Invoke(SlotIndex, CurrentItem);
        }
    }
    
    /// <summary>
    /// Handle mouse exit hover event
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log($"[VendorSlot] OnPointerExit - Slot {SlotIndex}");
        
        // Reset visual feedback
        if (SlotBackground != null)
        {
            UpdateVisualState(CurrentItem != null);
        }
        
        // Always trigger hover exit to hide details panel
        OnSlotHoverExit?.Invoke(SlotIndex, CurrentItem);
    }
    
    // Public method to get item details for tooltips
    public string GetItemDetails()
    {
        if (CurrentItem == null) return "";
        
        return $"{CurrentItem.ItemName}\nSell Price: {SellPrice} gold\nQuantity: {CurrentItem.Quantity}";
    }
}