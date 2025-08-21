using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

/// <summary>
/// Manages the inventory UI panel with 20 slots displaying item icons
/// Handles inventory data from server and visual representation
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("Inventory Settings")]
    public int MaxSlots = 20;
    public Vector2 SlotSize = new Vector2(64, 64);
    public Vector2 SlotSpacing = new Vector2(8, 8);
    public int SlotsPerRow = 5;
    
    [Header("UI References")]
    public Transform InventoryContainer;
    public GameObject SlotPrefab; // Will be created if not assigned
    public GameObject ItemDetailsPanel; // Will be created if not assigned
    
    [Header("Item Icon Settings")]
    public bool ShowPlaceholderIcons = true;
    public Color PlaceholderColor = Color.gray;
    
    // Internal components
    private List<InventorySlot> _inventorySlots = new List<InventorySlot>();
    private List<InventoryItem> _inventoryData = new List<InventoryItem>();
    private ItemIconManager _iconManager;
    private Text _itemDetailsText;
    private Text _goldDisplayText;
    private GameObject _goldDisplayPanel;
    
    // Events
    public System.Action<InventoryItem> OnItemClicked;
    public System.Action<InventoryItem> OnItemRightClicked;
    
    private void Awake()
    {
        // Subscribe to inventory network events
        NetworkManager.OnInventoryResponse += HandleInventoryResponse;
        NetworkManager.OnInventoryUpdate += HandleInventoryUpdate;
        
        Debug.Log("[InventoryUI] Subscribed to inventory network events");
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        NetworkManager.OnInventoryResponse -= HandleInventoryResponse;
        NetworkManager.OnInventoryUpdate -= HandleInventoryUpdate;
    }
    
    private void Start()
    {
        SetupInventoryUI();
        SetupItemIconManager();
        SetupGoldDisplayPanel();
        SetupItemDetailsPanel();
        
        // Request inventory data from server
        RequestInventoryFromServer();
    }
    
    private void SetupInventoryUI()
    {
        if (InventoryContainer == null)
        {
            Debug.LogError("[InventoryUI] InventoryContainer not assigned! Looking for existing container...");
            
            // Try to find existing inventory panel
            var inventoryPanel = GameObject.Find("InventoryPanel");
            if (inventoryPanel != null)
            {
                InventoryContainer = inventoryPanel.transform;
                Debug.Log("[InventoryUI] Found existing InventoryPanel");
            }
            else
            {
                Debug.LogError("[InventoryUI] No InventoryContainer found!");
                return;
            }
        }
        
        CreateInventorySlots();
        Debug.Log($"[InventoryUI] Created {_inventorySlots.Count} inventory slots");
    }
    
    private void SetupItemIconManager()
    {
        _iconManager = gameObject.AddComponent<ItemIconManager>();
        _iconManager.Initialize();
        Debug.Log("[InventoryUI] ItemIconManager initialized");
    }
    
    private void SetupGoldDisplayPanel()
    {
        // Create gold display panel if it doesn't exist
        if (_goldDisplayPanel == null)
        {
            _goldDisplayPanel = new GameObject("GoldDisplayPanel");
            _goldDisplayPanel.transform.SetParent(InventoryContainer, false);
            
            // Add Image component for background
            var bgImage = _goldDisplayPanel.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.15f, 0.05f, 0.9f); // Golden-brown background
            
            // Position much closer to the grid (moved up ~15%)
            var goldRect = _goldDisplayPanel.GetComponent<RectTransform>();
            goldRect.anchorMin = new Vector2(0.05f, 0.37f);
            goldRect.anchorMax = new Vector2(0.95f, 0.41f);
            goldRect.anchoredPosition = Vector2.zero;
            goldRect.sizeDelta = Vector2.zero;
        }
        
        // Create text component for gold amount
        if (_goldDisplayText == null)
        {
            GameObject textObj = new GameObject("GoldText");
            textObj.transform.SetParent(_goldDisplayPanel.transform, false);
            
            _goldDisplayText = textObj.AddComponent<Text>();
            _goldDisplayText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _goldDisplayText.fontSize = 18;
            _goldDisplayText.color = new Color(1f, 0.84f, 0f, 1f); // Golden color
            _goldDisplayText.alignment = TextAnchor.MiddleCenter;
            _goldDisplayText.fontStyle = FontStyle.Bold;
            _goldDisplayText.text = "Gold: 0";
            
            // Position text to fill the panel
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = Vector2.zero;
        }
        
        // Update gold display with current amount (placeholder for now)
        UpdateGoldDisplay(0);
        
        Debug.Log("[InventoryUI] Gold display panel created");
    }
    
    private void SetupItemDetailsPanel()
    {
        // Create details panel if it doesn't exist
        if (ItemDetailsPanel == null)
        {
            ItemDetailsPanel = new GameObject("ItemDetailsPanel");
            ItemDetailsPanel.transform.SetParent(InventoryContainer, false);
            
            // Add Image component for background
            var bgImage = ItemDetailsPanel.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f); // Dark background
            
            // Position with 2% buffer below the gold display panel (increased height for text)
            var detailsRect = ItemDetailsPanel.GetComponent<RectTransform>();
            detailsRect.anchorMin = new Vector2(0.05f, 0.05f);
            detailsRect.anchorMax = new Vector2(0.95f, 0.35f);
            detailsRect.anchoredPosition = Vector2.zero;
            detailsRect.sizeDelta = Vector2.zero;
        }
        
        // Create text component for item details
        if (_itemDetailsText == null)
        {
            GameObject textObj = new GameObject("DetailsText");
            textObj.transform.SetParent(ItemDetailsPanel.transform, false);
            
            _itemDetailsText = textObj.AddComponent<Text>();
            _itemDetailsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _itemDetailsText.fontSize = 16;
            _itemDetailsText.color = Color.white;
            _itemDetailsText.alignment = TextAnchor.UpperLeft;
            _itemDetailsText.verticalOverflow = VerticalWrapMode.Overflow;
            _itemDetailsText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _itemDetailsText.text = "";
            
            // Position text to fill the panel with padding
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.02f, 0.02f);
            textRect.anchorMax = new Vector2(0.98f, 0.98f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = Vector2.zero;
        }
        
        // Initially hide the details panel
        ItemDetailsPanel.SetActive(false);
        
        Debug.Log("[InventoryUI] Item details panel created and positioned");
    }
    
    private void CreateInventorySlots()
    {
        // Clear existing slots
        foreach (var slot in _inventorySlots)
        {
            if (slot != null && slot.gameObject != null)
                DestroyImmediate(slot.gameObject);
        }
        _inventorySlots.Clear();
        
        // Create grid container if it doesn't exist
        GameObject gridContainer = InventoryContainer.Find("SlotGrid")?.gameObject;
        if (gridContainer == null)
        {
            gridContainer = new GameObject("SlotGrid");
            gridContainer.transform.SetParent(InventoryContainer, false);
            
            // Add GridLayoutGroup for automatic positioning
            var gridLayout = gridContainer.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = SlotSize;
            gridLayout.spacing = SlotSpacing;
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = SlotsPerRow;
            gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.childAlignment = TextAnchor.UpperLeft;
            
            // Position the grid within the inventory container (leave space for gold display and details panel)
            var gridRect = gridContainer.GetComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0.05f, 0.26f);
            gridRect.anchorMax = new Vector2(0.95f, 0.95f);
            gridRect.anchoredPosition = Vector2.zero;
            gridRect.sizeDelta = Vector2.zero;
        }
        
        // Create inventory slots
        for (int i = 0; i < MaxSlots; i++)
        {
            GameObject slotObj = CreateInventorySlot(i);
            slotObj.transform.SetParent(gridContainer.transform, false);
            
            var slot = slotObj.GetComponent<InventorySlot>();
            if (slot != null)
            {
                _inventorySlots.Add(slot);
                
                // Setup click and hover events
                slot.OnSlotClicked += HandleSlotClicked;
                slot.OnSlotRightClicked += HandleSlotRightClicked;
                slot.OnSlotHoverEnter += HandleSlotHoverEnter;
                slot.OnSlotHoverExit += HandleSlotHoverExit;
            }
        }
        
        Debug.Log($"[InventoryUI] Created {MaxSlots} inventory slots in grid layout");
    }
    
    private GameObject CreateInventorySlot(int slotIndex)
    {
        GameObject slotObj = new GameObject($"InventorySlot_{slotIndex}");
        
        // Add Image component for background
        var bgImage = slotObj.AddComponent<Image>();
        bgImage.color = new Color(0.3f, 0.3f, 0.3f, 0.8f); // Dark slot background
        
        // Add InventorySlot component
        var slot = slotObj.AddComponent<InventorySlot>();
        slot.SlotIndex = slotIndex;
        slot.SlotBackground = bgImage;
        
        // Create item icon child object
        GameObject iconObj = new GameObject("ItemIcon");
        iconObj.transform.SetParent(slotObj.transform, false);
        
        var iconImage = iconObj.AddComponent<Image>();
        iconImage.color = Color.white;
        iconImage.preserveAspect = true;
        
        // Position icon to fill most of the slot
        var iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.1f, 0.1f);
        iconRect.anchorMax = new Vector2(0.9f, 0.9f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = Vector2.zero;
        
        slot.ItemIcon = iconImage;
        
        // Create quantity text
        GameObject quantityObj = new GameObject("QuantityText");
        quantityObj.transform.SetParent(slotObj.transform, false);
        
        var quantityText = quantityObj.AddComponent<Text>();
        quantityText.text = "";
        quantityText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        quantityText.fontSize = 12;
        quantityText.color = Color.white;
        quantityText.alignment = TextAnchor.LowerRight;
        quantityText.fontStyle = FontStyle.Bold;
        
        // Position quantity text in bottom-right corner
        var quantityRect = quantityObj.GetComponent<RectTransform>();
        quantityRect.anchorMin = new Vector2(0.5f, 0f);
        quantityRect.anchorMax = new Vector2(1f, 0.5f);
        quantityRect.anchoredPosition = Vector2.zero;
        quantityRect.sizeDelta = Vector2.zero;
        
        slot.QuantityText = quantityText;
        
        // Initially hide the slot contents
        iconObj.SetActive(false);
        quantityObj.SetActive(false);
        
        return slotObj;
    }
    
    private async void RequestInventoryFromServer()
    {
        Debug.Log("[InventoryUI] Requesting inventory from server...");
        
        var networkManager = FindObjectOfType<NetworkManager>();
        if (networkManager != null)
        {
            await networkManager.RequestInventory();
        }
        else
        {
            Debug.LogError("[InventoryUI] NetworkManager not found!");
        }
    }
    
    private void HandleInventoryResponse(NetworkMessages.InventoryResponseMessage response)
    {
        Debug.Log($"[InventoryUI] Received inventory response with {response.Items.Count} items");
        
        if (response.Success)
        {
            _inventoryData = response.Items;
            UpdateInventoryDisplay();
        }
        else
        {
            Debug.LogError($"[InventoryUI] Inventory request failed: {response.ErrorMessage}");
        }
    }
    
    private void HandleInventoryUpdate(NetworkMessages.InventoryUpdateMessage update)
    {
        Debug.Log($"[InventoryUI] Received inventory update: {update.UpdateType} with {update.UpdatedItems.Count} items");
        
        switch (update.UpdateType)
        {
            case "Add":
                foreach (var item in update.UpdatedItems)
                {
                    AddOrUpdateItem(item);
                }
                break;
                
            case "Remove":
                foreach (var item in update.UpdatedItems)
                {
                    RemoveItem(item.ItemId);
                }
                break;
                
            case "Update":
                foreach (var item in update.UpdatedItems)
                {
                    AddOrUpdateItem(item);
                }
                break;
                
            case "Clear":
                _inventoryData.Clear();
                break;
        }
        
        UpdateInventoryDisplay();
    }
    
    private void AddOrUpdateItem(InventoryItem newItem)
    {
        var existingItem = _inventoryData.FirstOrDefault(i => i.ItemId == newItem.ItemId);
        if (existingItem != null)
        {
            // Update existing item
            _inventoryData.Remove(existingItem);
        }
        
        _inventoryData.Add(newItem);
    }
    
    private void RemoveItem(string itemId)
    {
        _inventoryData.RemoveAll(i => i.ItemId == itemId);
    }
    
    private void UpdateInventoryDisplay()
    {
        // Clear all slots first
        foreach (var slot in _inventorySlots)
        {
            slot.ClearSlot();
        }
        
        // Place items in their assigned slots
        foreach (var item in _inventoryData)
        {
            if (item.SlotIndex >= 0 && item.SlotIndex < _inventorySlots.Count)
            {
                var slot = _inventorySlots[item.SlotIndex];
                slot.SetItem(item, _iconManager.GetItemIcon(item.IconName));
            }
        }
        
        Debug.Log($"[InventoryUI] Updated inventory display with {_inventoryData.Count} items");
    }
    
    private void HandleSlotClicked(int slotIndex, InventoryItem item)
    {
        Debug.Log($"[InventoryUI] Slot {slotIndex} left-clicked - Item: {item?.ItemName ?? "Empty"}");
        
        if (item != null)
        {
            // Left click = Use item (for consumables)
            UseItem(item, slotIndex);
            OnItemClicked?.Invoke(item);
        }
    }
    
    private void HandleSlotRightClicked(int slotIndex, InventoryItem item)
    {
        Debug.Log($"[InventoryUI] Slot {slotIndex} right-clicked - Item: {item?.ItemName ?? "Empty"}");
        
        if (item != null)
        {
            // Right click = Sell item
            SellItem(item, slotIndex);
            OnItemRightClicked?.Invoke(item);
        }
    }
    
    /// <summary>
    /// Use an item (consumables only)
    /// </summary>
    private async void UseItem(InventoryItem item, int slotIndex)
    {
        if (item == null) return;
        
        // Check if item is consumable
        bool isConsumable = IsConsumableItem(item);
        
        if (!isConsumable)
        {
            Debug.Log($"[InventoryUI] Item {item.ItemName} is not consumable - cannot use");
            ShowMessage($"{item.ItemName} is not usable");
            return;
        }
        
        Debug.Log($"[InventoryUI] Using consumable item: {item.ItemName} from slot {slotIndex}");
        
        // Send use request to server
        var networkManager = FindObjectOfType<NetworkManager>();
        if (networkManager != null)
        {
            var useRequest = new NetworkMessages.ItemUseRequestMessage
            {
                PlayerId = networkManager.GetPlayerId(),
                SlotIndex = slotIndex,
                ItemType = item.ItemType
            };
            
            await networkManager.SendItemUseRequest(useRequest);
        }
        else
        {
            Debug.LogError("[InventoryUI] NetworkManager not found for item use!");
        }
    }
    
    /// <summary>
    /// Sell an item (framework for future implementation)
    /// </summary>
    private async void SellItem(InventoryItem item, int slotIndex)
    {
        if (item == null) return;
        
        Debug.Log($"[InventoryUI] Attempting to sell item: {item.ItemName} from slot {slotIndex}");
        
        // TODO: Implement sell functionality when shop system is ready
        ShowMessage($"Selling {item.ItemName} - Shop system coming soon!");
        
        // Framework for future sell request
        /*
        var networkManager = FindObjectOfType<NetworkManager>();
        if (networkManager != null)
        {
            var sellRequest = new NetworkMessages.ItemSellRequestMessage
            {
                PlayerId = networkManager.GetPlayerId(),
                SlotIndex = slotIndex,
                ItemType = item.ItemType,
                Quantity = 1 // Default to selling one item
            };
            
            await networkManager.SendItemSellRequest(sellRequest);
        }
        */
    }
    
    /// <summary>
    /// Check if an item is consumable
    /// </summary>
    private bool IsConsumableItem(InventoryItem item)
    {
        if (item == null) return false;
        
        // Check by ItemCategory
        if (!string.IsNullOrEmpty(item.ItemCategory))
        {
            string category = item.ItemCategory.ToLower();
            return category == "consumable" || category == "medical" || category == "food";
        }
        
        // Fallback: Check by ItemType for common consumables
        string itemType = item.ItemType.ToLower();
        return itemType.Contains("potion") || 
               itemType.Contains("elixir") || 
               itemType.Contains("food") ||
               itemType.Contains("medicine") ||
               itemType.Contains("bandage");
    }
    
    /// <summary>
    /// Show a message to the player
    /// </summary>
    private void ShowMessage(string message)
    {
        // Try to use the GameManager's UI system
        if (GameManager.Instance?.UIManager != null)
        {
            GameManager.Instance.UIManager.ShowMessage(message);
        }
        else
        {
            Debug.Log($"[InventoryUI] Message: {message}");
        }
    }
    
    /// <summary>
    /// Public method to refresh inventory from server
    /// </summary>
    public void RefreshInventory()
    {
        RequestInventoryFromServer();
    }
    
    /// <summary>
    /// Get item at specific slot index
    /// </summary>
    public InventoryItem GetItemAtSlot(int slotIndex)
    {
        return _inventoryData.FirstOrDefault(i => i.SlotIndex == slotIndex);
    }
    
    /// <summary>
    /// Check if inventory is full
    /// </summary>
    public bool IsInventoryFull()
    {
        return _inventoryData.Count >= MaxSlots;
    }
    
    /// <summary>
    /// Handle slot hover enter - show item details
    /// </summary>
    private void HandleSlotHoverEnter(int slotIndex, InventoryItem item)
    {
        if (item != null && ItemDetailsPanel != null && _itemDetailsText != null)
        {
            string detailsText = GenerateItemDetailsText(item);
            _itemDetailsText.text = detailsText;
            ItemDetailsPanel.SetActive(true);
            
            Debug.Log($"[InventoryUI] Showing details for item: {item.ItemName}");
        }
    }
    
    /// <summary>
    /// Handle slot hover exit - hide item details
    /// </summary>
    private void HandleSlotHoverExit(int slotIndex, InventoryItem item)
    {
        if (ItemDetailsPanel != null)
        {
            ItemDetailsPanel.SetActive(false);
            Debug.Log("[InventoryUI] Hidden item details panel");
        }
    }
    
    /// <summary>
    /// Generate comprehensive text description for an item
    /// </summary>
    private string GenerateItemDetailsText(InventoryItem item)
    {
        var details = new System.Text.StringBuilder();
        
        // Item name and rarity (expand single-character rarity codes)
        details.AppendLine($"<b>{item.ItemName}</b>");
        details.AppendLine($"Rarity: {InventorySlot.ExpandRarityCode(item.Rarity)}");
        
        // Description
        if (!string.IsNullOrEmpty(item.ItemDescription))
        {
            details.AppendLine($"Description: {item.ItemDescription}");
        }
        
        // Quantity and stack info
        if (item.IsStackable)
        {
            details.AppendLine($"Quantity: {item.Quantity}/{item.MaxStackSize}");
        }
        else
        {
            details.AppendLine("Quantity: 1 (Not Stackable)");
        }
        
        // Combat stats
        if (item.AttackPower > 0)
        {
            details.AppendLine($"Attack Power: +{item.AttackPower}");
        }
        
        if (item.DefensePower > 0)
        {
            details.AppendLine($"Defense Power: +{item.DefensePower}");
        }
        
        // Item properties
        details.AppendLine($"Value: {item.Value} gold");
        details.AppendLine($"Level: {item.Level}");
        
        return details.ToString();
    }
    
    /// <summary>
    /// Update the gold display with current amount
    /// </summary>
    public void UpdateGoldDisplay(int goldAmount)
    {
        if (_goldDisplayText != null)
        {
            _goldDisplayText.text = $"Gold: {goldAmount:N0}";
        }
    }
    
    /// <summary>
    /// Get current gold amount (placeholder - will be integrated with player currency system)
    /// </summary>
    public int GetPlayerGold()
    {
        // TODO: Integrate with actual player currency system
        // For now, return 0 as placeholder
        return 0;
    }
}