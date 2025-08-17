using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages item icons for the inventory system
/// Creates placeholder icons and will support loading real icons in the future
/// </summary>
public class ItemIconManager : MonoBehaviour
{
    [Header("Placeholder Settings")]
    public int IconSize = 64;
    public Color DefaultIconColor = Color.white;
    
    // Icon cache
    private Dictionary<string, Sprite> _iconCache = new Dictionary<string, Sprite>();
    private Dictionary<string, Texture2D> _placeholderTextures = new Dictionary<string, Texture2D>();
    
    // Predefined item type colors for placeholders
    private Dictionary<string, Color> _itemTypeColors = new Dictionary<string, Color>
    {
        { "sword", new Color(0.8f, 0.8f, 0.9f, 1f) },      // Light blue-gray for swords
        { "shield", new Color(0.7f, 0.5f, 0.3f, 1f) },     // Bronze for shields  
        { "potion", new Color(0.2f, 0.8f, 0.2f, 1f) },     // Green for potions
        { "bow", new Color(0.6f, 0.4f, 0.2f, 1f) },        // Brown for bows
        { "armor", new Color(0.5f, 0.5f, 0.6f, 1f) },      // Gray for armor
        { "helmet", new Color(0.4f, 0.4f, 0.5f, 1f) },     // Dark gray for helmets
        { "boots", new Color(0.3f, 0.2f, 0.1f, 1f) },      // Dark brown for boots
        { "ring", new Color(0.9f, 0.8f, 0.1f, 1f) },       // Gold for rings
        { "amulet", new Color(0.6f, 0.3f, 0.8f, 1f) },     // Purple for amulets
        { "gem", new Color(0.8f, 0.2f, 0.8f, 1f) },        // Magenta for gems
        { "scroll", new Color(0.9f, 0.9f, 0.7f, 1f) },     // Cream for scrolls
        { "food", new Color(0.8f, 0.4f, 0.2f, 1f) },       // Orange for food
        { "material", new Color(0.6f, 0.6f, 0.6f, 1f) },   // Gray for crafting materials
        { "key", new Color(0.9f, 0.7f, 0.1f, 1f) },        // Golden for keys
        { "tool", new Color(0.5f, 0.3f, 0.1f, 1f) }        // Dark brown for tools
    };
    
    public void Initialize()
    {
        Debug.Log("[ItemIconManager] Initializing icon manager...");
        CreatePlaceholderIcons();
    }
    
    /// <summary>
    /// Get icon sprite for an item. Returns placeholder if real icon not found.
    /// </summary>
    public Sprite GetItemIcon(string iconName)
    {
        if (string.IsNullOrEmpty(iconName))
        {
            return GetPlaceholderIcon("default");
        }
        
        // Check cache first
        if (_iconCache.TryGetValue(iconName, out Sprite cachedIcon))
        {
            return cachedIcon;
        }
        
        // Try to load real icon from Resources
        Sprite realIcon = LoadRealIcon(iconName);
        if (realIcon != null)
        {
            _iconCache[iconName] = realIcon;
            return realIcon;
        }
        
        // Fall back to placeholder
        string itemType = ExtractItemTypeFromIconName(iconName);
        Sprite placeholder = GetPlaceholderIcon(itemType);
        _iconCache[iconName] = placeholder; // Cache the placeholder too
        
        return placeholder;
    }
    
    /// <summary>
    /// Load real icon from Resources folder (for future implementation)
    /// </summary>
    private Sprite LoadRealIcon(string iconName)
    {
        // Try to load from Resources/Icons/ folder
        Sprite icon = Resources.Load<Sprite>($"Icons/{iconName}");
        
        if (icon != null)
        {
            Debug.Log($"[ItemIconManager] Loaded real icon: {iconName}");
            return icon;
        }
        
        // Could also try alternative paths or formats here
        return null;
    }
    
    /// <summary>
    /// Extract item type from icon name for placeholder selection
    /// </summary>
    private string ExtractItemTypeFromIconName(string iconName)
    {
        if (string.IsNullOrEmpty(iconName))
            return "default";
            
        string lowerIconName = iconName.ToLower();
        
        // Check if icon name contains any known item types
        foreach (var itemType in _itemTypeColors.Keys)
        {
            if (lowerIconName.Contains(itemType))
            {
                return itemType;
            }
        }
        
        // Check for common patterns
        if (lowerIconName.Contains("weapon") || lowerIconName.Contains("blade"))
            return "sword";
        if (lowerIconName.Contains("drink") || lowerIconName.Contains("bottle"))
            return "potion";
        if (lowerIconName.Contains("chest") || lowerIconName.Contains("plate"))
            return "armor";
        if (lowerIconName.Contains("head"))
            return "helmet";
        if (lowerIconName.Contains("foot") || lowerIconName.Contains("shoe"))
            return "boots";
        if (lowerIconName.Contains("jewelry"))
            return "ring";
        if (lowerIconName.Contains("magic") || lowerIconName.Contains("spell"))
            return "scroll";
        
        return "default";
    }
    
    /// <summary>
    /// Create placeholder icons for different item types
    /// </summary>
    private void CreatePlaceholderIcons()
    {
        Debug.Log("[ItemIconManager] Creating placeholder icons...");
        
        foreach (var itemType in _itemTypeColors.Keys)
        {
            CreatePlaceholderTexture(itemType, _itemTypeColors[itemType]);
        }
        
        // Create default placeholder
        CreatePlaceholderTexture("default", DefaultIconColor);
        
        Debug.Log($"[ItemIconManager] Created {_placeholderTextures.Count} placeholder textures");
    }
    
    /// <summary>
    /// Create a simple placeholder texture for an item type
    /// </summary>
    private void CreatePlaceholderTexture(string itemType, Color color)
    {
        Texture2D texture = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
        
        // Create a simple icon pattern based on item type
        for (int x = 0; x < IconSize; x++)
        {
            for (int y = 0; y < IconSize; y++)
            {
                Color pixelColor = GetPlaceholderPixelColor(itemType, x, y, color);
                texture.SetPixel(x, y, pixelColor);
            }
        }
        
        texture.Apply();
        texture.filterMode = FilterMode.Point; // Crisp pixel art style
        
        _placeholderTextures[itemType] = texture;
        
        // Create sprite from texture
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, IconSize, IconSize), new Vector2(0.5f, 0.5f));
        sprite.name = $"Placeholder_{itemType}";
        
        _iconCache[itemType] = sprite;
    }
    
    /// <summary>
    /// Generate pixel color for placeholder icon based on item type and position
    /// </summary>
    private Color GetPlaceholderPixelColor(string itemType, int x, int y, Color baseColor)
    {
        // Create a border
        if (x == 0 || y == 0 || x == IconSize - 1 || y == IconSize - 1)
        {
            return Color.black;
        }
        
        // Create inner border
        if (x == 1 || y == 1 || x == IconSize - 2 || y == IconSize - 2)
        {
            return Color.Lerp(Color.black, baseColor, 0.5f);
        }
        
        // Create simple patterns based on item type
        switch (itemType)
        {
            case "sword":
                return CreateSwordPattern(x, y, baseColor);
            case "shield":
                return CreateShieldPattern(x, y, baseColor);
            case "potion":
                return CreatePotionPattern(x, y, baseColor);
            case "bow":
                return CreateBowPattern(x, y, baseColor);
            default:
                return CreateDefaultPattern(x, y, baseColor);
        }
    }
    
    private Color CreateSwordPattern(int x, int y, Color baseColor)
    {
        // Simple sword shape - vertical line with crossguard
        int center = IconSize / 2;
        if (x >= center - 1 && x <= center + 1)
        {
            if (y > IconSize * 0.7f) // Handle
                return Color.Lerp(baseColor, Color.black, 0.3f);
            else if (y > IconSize * 0.6f && Math.Abs(x - center) < 4) // Crossguard
                return Color.Lerp(baseColor, Color.white, 0.3f);
            else // Blade
                return baseColor;
        }
        return Color.clear;
    }
    
    private Color CreateShieldPattern(int x, int y, Color baseColor)
    {
        // Simple shield shape - oval
        int centerX = IconSize / 2;
        int centerY = IconSize / 2;
        float radius = IconSize * 0.3f;
        
        float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
        if (distance < radius)
        {
            return Color.Lerp(baseColor, Color.white, 0.2f);
        }
        return Color.clear;
    }
    
    private Color CreatePotionPattern(int x, int y, Color baseColor)
    {
        // Simple bottle shape
        int centerX = IconSize / 2;
        if (y < IconSize * 0.8f && Math.Abs(x - centerX) < IconSize * 0.2f) // Bottle body
        {
            return baseColor;
        }
        if (y >= IconSize * 0.8f && Math.Abs(x - centerX) < IconSize * 0.1f) // Bottle neck
        {
            return Color.Lerp(baseColor, Color.black, 0.3f);
        }
        return Color.clear;
    }
    
    private Color CreateBowPattern(int x, int y, Color baseColor)
    {
        // Simple bow shape - curved line
        int centerY = IconSize / 2;
        if (Math.Abs(y - centerY) < 2)
        {
            return baseColor;
        }
        return Color.clear;
    }
    
    private Color CreateDefaultPattern(int x, int y, Color baseColor)
    {
        // Simple square with gradient
        float centerX = IconSize / 2f;
        float centerY = IconSize / 2f;
        float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
        float maxDistance = IconSize * 0.3f;
        
        if (distance < maxDistance)
        {
            float alpha = 1f - (distance / maxDistance);
            return Color.Lerp(Color.clear, baseColor, alpha);
        }
        
        return Color.clear;
    }
    
    /// <summary>
    /// Get placeholder icon sprite by item type
    /// </summary>
    public Sprite GetPlaceholderIcon(string itemType)
    {
        if (_iconCache.TryGetValue(itemType, out Sprite icon))
        {
            return icon;
        }
        
        // Fall back to default if specific type not found
        if (itemType != "default" && _iconCache.TryGetValue("default", out Sprite defaultIcon))
        {
            return defaultIcon;
        }
        
        Debug.LogWarning($"[ItemIconManager] No placeholder icon found for type: {itemType}");
        return null;
    }
    
    /// <summary>
    /// Clear icon cache (useful when switching scenes)
    /// </summary>
    public void ClearCache()
    {
        _iconCache.Clear();
        
        foreach (var texture in _placeholderTextures.Values)
        {
            if (texture != null)
                DestroyImmediate(texture);
        }
        _placeholderTextures.Clear();
        
        Debug.Log("[ItemIconManager] Cleared icon cache");
    }
    
    private void OnDestroy()
    {
        ClearCache();
    }
}