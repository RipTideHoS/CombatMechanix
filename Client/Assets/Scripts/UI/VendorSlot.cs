using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Handles individual vendor slot interactions and display
/// Shows item icons, names, and sell prices
/// </summary>
public class VendorSlot : MonoBehaviour, IPointerClickHandler
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
    
    private ItemIconManager _iconManager;
    private bool _isInitialized = false;
    
    private void Awake()
    {
        InitializeComponents();
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
    
    private void InitializeComponents()
    {
        // Auto-find components if not assigned
        if (SlotBackground == null)
            SlotBackground = GetComponent<Image>();
            
        if (ItemIcon == null)
            ItemIcon = transform.Find("ItemIcon")?.GetComponent<Image>();
            
        if (PriceText == null)
            PriceText = transform.Find("PriceText")?.GetComponent<Text>();
            
        // Start with empty appearance
        UpdateVisualState(false);
    }
    
    public void UpdateSlot(InventoryItem item, int sellPrice)
    {
        CurrentItem = item;
        SellPrice = sellPrice;
        
        if (item != null && !string.IsNullOrEmpty(item.ItemType))
        {
            // Show item
            UpdateItemDisplay(item);
            UpdatePriceDisplay(sellPrice);
            UpdateVisualState(true);
        }
        else
        {
            ClearSlot();
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
        if (ItemIcon == null || item == null) return;
        
        // Try to get item icon from IconManager
        if (_iconManager != null)
        {
            Sprite itemSprite = _iconManager.GetItemIcon(item.ItemType);
            if (itemSprite != null)
            {
                ItemIcon.sprite = itemSprite;
                ItemIcon.color = Color.white;
                return;
            }
        }
        
        // Fallback: Show placeholder colored icon
        ItemIcon.sprite = null;
        ItemIcon.color = GetItemTypeColor(item.ItemType);
    }
    
    private void UpdatePriceDisplay(int price)
    {
        if (PriceText != null)
        {
            PriceText.text = price > 0 ? $"{price}g" : "";
        }
    }
    
    private void UpdateVisualState(bool hasItem)
    {
        if (SlotBackground == null) return;
        
        SlotBackground.color = hasItem ? OccupiedSlotColor : EmptySlotColor;
    }
    
    private Color GetItemTypeColor(string itemType)
    {
        // Fallback colors for different item types
        switch (itemType?.ToLower())
        {
            case "sword": return Color.red;
            case "shield": return Color.blue;
            case "helmet": return Color.gray;
            case "chestplate": return Color.yellow;
            case "boots": return new Color(0.6f, 0.3f, 0.1f, 1f); // Brown color
            case "ring": return Color.magenta;
            case "potion": return Color.green;
            default: return Color.white;
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
    
    // Visual feedback for hover (optional)
    private void OnMouseEnter()
    {
        if (SlotBackground != null && CurrentItem != null)
        {
            SlotBackground.color = HighlightColor;
        }
    }
    
    private void OnMouseExit()
    {
        if (SlotBackground != null)
        {
            UpdateVisualState(CurrentItem != null);
        }
    }
    
    // Public method to get item details for tooltips
    public string GetItemDetails()
    {
        if (CurrentItem == null) return "";
        
        return $"{CurrentItem.ItemName}\nSell Price: {SellPrice} gold\nQuantity: {CurrentItem.Quantity}";
    }
}