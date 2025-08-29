using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

/// <summary>
/// Manages the vendor UI panel showing player's inventory with sell prices
/// Allows players to sell items by clicking on them
/// </summary>
public class VendorUI : MonoBehaviour
{
    [Header("Vendor Settings")]
    public string VendorName = "General Merchant";
    public int MaxSlots = 20;
    public Vector2 SlotSize = new Vector2(64, 64);
    public Vector2 SlotSpacing = new Vector2(8, 8);
    public int SlotsPerRow = 5;
    
    [Header("UI References")]
    public Transform VendorContainer;
    public GameObject SlotPrefab; // Will be created if not assigned
    public Text VendorTitleText;
    public Text PlayerGoldText;
    
    // Internal components
    private List<VendorSlot> _vendorSlots = new List<VendorSlot>();
    private List<InventoryItem> _playerInventory = new List<InventoryItem>();
    private ItemIconManager _iconManager;
    private int _playerGold = 0;
    
    // Events
    public System.Action<InventoryItem, int> OnItemSellClicked; // Item, slot index
    
    private void Awake()
    {
        // Subscribe to inventory updates to refresh vendor display
        NetworkManager.OnInventoryResponse += HandleInventoryResponse;
        NetworkManager.OnInventoryUpdate += HandleInventoryUpdateMessage;
        
        Debug.Log("[VendorUI] Subscribed to inventory network events");
    }
    
    private void Start()
    {
        // Only initialize if the panel is active (when opened)
        if (gameObject.activeInHierarchy)
        {
            InitializeVendorUI();
        }
    }
    
    private void Update()
    {
        // Handle V key press to close vendor panel when it's open
        if (gameObject.activeInHierarchy && Input.GetKeyDown(KeyCode.V))
        {
            HideVendorPanel();
        }
    }
    
    private void InitializeVendorUI()
    {
        // Find or get ItemIconManager
        _iconManager = FindObjectOfType<ItemIconManager>();
        if (_iconManager == null)
        {
            Debug.LogWarning("[VendorUI] ItemIconManager not found - icons may not display properly");
        }
        
        // Initialize vendor title
        if (VendorTitleText != null)
        {
            VendorTitleText.text = $"{VendorName} - Sell Items";
        }
        
        // Create vendor slots
        CreateVendorSlots();
        
        Debug.Log($"[VendorUI] Initialized with {MaxSlots} slots for {VendorName}");
    }
    
    private void CreateVendorSlots()
    {
        if (VendorContainer == null)
        {
            Debug.LogError("[VendorUI] VendorContainer is null - cannot create slots");
            return;
        }
        
        // Clear existing slots
        _vendorSlots.Clear();
        foreach (Transform child in VendorContainer)
        {
            if (Application.isPlaying)
                Destroy(child.gameObject);
        }
        
        // Create new slots
        for (int i = 0; i < MaxSlots; i++)
        {
            GameObject slotObj = CreateVendorSlot(i);
            VendorSlot vendorSlot = slotObj.GetComponent<VendorSlot>();
            if (vendorSlot == null)
            {
                vendorSlot = slotObj.AddComponent<VendorSlot>();
            }
            
            vendorSlot.SlotIndex = i;
            vendorSlot.OnSlotClicked += HandleSlotClicked;
            _vendorSlots.Add(vendorSlot);
        }
        
        Debug.Log($"[VendorUI] Created {_vendorSlots.Count} vendor slots");
    }
    
    private GameObject CreateVendorSlot(int slotIndex)
    {
        // Create slot GameObject
        GameObject slotObj = new GameObject($"VendorSlot_{slotIndex}");
        slotObj.transform.SetParent(VendorContainer, false);
        
        // Add Image component for slot background
        Image slotImage = slotObj.AddComponent<Image>();
        slotImage.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        
        // Add Button component for clicking
        Button slotButton = slotObj.AddComponent<Button>();
        
        // Set up RectTransform for grid layout
        RectTransform rectTransform = slotObj.GetComponent<RectTransform>();
        rectTransform.sizeDelta = SlotSize;
        
        // Position in grid
        int row = slotIndex / SlotsPerRow;
        int col = slotIndex % SlotsPerRow;
        float x = col * (SlotSize.x + SlotSpacing.x);
        float y = -row * (SlotSize.y + SlotSpacing.y);
        rectTransform.anchoredPosition = new Vector2(x, y);
        
        // Create item icon child
        GameObject iconObj = new GameObject("ItemIcon");
        iconObj.transform.SetParent(slotObj.transform, false);
        Image iconImage = iconObj.AddComponent<Image>();
        iconImage.color = Color.clear; // Start transparent
        
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.sizeDelta = Vector2.zero;
        iconRect.anchoredPosition = Vector2.zero;
        
        // Create price text child
        GameObject priceObj = new GameObject("PriceText");
        priceObj.transform.SetParent(slotObj.transform, false);
        Text priceText = priceObj.AddComponent<Text>();
        priceText.text = "";
        priceText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        priceText.fontSize = 10;
        priceText.color = Color.yellow;
        priceText.alignment = TextAnchor.LowerRight;
        
        RectTransform priceRect = priceObj.GetComponent<RectTransform>();
        priceRect.anchorMin = Vector2.zero;
        priceRect.anchorMax = Vector2.one;
        priceRect.sizeDelta = Vector2.zero;
        priceRect.anchoredPosition = Vector2.zero;
        
        return slotObj;
    }
    
    private void HandleSlotClicked(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < _playerInventory.Count)
        {
            InventoryItem item = _playerInventory[slotIndex];
            if (item != null && !string.IsNullOrEmpty(item.ItemType))
            {
                Debug.Log($"[VendorUI] Player wants to sell {item.ItemName} from slot {slotIndex}");
                OnItemSellClicked?.Invoke(item, slotIndex);
            }
        }
    }
    
    private void HandleInventoryResponse(NetworkMessages.InventoryResponseMessage inventoryResponse)
    {
        if (inventoryResponse == null || !inventoryResponse.Success) return;
        
        Debug.Log($"[VendorUI] Received inventory response with {inventoryResponse.Items?.Count ?? 0} items");
        
        // Update internal inventory data
        _playerInventory.Clear();
        if (inventoryResponse.Items != null)
        {
            _playerInventory.AddRange(inventoryResponse.Items);
        }
        
        // Update gold display (placeholder for now - no gold field in response yet)
        _playerGold = 0; // TODO: Add gold field to InventoryResponseMessage in Phase 4
        UpdateGoldDisplay();
        
        // Refresh vendor display
        RefreshVendorDisplay();
    }
    
    private void HandleInventoryUpdateMessage(NetworkMessages.InventoryUpdateMessage inventoryUpdate)
    {
        if (inventoryUpdate == null) return;
        
        Debug.Log($"[VendorUI] Received inventory update: {inventoryUpdate.UpdateType}");
        
        // For now, just refresh the entire display
        // TODO: Optimize to only update changed items
        RefreshVendorDisplay();
    }
    
    private void RefreshVendorDisplay()
    {
        // Update each slot with inventory data
        for (int i = 0; i < _vendorSlots.Count; i++)
        {
            VendorSlot slot = _vendorSlots[i];
            
            if (i < _playerInventory.Count && _playerInventory[i] != null && !string.IsNullOrEmpty(_playerInventory[i].ItemType))
            {
                InventoryItem item = _playerInventory[i];
                slot.UpdateSlot(item, CalculateSellPrice(item));
            }
            else
            {
                slot.ClearSlot();
            }
        }
        
        Debug.Log($"[VendorUI] Refreshed vendor display with {_playerInventory.Count} items");
    }
    
    private int CalculateSellPrice(InventoryItem item)
    {
        if (item == null) return 0;
        
        // Use item value if available, otherwise calculate based on type
        if (item.Value > 0)
        {
            return item.Value / 2; // Sell for half the value
        }
        
        // Fallback pricing based on item type
        switch (item.ItemType?.ToLower())
        {
            case "sword": return 15;
            case "shield": return 10;
            case "helmet": return 8;
            case "chestplate": return 12;
            case "boots": return 6;
            case "ring": return 20;
            case "potion": return 5;
            default: return 1;
        }
    }
    
    private void UpdateGoldDisplay()
    {
        if (PlayerGoldText != null)
        {
            PlayerGoldText.text = $"Gold: {_playerGold}";
        }
    }
    
    public void ShowVendorPanel()
    {
        gameObject.SetActive(true);
        
        // Initialize UI if not already done
        if (_vendorSlots.Count == 0)
        {
            InitializeVendorUI();
        }
        
        // Request fresh inventory data when opening
        var networkManager = FindObjectOfType<NetworkManager>();
        if (networkManager != null)
        {
            _ = networkManager.RequestInventory(); // Use the correct method name
        }
        
        Debug.Log($"[VendorUI] Vendor panel opened for {VendorName}");
    }
    
    public void HideVendorPanel()
    {
        gameObject.SetActive(false);
        Debug.Log($"[VendorUI] Vendor panel closed");
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        NetworkManager.OnInventoryResponse -= HandleInventoryResponse;
        NetworkManager.OnInventoryUpdate -= HandleInventoryUpdateMessage;
        
        Debug.Log("[VendorUI] Unsubscribed from network events");
    }
}