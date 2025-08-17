using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Individual inventory slot that can hold an item and handle clicks
/// </summary>
public class InventorySlot : MonoBehaviour, IPointerClickHandler
{
    [Header("Slot Properties")]
    public int SlotIndex = -1;
    
    [Header("UI Components")]
    public Image SlotBackground;
    public Image ItemIcon;
    public Text QuantityText;
    
    [Header("Visual Settings")]
    public Color EmptySlotColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
    public Color OccupiedSlotColor = new Color(0.4f, 0.4f, 0.4f, 0.9f);
    public Color HighlightColor = new Color(0.6f, 0.6f, 0.8f, 1f);
    
    // Current item data
    private InventoryItem _currentItem;
    private bool _isOccupied = false;
    
    // Events
    public System.Action<int, InventoryItem> OnSlotClicked;
    public System.Action<int, InventoryItem> OnSlotRightClicked;
    
    private void Awake()
    {
        // Ensure we have required components
        if (SlotBackground == null)
            SlotBackground = GetComponent<Image>();
        
        if (ItemIcon == null)
            ItemIcon = transform.Find("ItemIcon")?.GetComponent<Image>();
        
        if (QuantityText == null)
            QuantityText = transform.Find("QuantityText")?.GetComponent<Text>();
            
        // Start with empty slot appearance
        SetSlotAppearance(false);
    }
    
    /// <summary>
    /// Set item in this slot
    /// </summary>
    public void SetItem(InventoryItem item, Sprite itemIcon = null)
    {
        _currentItem = item;
        _isOccupied = item != null;
        
        if (_isOccupied)
        {
            // Show item icon
            if (ItemIcon != null)
            {
                ItemIcon.gameObject.SetActive(true);
                ItemIcon.sprite = itemIcon;
                
                // If no icon provided, show placeholder
                if (itemIcon == null)
                {
                    ItemIcon.color = GetRarityColor(item.Rarity);
                }
                else
                {
                    ItemIcon.color = Color.white;
                }
            }
            
            // Show quantity if stackable and more than 1
            if (QuantityText != null)
            {
                if (item.IsStackable && item.Quantity > 1)
                {
                    QuantityText.gameObject.SetActive(true);
                    QuantityText.text = item.Quantity.ToString();
                }
                else
                {
                    QuantityText.gameObject.SetActive(false);
                }
            }
            
            SetSlotAppearance(true);
            Debug.Log($"[InventorySlot] Set item in slot {SlotIndex}: {item.ItemName} (x{item.Quantity})");
        }
        else
        {
            ClearSlot();
        }
    }
    
    /// <summary>
    /// Clear this slot
    /// </summary>
    public void ClearSlot()
    {
        _currentItem = null;
        _isOccupied = false;
        
        // Hide item visuals
        if (ItemIcon != null)
            ItemIcon.gameObject.SetActive(false);
            
        if (QuantityText != null)
            QuantityText.gameObject.SetActive(false);
        
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
        switch (rarity?.ToLower())
        {
            case "common":
                return Color.white;
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
            // Right click
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
    /// Get current item in this slot
    /// </summary>
    public InventoryItem GetItem()
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
    /// Check if this slot can accept a specific item
    /// </summary>
    public bool CanAcceptItem(InventoryItem item)
    {
        if (!_isOccupied)
            return true;
            
        // Check if items can stack
        if (_currentItem.ItemType == item.ItemType && 
            _currentItem.IsStackable && 
            item.IsStackable &&
            _currentItem.Quantity + item.Quantity <= _currentItem.MaxStackSize)
        {
            return true;
        }
        
        return false;
    }
}