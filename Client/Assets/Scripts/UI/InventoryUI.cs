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
    
    [Header("Item Icon Settings")]
    public bool ShowPlaceholderIcons = true;
    public Color PlaceholderColor = Color.gray;
    
    // Internal components
    private List<InventorySlot> _inventorySlots = new List<InventorySlot>();
    private List<InventoryItem> _inventoryData = new List<InventoryItem>();
    private ItemIconManager _iconManager;
    
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
            
            // Position the grid within the inventory container (leave space for title)
            var gridRect = gridContainer.GetComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0.05f, 0.05f);
            gridRect.anchorMax = new Vector2(0.95f, 0.8f);
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
                
                // Setup click events
                slot.OnSlotClicked += HandleSlotClicked;
                slot.OnSlotRightClicked += HandleSlotRightClicked;
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
        Debug.Log($"[InventoryUI] Slot {slotIndex} clicked - Item: {item?.ItemName ?? "Empty"}");
        
        if (item != null)
        {
            OnItemClicked?.Invoke(item);
        }
    }
    
    private void HandleSlotRightClicked(int slotIndex, InventoryItem item)
    {
        Debug.Log($"[InventoryUI] Slot {slotIndex} right-clicked - Item: {item?.ItemName ?? "Empty"}");
        
        if (item != null)
        {
            OnItemRightClicked?.Invoke(item);
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
}