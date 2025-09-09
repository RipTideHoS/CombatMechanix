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
    public GameObject ItemDetailsPanel; // Will be created if not assigned
    
    // Internal components
    private List<VendorSlot> _vendorSlots = new List<VendorSlot>();
    private List<InventoryItem> _playerInventory = new List<InventoryItem>();
    private ItemIconManager _iconManager;
    private int _playerGold = 100; // Default starting gold
    private Text _itemDetailsText;
    private bool _isInitialized = false;
    
    // Events
    public System.Action<InventoryItem, int> OnItemSellClicked; // Item, slot index
    
    private void Awake()
    {
        Debug.Log($"[VendorUI] Awake() called on GameObject: {gameObject.name}");
        
        // Subscribe to inventory updates to refresh vendor display
        NetworkManager.OnInventoryResponse += HandleInventoryResponse;
        NetworkManager.OnInventoryUpdate += HandleInventoryUpdateMessage;
        NetworkManager.OnItemSellResponse += HandleItemSellResponse;
        
        // Subscribe to player stats for gold updates (consistent with InventoryUI)
        ClientPlayerStats.OnStatsUpdated += HandlePlayerStatsUpdate;
        ClientPlayerStats.OnGoldChanged += HandleGoldChanged;
        
        Debug.Log("[VendorUI] Subscribed to inventory and player stats network events");
    }
    
    private void Start()
    {
        Debug.Log($"[VendorUI] Start() called - GameObject active: {gameObject.activeInHierarchy}, Name: {gameObject.name}");
        // Only initialize if the panel is active (when opened)
        if (gameObject.activeInHierarchy)
        {
            Debug.Log("[VendorUI] GameObject is active, calling InitializeVendorUI()");
            InitializeVendorUI();
        }
        else
        {
            Debug.Log("[VendorUI] GameObject is inactive, skipping InitializeVendorUI()");
        }
    }
    
    private void OnEnable()
    {
        Debug.Log($"[VendorUI] OnEnable() called - VendorSlots count: {_vendorSlots.Count}, VendorContainer: {(VendorContainer != null ? VendorContainer.name : "NULL")}");
        // Initialize when panel becomes active (if not already initialized)
        if (_vendorSlots.Count == 0)
        {
            Debug.Log("[VendorUI] No vendor slots exist, calling InitializeVendorUI()");
            InitializeVendorUI();
        }
        else
        {
            Debug.Log("[VendorUI] Vendor slots already exist, skipping InitializeVendorUI()");
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
        if (_isInitialized)
        {
            Debug.Log("[VendorUI] Already initialized, skipping InitializeVendorUI()");
            return;
        }
        
        Debug.Log("[VendorUI] InitializeVendorUI() starting...");
        
        // Ensure ItemIconManager is available (matching InventoryUI approach)
        _iconManager = FindObjectOfType<ItemIconManager>();
        if (_iconManager == null)
        {
            _iconManager = gameObject.AddComponent<ItemIconManager>();
            _iconManager.Initialize();
            Debug.Log("[VendorUI] Created and initialized ItemIconManager");
        }
        
        // Initialize vendor title
        if (VendorTitleText != null)
        {
            VendorTitleText.text = $"{VendorName} - Sell Items";
        }
        
        // Create vendor slots
        CreateVendorSlots();
        
        // Setup item details panel (similar to InventoryUI pattern)
        if (ItemDetailsPanel == null)
        {
            SetupItemDetailsPanel();
        }
        else
        {
            Debug.Log("[VendorUI] ItemDetailsPanel already assigned by AutoSceneSetup, setting up text reference");
            SetupItemDetailsText();
        }
        
        _isInitialized = true;
        Debug.Log($"[VendorUI] Initialized with {MaxSlots} slots for {VendorName}");
    }
    
    private void CreateVendorSlots()
    {
        Debug.Log($"[VendorUI] CreateVendorSlots() called - MaxSlots: {MaxSlots}, Current _vendorSlots.Count: {_vendorSlots.Count}");
        
        if (VendorContainer == null)
        {
            Debug.LogError("[VendorUI] VendorContainer is null - cannot create slots! Please assign VendorContainer in the Inspector.");
            
            // Try to find an existing container automatically
            var vendorPanel = GameObject.Find("VendorPanel");
            if (vendorPanel != null)
            {
                VendorContainer = vendorPanel.transform;
                Debug.Log("[VendorUI] Auto-found VendorPanel as container");
            }
            else
            {
                Debug.LogError("[VendorUI] Could not auto-find VendorPanel either!");
                return;
            }
        }
        
        // Clear existing slots from VendorSlotsContainer if it exists
        _vendorSlots.Clear();
        Transform existingSlotsContainer = VendorContainer.Find("VendorSlotsContainer");
        if (existingSlotsContainer != null)
        {
            // Clear all slots from the existing slots container
            foreach (Transform child in existingSlotsContainer)
            {
                if (Application.isPlaying)
                    Destroy(child.gameObject);
            }
        }
        else
        {
            // Fallback: clear direct children of VendorContainer (but preserve ItemDetailsPanel)
            foreach (Transform child in VendorContainer)
            {
                if (Application.isPlaying && child.name != "ItemDetailsPanel")
                    Destroy(child.gameObject);
            }
        }
        
        // Create separate container for vendor slots (so GridLayoutGroup doesn't affect ItemDetailsPanel)
        Transform slotsTransform = VendorContainer.Find("VendorSlotsContainer");
        GameObject slotsContainer;
        if (slotsTransform == null)
        {
            slotsContainer = new GameObject("VendorSlotsContainer");
            slotsContainer.transform.SetParent(VendorContainer, false);
            
            // Position slots container to leave space for details panel at bottom
            var slotsRect = slotsContainer.AddComponent<RectTransform>();
            slotsRect.anchorMin = new Vector2(0.0f, 0.25f); // Top 75% of container
            slotsRect.anchorMax = new Vector2(1.0f, 1.0f);
            slotsRect.anchoredPosition = Vector2.zero;
            slotsRect.sizeDelta = Vector2.zero;
        }
        else
        {
            slotsContainer = slotsTransform.gameObject;
        }
        
        // Add GridLayoutGroup to the slots container (not VendorContainer)
        var gridLayout = slotsContainer.GetComponent<GridLayoutGroup>();
        if (gridLayout == null)
        {
            gridLayout = slotsContainer.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = SlotSize;
            gridLayout.spacing = SlotSpacing;
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = SlotsPerRow;
            gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.childAlignment = TextAnchor.UpperLeft;
            
            Debug.Log("[VendorUI] Added GridLayoutGroup to VendorSlotsContainer");
        }
        
        // Create new slots using grid layout
        Debug.Log($"[VendorUI] Creating {MaxSlots} vendor slots in {slotsContainer.name}...");
        Debug.Log($"[VendorUI] Current _vendorSlots count before creation: {_vendorSlots.Count}");
        for (int i = 0; i < MaxSlots; i++)
        {
            GameObject slotObj = CreateVendorSlot(i);
            if (slotObj == null)
            {
                Debug.LogError($"[VendorUI] Failed to create slot {i}");
                continue;
            }
            
            slotObj.transform.SetParent(slotsContainer.transform, false);
            Debug.Log($"[VendorUI] Created slot {i}, parented to slots container");
            
            VendorSlot vendorSlot = slotObj.GetComponent<VendorSlot>();
            if (vendorSlot == null)
            {
                vendorSlot = slotObj.AddComponent<VendorSlot>();
                Debug.Log($"[VendorUI] Added VendorSlot component to slot {i}");
            }
            
            // Set component references directly - get them immediately after creation
            vendorSlot.SlotBackground = slotObj.GetComponent<Image>();
            
            // Find child objects that were just created
            Transform iconTransform = slotObj.transform.Find("ItemIcon");
            Transform priceTransform = slotObj.transform.Find("PriceText");
            
            if (iconTransform != null)
                vendorSlot.ItemIcon = iconTransform.GetComponent<Image>();
            if (priceTransform != null)
                vendorSlot.PriceText = priceTransform.GetComponent<Text>();
            
            Debug.Log($"[VendorUI] Slot {i} child search - IconTransform: {iconTransform != null}, PriceTransform: {priceTransform != null}");
            Debug.Log($"[VendorUI] Slot {i} components - Background: {vendorSlot.SlotBackground != null}, Icon: {vendorSlot.ItemIcon != null}, Price: {vendorSlot.PriceText != null}");
            
            vendorSlot.SlotIndex = i;
            vendorSlot.SetIconManager(_iconManager);
            vendorSlot.OnSlotClicked += HandleSlotClicked;
            vendorSlot.OnSlotHoverEnter += HandleSlotHoverEnter;
            vendorSlot.OnSlotHoverExit += HandleSlotHoverExit;
            _vendorSlots.Add(vendorSlot);
        }
        
        Debug.Log($"[VendorUI] Created {_vendorSlots.Count} vendor slots in grid layout");
        Debug.Log($"[VendorUI] VendorContainer children count: {VendorContainer.childCount}");
    }
    
    private GameObject CreateVendorSlot(int slotIndex)
    {
        // Create slot GameObject
        GameObject slotObj = new GameObject($"VendorSlot_{slotIndex}");
        
        // Add Image component for slot background
        Image slotImage = slotObj.AddComponent<Image>();
        slotImage.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        slotImage.raycastTarget = true; // Enable pointer events for hover detection
        
        // GridLayoutGroup will handle sizing and positioning automatically
        
        // Create item icon child (matching InventoryUI structure)
        GameObject iconObj = new GameObject("ItemIcon");
        iconObj.transform.SetParent(slotObj.transform, false);
        Image iconImage = iconObj.AddComponent<Image>();
        iconImage.color = Color.white;
        iconImage.preserveAspect = true;
        
        // Position icon to fill most of the slot (matching InventorySlot)
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.1f, 0.1f);
        iconRect.anchorMax = new Vector2(0.9f, 0.9f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = Vector2.zero;
        
        // Create price text child (positioned in bottom-right)
        GameObject priceObj = new GameObject("PriceText");
        priceObj.transform.SetParent(slotObj.transform, false);
        Text priceText = priceObj.AddComponent<Text>();
        priceText.text = "";
        priceText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        priceText.fontSize = 12;
        priceText.color = Color.yellow;
        priceText.alignment = TextAnchor.LowerRight;
        priceText.fontStyle = FontStyle.Bold;
        
        // Position price text in bottom-right corner (matching InventorySlot quantity text)
        RectTransform priceRect = priceObj.GetComponent<RectTransform>();
        priceRect.anchorMin = new Vector2(0.5f, 0f);
        priceRect.anchorMax = new Vector2(1f, 0.5f);
        priceRect.anchoredPosition = Vector2.zero;
        priceRect.sizeDelta = Vector2.zero;
        
        // Initially hide the slot contents
        iconObj.SetActive(false);
        priceObj.SetActive(false);
        
        return slotObj;
    }
    
    private void HandleSlotClicked(int slotIndex)
    {
        // Find item by SlotIndex (matching InventoryUI approach)
        var item = _playerInventory.FirstOrDefault(i => i.SlotIndex == slotIndex);
        if (item != null && !string.IsNullOrEmpty(item.ItemType))
        {
            Debug.Log($"[VendorUI] Player wants to sell {item.ItemName} from slot {slotIndex}");
            OnItemSellClicked?.Invoke(item, slotIndex);
        }
        else
        {
            Debug.Log($"[VendorUI] No item found in slot {slotIndex}");
        }
    }
    
    private void HandleInventoryResponse(NetworkMessages.InventoryResponseMessage inventoryResponse)
    {
        Debug.Log($"[VendorUI] HandleInventoryResponse called - Response null: {inventoryResponse == null}");
        
        if (inventoryResponse == null)
        {
            Debug.LogError("[VendorUI] Received null inventory response!");
            return;
        }
        
        if (!inventoryResponse.Success)
        {
            Debug.LogError($"[VendorUI] Inventory response failed: {inventoryResponse.ErrorMessage}");
            return;
        }
        
        Debug.Log($"[VendorUI] Received inventory response with {inventoryResponse.Items?.Count ?? 0} items");
        
        // Update internal inventory data
        _playerInventory.Clear();
        Debug.Log($"[VendorUI] inventoryResponse.Items null: {inventoryResponse.Items == null}");
        
        if (inventoryResponse.Items != null)
        {
            Debug.Log($"[VendorUI] Processing {inventoryResponse.Items.Count} items from inventory response");
            foreach (var item in inventoryResponse.Items)
            {
                Debug.Log($"[VendorUI] Item: {item?.ItemName} ({item?.ItemType}) - Qty: {item?.Quantity} - Value: {item?.Value}");
            }
            _playerInventory.AddRange(inventoryResponse.Items);
        }
        else
        {
            Debug.LogError("[VendorUI] inventoryResponse.Items is null despite having a count!");
        }
        
        // Gold is not included in inventory response, keep current value
        // Gold gets updated through HandleItemSellResponse when items are sold
        UpdateGoldDisplay();
        
        // Refresh vendor display
        Debug.Log($"[VendorUI] About to refresh vendor display with {_playerInventory.Count} items");
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
    
    private void HandleItemSellResponse(NetworkMessages.ItemSellResponseMessage sellResponse)
    {
        if (sellResponse == null) return;
        
        Debug.Log($"[VendorUI] Received sell response - Success: {sellResponse.Success}, CurrentGold: {sellResponse.CurrentGold}");
        
        if (sellResponse.Success)
        {
            // Update player gold display
            _playerGold = sellResponse.CurrentGold;
            UpdateGoldDisplay();
            
            // Also update ClientPlayerStats to keep all UIs synchronized
            var clientPlayerStats = FindObjectOfType<ClientPlayerStats>();
            if (clientPlayerStats != null)
            {
                clientPlayerStats.UpdateGold(sellResponse.CurrentGold);
                Debug.Log($"[VendorUI] Updated ClientPlayerStats gold to {sellResponse.CurrentGold}");
            }
            
            Debug.Log($"[VendorUI] Updated gold display to {_playerGold} after selling item");
        }
    }
    
    private void RefreshVendorDisplay()
    {
        Debug.Log($"[VendorUI] RefreshVendorDisplay - _vendorSlots.Count: {_vendorSlots.Count}, _playerInventory.Count: {_playerInventory.Count}");
        
        // Clear all slots first (matching InventoryUI approach)
        foreach (var slot in _vendorSlots)
        {
            slot.ClearSlot();
        }
        
        // Place items in their assigned slots by SlotIndex (matching InventoryUI logic)
        foreach (var item in _playerInventory)
        {
            if (item.SlotIndex >= 0 && item.SlotIndex < _vendorSlots.Count)
            {
                var slot = _vendorSlots[item.SlotIndex];
                int sellPrice = CalculateSellPrice(item);
                Debug.Log($"[VendorUI] Slot {item.SlotIndex}: Updating with {item.ItemName} ({item.ItemType}) - Price: {sellPrice}g");
                slot.UpdateSlot(item, sellPrice);
            }
            else
            {
                Debug.LogWarning($"[VendorUI] Item {item.ItemName} has invalid SlotIndex: {item.SlotIndex}");
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
            var playerStats = FindObjectOfType<ClientPlayerStats>();
            int currentGold = playerStats != null ? playerStats.Gold : _playerGold;
            PlayerGoldText.text = $"Gold: {currentGold}";
        }
    }
    
    /// <summary>
    /// Handle player stats updates (including gold changes)
    /// </summary>
    private void HandlePlayerStatsUpdate(ClientPlayerStats stats)
    {
        if (stats != null)
        {
            _playerGold = stats.Gold; // Keep local copy in sync
            UpdateGoldDisplay();
        }
    }
    
    /// <summary>
    /// Handle specific gold changes
    /// </summary>
    private void HandleGoldChanged(int newGold)
    {
        _playerGold = newGold; // Keep local copy in sync
        UpdateGoldDisplay();
    }
    
    private void SetupItemDetailsPanel()
    {
        Debug.Log($"[VendorUI] SetupItemDetailsPanel() called - ItemDetailsPanel: {(ItemDetailsPanel != null ? "exists" : "null")}, VendorContainer: {(VendorContainer != null ? "valid" : "null")}");
        
        // Check if ItemDetailsPanel reference exists but GameObject was destroyed
        if (ItemDetailsPanel != null)
        {
            try 
            {
                // Try to access the GameObject - this will throw if destroyed
                _ = ItemDetailsPanel.name;
            }
            catch
            {
                // GameObject was destroyed but reference remains, clear it
                Debug.Log("[VendorUI] ItemDetailsPanel reference exists but GameObject was destroyed, clearing reference");
                ItemDetailsPanel = null;
            }
        }
        
        // Create details panel if it doesn't exist (similar to InventoryUI pattern)
        if (ItemDetailsPanel == null)
        {
            if (VendorContainer == null)
            {
                Debug.LogError("[VendorUI] Cannot create ItemDetailsPanel - VendorContainer is null!");
                return;
            }
            
            ItemDetailsPanel = new GameObject("ItemDetailsPanel");
            ItemDetailsPanel.transform.SetParent(VendorContainer, false);
            
            // Add Image component for background
            var bgImage = ItemDetailsPanel.AddComponent<Image>();
            bgImage.color = new Color(0.8f, 0.2f, 0.2f, 0.9f); // Temporary bright red background for debugging
            
            // Position to match the full width of the grid layout (same horizontal bounds as vendor slots)
            var detailsRect = ItemDetailsPanel.GetComponent<RectTransform>();
            detailsRect.anchorMin = new Vector2(0.05f, 0.05f);
            detailsRect.anchorMax = new Vector2(0.95f, 0.35f);
            detailsRect.anchoredPosition = Vector2.zero;
            detailsRect.sizeDelta = Vector2.zero;
        }
        
        // Create text component for item details
        if (_itemDetailsText == null)
        {
            GameObject textObj = new GameObject("VendorDetailsText");
            textObj.transform.SetParent(ItemDetailsPanel.transform, false);
            
            _itemDetailsText = textObj.AddComponent<Text>();
            _itemDetailsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _itemDetailsText.fontSize = 14;
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
        
        // Temporarily make panel visible for debugging positioning
        ItemDetailsPanel.SetActive(true);
        
        Debug.Log("[VendorUI] Item details panel created and positioned");
    }
    
    private void SetupItemDetailsText()
    {
        if (ItemDetailsPanel != null && _itemDetailsText == null)
        {
            Transform detailsTextTransform = ItemDetailsPanel.transform.Find("VendorDetailsText");
            if (detailsTextTransform != null)
            {
                _itemDetailsText = detailsTextTransform.GetComponent<Text>();
                Debug.Log("[VendorUI] Found and connected to VendorDetailsText component");
            }
            else
            {
                Debug.LogError("[VendorUI] Could not find VendorDetailsText child in ItemDetailsPanel");
            }
        }
    }
    
    
    private void HandleSlotHoverEnter(int slotIndex, InventoryItem item)
    {
        Debug.Log($"[VendorUI] HandleSlotHoverEnter called - Slot: {slotIndex}, Item: {item?.ItemName ?? "NULL"}, ItemDetailsPanel: {ItemDetailsPanel != null}, DetailsText: {_itemDetailsText != null}");
        
        if (item != null && ItemDetailsPanel != null && _itemDetailsText != null)
        {
            string detailsText = GenerateVendorItemDetailsText(item);
            _itemDetailsText.text = detailsText;
            
            // Change background color based on item rarity
            var bgImage = ItemDetailsPanel.GetComponent<Image>();
            if (bgImage != null)
            {
                bgImage.color = GetRarityColor(item.Rarity);
            }
            
            ItemDetailsPanel.SetActive(true);
            
            Debug.Log($"[VendorUI] Showing details for item: {item.ItemName} (Rarity: {item.Rarity}) - Panel active: {ItemDetailsPanel.activeSelf}");
        }
        else
        {
            Debug.LogWarning($"[VendorUI] Cannot show details - Item: {item != null}, Panel: {ItemDetailsPanel != null}, Text: {_itemDetailsText != null}");
        }
    }
    
    private void HandleSlotHoverExit(int slotIndex, InventoryItem item)
    {
        Debug.Log($"[VendorUI] HandleSlotHoverExit called - Slot: {slotIndex}");
        
        if (ItemDetailsPanel != null)
        {
            // Reset background to default color
            var bgImage = ItemDetailsPanel.GetComponent<Image>();
            if (bgImage != null)
            {
                bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f); // Default dark background
            }
            
            ItemDetailsPanel.SetActive(false);
            Debug.Log("[VendorUI] Hidden vendor item details panel");
        }
        else
        {
            Debug.LogWarning("[VendorUI] ItemDetailsPanel is null in HandleSlotHoverExit");
        }
    }
    
    private string GenerateVendorItemDetailsText(InventoryItem item)
    {
        var details = new System.Text.StringBuilder();
        
        // Item name and rarity
        details.AppendLine($"<b>{item.ItemName}</b>");
        details.AppendLine($"Rarity: {VendorSlot.ExpandRarityCode(item.Rarity)}");
        
        // Description
        if (!string.IsNullOrEmpty(item.ItemDescription))
        {
            details.AppendLine($"Description: {item.ItemDescription}");
        }
        
        // Quantity info
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
        
        // Sell price calculation
        int sellPrice = CalculateSellPrice(item);
        details.AppendLine($"<color=yellow>Sell Price: {sellPrice} gold</color>");
        
        // Item properties
        details.AppendLine($"Base Value: {item.Value} gold");
        details.AppendLine($"Level: {item.Level}");
        
        // Vendor-specific instructions
        details.AppendLine("");
        details.AppendLine("<color=cyan>Click to sell this item</color>");
        
        return details.ToString();
    }
    
    private Color GetRarityColor(string rarity)
    {
        string expandedRarity = VendorSlot.ExpandRarityCode(rarity);
        
        switch (expandedRarity?.ToLower())
        {
            case "common":
                return new Color(0.3f, 0.3f, 0.3f, 0.9f); // Dark gray for common
            case "uncommon":
                return new Color(0.1f, 0.4f, 0.1f, 0.9f); // Dark green for uncommon
            case "rare":
                return new Color(0.1f, 0.1f, 0.5f, 0.9f); // Dark blue for rare
            case "epic":
                return new Color(0.4f, 0.1f, 0.4f, 0.9f); // Dark magenta for epic
            case "legendary":
                return new Color(0.5f, 0.4f, 0.1f, 0.9f); // Dark gold for legendary
            default:
                return new Color(0.1f, 0.1f, 0.1f, 0.9f); // Default dark background
        }
    }
    
    public void ShowVendorPanel()
    {
        Debug.Log($"[VendorUI] ShowVendorPanel called - GameObject active: {gameObject.activeInHierarchy}");
        Debug.Log($"[VendorUI] VendorContainer assigned: {VendorContainer != null}");
        Debug.Log($"[VendorUI] Current vendor slots count: {_vendorSlots.Count}");
        
        gameObject.SetActive(true);
        
        // Initialize UI if not already done
        if (_vendorSlots.Count == 0)
        {
            Debug.Log("[VendorUI] No vendor slots found, initializing UI...");
            InitializeVendorUI();
            Debug.Log($"[VendorUI] After initialization, vendor slots count: {_vendorSlots.Count}");
        }
        
        // Emergency fallback - create ItemDetailsPanel if it's still null
        if (ItemDetailsPanel == null && VendorContainer != null)
        {
            Debug.LogWarning("[VendorUI] EMERGENCY FALLBACK: Creating ItemDetailsPanel directly in ShowVendorPanel");
            CreateEmergencyItemDetailsPanel();
        }
        
        // Request fresh inventory data when opening
        var networkManager = FindObjectOfType<NetworkManager>();
        if (networkManager != null)
        {
            Debug.Log("[VendorUI] Found NetworkManager, requesting inventory...");
            _ = networkManager.RequestInventory();
        }
        else
        {
            Debug.LogError("[VendorUI] NetworkManager not found! Cannot request inventory.");
        }
        
        Debug.Log($"[VendorUI] Vendor panel opened for {VendorName} - ItemDetailsPanel: {ItemDetailsPanel != null}");
    }
    
    private void CreateEmergencyItemDetailsPanel()
    {
        Debug.Log("[VendorUI] Creating emergency ItemDetailsPanel...");
        
        ItemDetailsPanel = new GameObject("ItemDetailsPanel");
        ItemDetailsPanel.transform.SetParent(VendorContainer, false);
        
        // Add Image component for background
        var bgImage = ItemDetailsPanel.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.8f, 0.1f, 0.9f); // Bright green to distinguish from AutoSceneSetup version
        
        // Position to match the full width of the grid layout
        var detailsRect = ItemDetailsPanel.GetComponent<RectTransform>();
        detailsRect.anchorMin = new Vector2(0.05f, 0.05f);
        detailsRect.anchorMax = new Vector2(0.95f, 0.35f);
        detailsRect.anchoredPosition = Vector2.zero;
        detailsRect.sizeDelta = Vector2.zero;
        
        // Create text component for item details
        GameObject textObj = new GameObject("VendorDetailsText");
        textObj.transform.SetParent(ItemDetailsPanel.transform, false);
        
        _itemDetailsText = textObj.AddComponent<Text>();
        _itemDetailsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _itemDetailsText.fontSize = 14;
        _itemDetailsText.color = Color.white;
        _itemDetailsText.alignment = TextAnchor.UpperLeft;
        _itemDetailsText.verticalOverflow = VerticalWrapMode.Overflow;
        _itemDetailsText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _itemDetailsText.text = "Emergency ItemDetailsPanel created";
        
        // Position text to fill the panel with padding
        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.02f, 0.02f);
        textRect.anchorMax = new Vector2(0.98f, 0.98f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;
        
        // Make visible for debugging
        ItemDetailsPanel.SetActive(true);
        
        Debug.Log("[VendorUI] Emergency ItemDetailsPanel created with bright green background");
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
        NetworkManager.OnItemSellResponse -= HandleItemSellResponse;
        
        Debug.Log("[VendorUI] Unsubscribed from network events");
    }
}