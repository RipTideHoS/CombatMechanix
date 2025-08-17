using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class InventoryManager : MonoBehaviour
{
    [Header("Inventory Settings")]
    public int InventorySize = 20;

    [Header("UI References")]
    public Transform InventoryGrid; // Parent object for inventory slot UI elements
    public GameObject InventorySlotPrefab; // Prefab for individual inventory slots

    private Dictionary<string, int> _inventory = new Dictionary<string, int>();
    private List<InventoryItem> _inventorySlots = new List<InventoryItem>();
    private List<GameObject> _slotUIElements = new List<GameObject>();

    public event Action OnInventoryChanged;

    private void Start()
    {
        InitializeInventory();
        CreateInventoryUI();
    }

    private void InitializeInventory()
    {
        _inventorySlots = new List<InventoryItem>(InventorySize);
        for (int i = 0; i < InventorySize; i++)
        {
            _inventorySlots.Add(null);
        }

        Debug.Log($"Inventory initialized with {InventorySize} slots");
    }

    private void CreateInventoryUI()
    {
        if (InventoryGrid == null || InventorySlotPrefab == null) return;

        // Clear existing UI elements
        foreach (var element in _slotUIElements)
        {
            if (element != null)
                DestroyImmediate(element);
        }
        _slotUIElements.Clear();

        // Create UI slots
        for (int i = 0; i < InventorySize; i++)
        {
            var slotObj = Instantiate(InventorySlotPrefab, InventoryGrid);
            slotObj.name = $"InventorySlot_{i}";
            _slotUIElements.Add(slotObj);

            // Setup slot click handler
            var button = slotObj.GetComponent<UnityEngine.UI.Button>();
            if (button != null)
            {
                int slotIndex = i; // Capture for closure
                button.onClick.AddListener(() => OnSlotClicked(slotIndex));
            }
        }
    }

    public bool AddItem(string itemType, int quantity)
    {
        // Try to stack with existing items first
        for (int i = 0; i < _inventorySlots.Count; i++)
        {
            if (_inventorySlots[i] != null && _inventorySlots[i].ItemType == itemType)
            {
                _inventorySlots[i].Quantity += quantity;
                OnInventoryChanged?.Invoke();
                UpdateInventoryUI();
                Debug.Log($"Added {quantity} {itemType} to existing stack. New total: {_inventorySlots[i].Quantity}");
                return true;
            }
        }

        // Find empty slot
        for (int i = 0; i < _inventorySlots.Count; i++)
        {
            if (_inventorySlots[i] == null)
            {
                _inventorySlots[i] = new InventoryItem
                {
                    ItemType = itemType,
                    Quantity = quantity,
                    SlotIndex = i
                };
                OnInventoryChanged?.Invoke();
                UpdateInventoryUI();
                Debug.Log($"Added {quantity} {itemType} to slot {i}");
                return true;
            }
        }

        // Inventory full
        if (GameManager.Instance.UIManager != null)
        {
            GameManager.Instance.UIManager.ShowMessage("Inventory is full!");
        }
        Debug.LogWarning("Inventory is full!");
        return false;
    }

    public bool AddItem(InventoryItem item)
    {
        if (item == null)
        {
            Debug.LogWarning("Cannot add null item to inventory");
            return false;
        }

        // Try to stack with existing items first (if item is stackable)
        if (item.IsStackable)
        {
            for (int i = 0; i < _inventorySlots.Count; i++)
            {
                if (_inventorySlots[i] != null && 
                    _inventorySlots[i].ItemType == item.ItemType &&
                    _inventorySlots[i].Quantity < _inventorySlots[i].MaxStackSize)
                {
                    int spaceAvailable = _inventorySlots[i].MaxStackSize - _inventorySlots[i].Quantity;
                    int amountToAdd = Mathf.Min(item.Quantity, spaceAvailable);
                    
                    _inventorySlots[i].Quantity += amountToAdd;
                    item.Quantity -= amountToAdd;
                    
                    OnInventoryChanged?.Invoke();
                    UpdateInventoryUI();
                    Debug.Log($"Added {amountToAdd} {item.ItemName} to existing stack. New total: {_inventorySlots[i].Quantity}");
                    
                    // If we've added all the items, we're done
                    if (item.Quantity <= 0)
                    {
                        return true;
                    }
                }
            }
        }

        // Find empty slot for remaining items
        for (int i = 0; i < _inventorySlots.Count; i++)
        {
            if (_inventorySlots[i] == null)
            {
                // Create a copy of the item for this slot
                var slotItem = new InventoryItem
                {
                    ItemId = item.ItemId,
                    ItemType = item.ItemType,
                    ItemName = item.ItemName,
                    ItemDescription = item.ItemDescription,
                    Quantity = item.Quantity,
                    SlotIndex = i,
                    IconName = item.IconName,
                    Rarity = item.Rarity,
                    Level = item.Level,
                    IsStackable = item.IsStackable,
                    MaxStackSize = item.MaxStackSize,
                    AttackPower = item.AttackPower,
                    DefensePower = item.DefensePower,
                    Value = item.Value
                };
                
                _inventorySlots[i] = slotItem;
                Debug.Log($"[InventoryManager] *** UI DEBUG *** Added {item.ItemName} (x{item.Quantity}) to slot {i}");
                Debug.Log($"[InventoryManager] *** UI DEBUG *** Slots used after addition: {_inventorySlots.Count(slot => slot != null)} / {_inventorySlots.Count}");
                
                OnInventoryChanged?.Invoke();
                Debug.Log($"[InventoryManager] *** UI DEBUG *** OnInventoryChanged event invoked");
                
                UpdateInventoryUI();
                Debug.Log($"[InventoryManager] *** UI DEBUG *** UpdateInventoryUI() called");
                
                return true;
            }
        }

        // Inventory full
        if (GameManager.Instance.UIManager != null)
        {
            GameManager.Instance.UIManager.ShowMessage("Inventory is full!");
        }
        Debug.LogWarning("Inventory is full!");
        return false;
    }

    public bool RemoveItem(string itemType, int quantity)
    {
        for (int i = 0; i < _inventorySlots.Count; i++)
        {
            if (_inventorySlots[i] != null && _inventorySlots[i].ItemType == itemType)
            {
                if (_inventorySlots[i].Quantity >= quantity)
                {
                    _inventorySlots[i].Quantity -= quantity;
                    if (_inventorySlots[i].Quantity <= 0)
                    {
                        _inventorySlots[i] = null;
                    }
                    OnInventoryChanged?.Invoke();
                    UpdateInventoryUI();
                    Debug.Log($"Removed {quantity} {itemType}");
                    return true;
                }
            }
        }
        Debug.LogWarning($"Not enough {itemType} in inventory");
        return false;
    }

    public int GetItemCount(string itemType)
    {
        int total = 0;
        foreach (var slot in _inventorySlots)
        {
            if (slot != null && slot.ItemType == itemType)
            {
                total += slot.Quantity;
            }
        }
        return total;
    }

    public List<InventoryItem> GetInventoryItems()
    {
        return _inventorySlots.Where(slot => slot != null).ToList();
    }

    public bool HasItem(string itemType, int quantity = 1)
    {
        return GetItemCount(itemType) >= quantity;
    }

    public bool IsInventoryFull()
    {
        return _inventorySlots.All(slot => slot != null);
    }

    public int GetEmptySlotCount()
    {
        return _inventorySlots.Count(slot => slot == null);
    }

    private void OnSlotClicked(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < _inventorySlots.Count)
        {
            var item = _inventorySlots[slotIndex];
            if (item != null)
            {
                Debug.Log($"Clicked slot {slotIndex}: {item.ItemType} x{item.Quantity}");
                // Handle item use/interaction here
                UseItem(slotIndex);
            }
        }
    }

    private void UseItem(int slotIndex)
    {
        var item = _inventorySlots[slotIndex];
        if (item == null) return;

        // Basic item usage logic
        switch (item.ItemType.ToLower())
        {
            case "health_potion":
                Debug.Log("Used health potion!");
                RemoveItem(item.ItemType, 1);
                // Heal player logic would go here
                break;

            case "mana_potion":
                Debug.Log("Used mana potion!");
                RemoveItem(item.ItemType, 1);
                // Restore mana logic would go here
                break;

            default:
                Debug.Log($"Cannot use item: {item.ItemType}");
                break;
        }
    }

    private void UpdateInventoryUI()
    {
        Debug.Log($"[InventoryManager] *** UI DEBUG *** UpdateInventoryUI called");
        Debug.Log($"[InventoryManager] *** UI DEBUG *** _slotUIElements.Count: {_slotUIElements.Count}, _inventorySlots.Count: {_inventorySlots.Count}");
        
        if (_slotUIElements.Count != _inventorySlots.Count)
        {
            Debug.LogWarning($"[InventoryManager] *** UI DEBUG *** UI Elements count mismatch! UI Elements: {_slotUIElements.Count}, Inventory Slots: {_inventorySlots.Count} - RETURNING EARLY");
            return;
        }

        Debug.Log($"[InventoryManager] *** UI DEBUG *** Updating {_inventorySlots.Count} slots");

        for (int i = 0; i < _inventorySlots.Count; i++)
        {
            var slotUI = _slotUIElements[i];
            var item = _inventorySlots[i];

            Debug.Log($"[InventoryManager] *** UI DEBUG *** Updating slot {i}: {(item != null ? item.ItemName : "EMPTY")}");

            // Get UI components
            var text = slotUI.GetComponentInChildren<UnityEngine.UI.Text>();
            var image = slotUI.GetComponent<UnityEngine.UI.Image>();

            if (item != null)
            {
                Debug.Log($"[InventoryManager] *** UI DEBUG *** Setting slot {i} to show: {item.ItemType} x{item.Quantity}");
                
                // Show item info
                if (text != null)
                {
                    text.text = $"{item.ItemType}\nx{item.Quantity}";
                    Debug.Log($"[InventoryManager] *** UI DEBUG *** Text component updated for slot {i}");
                }
                else
                {
                    Debug.LogWarning($"[InventoryManager] *** UI DEBUG *** No Text component found in slot {i}");
                }

                // Set item icon (you would load actual item icons here)
                if (image != null)
                {
                    image.color = GetItemColor(item.ItemType);
                    Debug.Log($"[InventoryManager] *** UI DEBUG *** Image color set for slot {i}");
                }
                else
                {
                    Debug.LogWarning($"[InventoryManager] *** UI DEBUG *** No Image component found in slot {i}");
                }
            }
            else
            {
                Debug.Log($"[InventoryManager] *** UI DEBUG *** Setting slot {i} to empty");
                // Empty slot
                if (text != null)
                {
                    text.text = "";
                }

                if (image != null)
                {
                    image.color = Color.gray;
                }
            }
        }
    }

    private Color GetItemColor(string itemType)
    {
        // Simple color coding for different item types
        switch (itemType.ToLower())
        {
            case "wood": return new Color(0.6f, 0.3f, 0.1f); // Brown
            case "stone": return Color.gray;
            case "iron": return new Color(0.7f, 0.7f, 0.8f); // Silver
            case "gold": return Color.yellow;
            case "health_potion": return Color.red;
            case "mana_potion": return Color.blue;
            default: return Color.white;
        }
    }

    // Method to save/load inventory (placeholder)
    public void SaveInventory()
    {
        // In a full implementation, this would save to PlayerPrefs or server
        Debug.Log("Inventory saved");
    }

    public void LoadInventory()
    {
        // In a full implementation, this would load from PlayerPrefs or server
        Debug.Log("Inventory loaded");
    }
}