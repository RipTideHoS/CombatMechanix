using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

/// <summary>
/// Manages the character equipment UI panel with 6 slots for equipped gear
/// Handles equipment data from server and visual representation
/// Occupies the same screen space as inventory panel (mutually exclusive)
/// </summary>
public class CharacterUI : MonoBehaviour
{
    [Header("Equipment Settings")]
    public int MaxEquipmentSlots = 6;
    public Vector2 SlotSize = new Vector2(64, 64);
    public Vector2 SlotSpacing = new Vector2(8, 8);
    
    [Header("UI References")]
    public Transform CharacterContainer;
    public GameObject SlotPrefab; // Will be created if not assigned
    public GameObject ItemDetailsPanel; // Will be created if not assigned
    
    [Header("Item Icon Settings")]
    public bool ShowPlaceholderIcons = true;
    public Color PlaceholderColor = Color.gray;
    
    // Equipment slot types in order
    private readonly string[] _equipmentSlotTypes = { "Helmet", "Chest", "Legs", "Weapon", "Offhand", "Accessory" };
    
    // Internal components
    private List<EquipmentSlot> _equipmentSlots = new List<EquipmentSlot>();
    private List<EquippedItem> _equipmentData = new List<EquippedItem>();
    private ItemIconManager _iconManager;
    private Text _itemDetailsText;
    private Text _statsDisplayText;
    private GameObject _statsDisplayPanel;
    
    // Events
    public System.Action<EquippedItem> OnItemClicked;
    public System.Action<EquippedItem> OnItemRightClicked;
    
    private void Awake()
    {
        // Subscribe to equipment network events
        NetworkManager.OnEquipmentResponse += HandleEquipmentResponse;
        NetworkManager.OnEquipmentUpdate += HandleEquipmentUpdate;
        
        Debug.Log("[CharacterUI] Subscribed to equipment network events");
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        NetworkManager.OnEquipmentResponse -= HandleEquipmentResponse;
        NetworkManager.OnEquipmentUpdate -= HandleEquipmentUpdate;
    }
    
    private void Start()
    {
        SetupCharacterUI();
        SetupItemIconManager();
        SetupStatsDisplayPanel();
        SetupItemDetailsPanel();
        
        // Request equipment data from server
        RequestEquipmentFromServer();
    }
    
    private void SetupCharacterUI()
    {
        if (CharacterContainer == null)
        {
            Debug.LogError("[CharacterUI] CharacterContainer not assigned! Looking for existing container...");
            
            // Try to find existing character panel
            var characterPanel = GameObject.Find("CharacterPanel");
            if (characterPanel != null)
            {
                CharacterContainer = characterPanel.transform;
                Debug.Log("[CharacterUI] Found existing CharacterPanel");
            }
            else
            {
                Debug.LogError("[CharacterUI] No CharacterContainer found!");
                return;
            }
        }
        
        CreateEquipmentSlots();
        Debug.Log($"[CharacterUI] Created {_equipmentSlots.Count} equipment slots");
    }
    
    private void SetupItemIconManager()
    {
        _iconManager = gameObject.AddComponent<ItemIconManager>();
        _iconManager.Initialize();
        Debug.Log("[CharacterUI] ItemIconManager initialized");
    }
    
    private void SetupStatsDisplayPanel()
    {
        // Create stats display panel if it doesn't exist
        if (_statsDisplayPanel == null)
        {
            _statsDisplayPanel = new GameObject("StatsDisplayPanel");
            _statsDisplayPanel.transform.SetParent(CharacterContainer, false);
            
            // Add Image component for background
            var bgImage = _statsDisplayPanel.AddComponent<Image>();
            bgImage.color = new Color(0.15f, 0.2f, 0.05f, 0.9f); // Green-tinted background
            
            // Position similar to gold display in inventory
            var statsRect = _statsDisplayPanel.GetComponent<RectTransform>();
            statsRect.anchorMin = new Vector2(0.05f, 0.37f);
            statsRect.anchorMax = new Vector2(0.95f, 0.41f);
            statsRect.anchoredPosition = Vector2.zero;
            statsRect.sizeDelta = Vector2.zero;
        }
        
        // Create text component for equipment stats
        if (_statsDisplayText == null)
        {
            GameObject textObj = new GameObject("StatsText");
            textObj.transform.SetParent(_statsDisplayPanel.transform, false);
            
            _statsDisplayText = textObj.AddComponent<Text>();
            _statsDisplayText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _statsDisplayText.fontSize = 16;
            _statsDisplayText.color = new Color(0.8f, 1f, 0.8f, 1f); // Light green color
            _statsDisplayText.alignment = TextAnchor.MiddleCenter;
            _statsDisplayText.fontStyle = FontStyle.Bold;
            _statsDisplayText.text = "Equipment Stats";
            
            // Position text to fill the panel
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = Vector2.zero;
        }
        
        // Update stats display with current equipment
        UpdateStatsDisplay();
        
        Debug.Log("[CharacterUI] Equipment stats display panel created");
    }
    
    private void SetupItemDetailsPanel()
    {
        // Create details panel if it doesn't exist
        if (ItemDetailsPanel == null)
        {
            ItemDetailsPanel = new GameObject("ItemDetailsPanel");
            ItemDetailsPanel.transform.SetParent(CharacterContainer, false);
            
            // Add Image component for background
            var bgImage = ItemDetailsPanel.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f); // Dark background
            
            // Position with 2% buffer below the stats display panel
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
        
        Debug.Log("[CharacterUI] Item details panel created and positioned");
    }
    
    private void CreateEquipmentSlots()
    {
        // Clear existing slots
        foreach (var slot in _equipmentSlots)
        {
            if (slot != null && slot.gameObject != null)
                DestroyImmediate(slot.gameObject);
        }
        _equipmentSlots.Clear();
        
        // Create equipment layout container if it doesn't exist
        GameObject layoutContainer = CharacterContainer.Find("EquipmentLayout")?.gameObject;
        if (layoutContainer == null)
        {
            layoutContainer = new GameObject("EquipmentLayout");
            layoutContainer.transform.SetParent(CharacterContainer, false);
            
            // Add RectTransform component for UI positioning
            var layoutRect = layoutContainer.AddComponent<RectTransform>();
            
            // Position the equipment layout (leave space for title, stats display and details panel)
            layoutRect.anchorMin = new Vector2(0.05f, 0.42f);
            layoutRect.anchorMax = new Vector2(0.95f, 0.85f);
            layoutRect.anchoredPosition = Vector2.zero;
            layoutRect.sizeDelta = Vector2.zero;
        }
        
        // Create equipment slots in a character-like arrangement
        CreateEquipmentSlotLayout(layoutContainer);
        
        Debug.Log($"[CharacterUI] Created {MaxEquipmentSlots} equipment slots in character layout");
    }
    
    private void CreateEquipmentSlotLayout(GameObject layoutContainer)
    {
        // Equipment slot positions in a character layout
        // Helmet (top center), Chest (center), Legs (bottom center)
        // Weapon (left center), Offhand (right center), Accessory (bottom right)
        
        var slotPositions = new Vector2[]
        {
            new Vector2(0.5f, 0.85f),  // Helmet (top center)
            new Vector2(0.5f, 0.6f),   // Chest (center)
            new Vector2(0.5f, 0.35f),  // Legs (bottom center)
            new Vector2(0.2f, 0.6f),   // Weapon (left center)
            new Vector2(0.8f, 0.6f),   // Offhand (right center)
            new Vector2(0.8f, 0.35f)   // Accessory (bottom right)
        };
        
        for (int i = 0; i < MaxEquipmentSlots; i++)
        {
            GameObject slotObj = CreateEquipmentSlot(i, _equipmentSlotTypes[i]);
            slotObj.transform.SetParent(layoutContainer.transform, false);
            
            // Position the slot manually
            var slotRect = slotObj.GetComponent<RectTransform>();
            slotRect.anchorMin = slotPositions[i];
            slotRect.anchorMax = slotPositions[i];
            slotRect.anchoredPosition = Vector2.zero;
            slotRect.sizeDelta = SlotSize;
            
            var slot = slotObj.GetComponent<EquipmentSlot>();
            if (slot != null)
            {
                _equipmentSlots.Add(slot);
                
                // Setup click and hover events
                slot.OnSlotClicked += HandleSlotClicked;
                slot.OnSlotRightClicked += HandleSlotRightClicked;
                slot.OnSlotHoverEnter += HandleSlotHoverEnter;
                slot.OnSlotHoverExit += HandleSlotHoverExit;
            }
        }
    }
    
    private GameObject CreateEquipmentSlot(int slotIndex, string slotType)
    {
        GameObject slotObj = new GameObject($"EquipmentSlot_{slotType}");
        
        // Add Image component for background
        var bgImage = slotObj.AddComponent<Image>();
        bgImage.color = new Color(0.3f, 0.3f, 0.3f, 0.8f); // Dark slot background
        
        // Add EquipmentSlot component
        var slot = slotObj.AddComponent<EquipmentSlot>();
        slot.SlotIndex = slotIndex;
        slot.SlotType = slotType;
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
        
        // Create slot label text (shows slot type when empty)
        GameObject labelObj = new GameObject("SlotLabel");
        labelObj.transform.SetParent(slotObj.transform, false);
        
        var labelText = labelObj.AddComponent<Text>();
        labelText.text = slotType;
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontSize = 10;
        labelText.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.fontStyle = FontStyle.Normal;
        
        // Position label text to fill the slot
        var labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = Vector2.zero;
        
        slot.SlotLabel = labelText;
        
        // Initially show slot label, hide icon
        iconObj.SetActive(false);
        labelObj.SetActive(true);
        
        return slotObj;
    }
    
    private async void RequestEquipmentFromServer()
    {
        Debug.Log("[CharacterUI] Requesting equipment from server...");
        
        var networkManager = FindObjectOfType<NetworkManager>();
        if (networkManager != null)
        {
            await networkManager.RequestEquipment();
        }
        else
        {
            Debug.LogError("[CharacterUI] NetworkManager not found!");
        }
    }
    
    private void HandleEquipmentResponse(NetworkMessages.EquipmentResponseMessage response)
    {
        Debug.Log($"[CharacterUI] Received equipment response with {response.Items.Count} items");
        
        if (response.Success)
        {
            _equipmentData = response.Items;
            UpdateEquipmentDisplay();
        }
        else
        {
            Debug.LogError($"[CharacterUI] Equipment request failed: {response.ErrorMessage}");
        }
    }
    
    private void HandleEquipmentUpdate(NetworkMessages.EquipmentUpdateMessage update)
    {
        Debug.Log($"[CharacterUI] Received equipment update: {update.UpdateType} with {update.UpdatedItems.Count} items");
        
        switch (update.UpdateType)
        {
            case "Equip":
                foreach (var item in update.UpdatedItems)
                {
                    AddOrUpdateEquipment(item);
                }
                break;
                
            case "Unequip":
                foreach (var item in update.UpdatedItems)
                {
                    RemoveEquipment(item.SlotType);
                }
                break;
                
            case "Replace":
                foreach (var item in update.UpdatedItems)
                {
                    AddOrUpdateEquipment(item);
                }
                break;
        }
        
        UpdateEquipmentDisplay();
        UpdateStatsDisplay();
    }
    
    private void AddOrUpdateEquipment(EquippedItem newItem)
    {
        var existingItem = _equipmentData.FirstOrDefault(i => i.SlotType == newItem.SlotType);
        if (existingItem != null)
        {
            _equipmentData.Remove(existingItem);
        }
        
        _equipmentData.Add(newItem);
    }
    
    private void RemoveEquipment(string slotType)
    {
        _equipmentData.RemoveAll(i => i.SlotType == slotType);
    }
    
    private void UpdateEquipmentDisplay()
    {
        // Clear all slots first
        foreach (var slot in _equipmentSlots)
        {
            slot.ClearSlot();
        }
        
        // Place equipped items in their respective slots
        foreach (var item in _equipmentData)
        {
            var slot = _equipmentSlots.FirstOrDefault(s => s.SlotType == item.SlotType);
            if (slot != null)
            {
                slot.SetItem(item, _iconManager.GetItemIcon(item.IconName));
            }
        }
        
        Debug.Log($"[CharacterUI] Updated equipment display with {_equipmentData.Count} items");
    }
    
    private void UpdateStatsDisplay()
    {
        if (_statsDisplayText == null) return;
        
        // Calculate total stats from equipped items
        int totalAttackPower = _equipmentData.Sum(item => item.AttackPower);
        int totalDefensePower = _equipmentData.Sum(item => item.DefensePower);
        
        _statsDisplayText.text = $"ATK: +{totalAttackPower}  DEF: +{totalDefensePower}";
        
        Debug.Log($"[CharacterUI] Updated equipment stats: ATK +{totalAttackPower}, DEF +{totalDefensePower}");
    }
    
    private void HandleSlotClicked(int slotIndex, EquippedItem item)
    {
        Debug.Log($"[CharacterUI] Equipment slot {slotIndex} left-clicked - Item: {item?.ItemName ?? "Empty"}");
        
        if (item != null)
        {
            OnItemClicked?.Invoke(item);
        }
    }
    
    private void HandleSlotRightClicked(int slotIndex, EquippedItem item)
    {
        Debug.Log($"[CharacterUI] Equipment slot {slotIndex} right-clicked - Item: {item?.ItemName ?? "Empty"}");
        
        // Check if player is dead - prevent unequipping while dead
        var playerStats = FindObjectOfType<ClientPlayerStats>();
        if (playerStats != null && !playerStats.IsAlive())
        {
            Debug.Log("[CharacterUI] Cannot unequip items while dead");
            return;
        }
        
        if (item != null)
        {
            // Right click = Unequip item
            UnequipItem(item);
            OnItemRightClicked?.Invoke(item);
        }
    }
    
    private async void UnequipItem(EquippedItem item)
    {
        if (item == null) return;
        
        Debug.Log($"[CharacterUI] Attempting to unequip item: {item.ItemName} from slot {item.SlotType}");
        
        // Send unequip request to server
        var networkManager = FindObjectOfType<NetworkManager>();
        if (networkManager != null)
        {
            var unequipRequest = new NetworkMessages.ItemUnequipRequestMessage
            {
                PlayerId = networkManager.GetPlayerId(),
                SlotType = item.SlotType
            };
            
            await networkManager.SendItemUnequipRequest(unequipRequest);
        }
        else
        {
            Debug.LogError("[CharacterUI] NetworkManager not found for item unequip!");
        }
    }
    
    private void HandleSlotHoverEnter(int slotIndex, EquippedItem item)
    {
        if (item != null && ItemDetailsPanel != null && _itemDetailsText != null)
        {
            string detailsText = GenerateItemDetailsText(item);
            _itemDetailsText.text = detailsText;
            ItemDetailsPanel.SetActive(true);
            
            Debug.Log($"[CharacterUI] Showing details for equipped item: {item.ItemName}");
        }
    }
    
    private void HandleSlotHoverExit(int slotIndex, EquippedItem item)
    {
        if (ItemDetailsPanel != null)
        {
            ItemDetailsPanel.SetActive(false);
            Debug.Log("[CharacterUI] Hidden item details panel");
        }
    }
    
    private string GenerateItemDetailsText(EquippedItem item)
    {
        var details = new System.Text.StringBuilder();
        
        // Item name and rarity
        details.AppendLine($"<b>{item.ItemName}</b>");
        details.AppendLine($"Rarity: {InventorySlot.ExpandRarityCode(item.Rarity)}");
        details.AppendLine($"Slot: {item.SlotType}");
        
        // Description
        if (!string.IsNullOrEmpty(item.ItemDescription))
        {
            details.AppendLine($"Description: {item.ItemDescription}");
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
        details.AppendLine($"Equipped: {item.DateEquipped:MM/dd/yyyy}");
        
        return details.ToString();
    }
    
    /// <summary>
    /// Public method to refresh equipment from server
    /// </summary>
    public void RefreshEquipment()
    {
        RequestEquipmentFromServer();
    }
    
    /// <summary>
    /// Get equipped item in specific slot type
    /// </summary>
    public EquippedItem GetEquippedItemInSlot(string slotType)
    {
        return _equipmentData.FirstOrDefault(i => i.SlotType == slotType);
    }
    
    /// <summary>
    /// Check if equipment slot is occupied
    /// </summary>
    public bool IsSlotOccupied(string slotType)
    {
        return _equipmentData.Any(i => i.SlotType == slotType);
    }
}