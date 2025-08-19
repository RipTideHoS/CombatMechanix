using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple player health bar that follows the exact pattern of working enemy health bars
/// No Canvas hierarchy complications - just a basic slider that works
/// </summary>
public class SimplePlayerHealthBar : MonoBehaviour
{
    [Header("UI References")]
    public Slider HealthSlider;
    public Text HealthText;
    public Image HealthFillImage;
    public Image BackgroundImage;

    [Header("Colors")]
    public Color FullHealthColor = Color.green;
    public Color MidHealthColor = Color.yellow;
    public Color LowHealthColor = Color.red;
    public Color BackgroundColor = new Color(0, 0, 0, 0.5f);

    private ClientPlayerStats _playerStats;

    private void Start()
    {
        SetupUI();
        StartCoroutine(InitializeWithDelay());
    }

    private System.Collections.IEnumerator InitializeWithDelay()
    {
        Debug.Log("[SimplePlayerHealthBar] InitializeWithDelay started");
        
        // Wait for other components to initialize
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        
        // Find player stats component
        _playerStats = FindObjectOfType<ClientPlayerStats>();
        if (_playerStats == null)
        {
            Debug.LogWarning("[SimplePlayerHealthBar] ClientPlayerStats not found!");
            yield break;
        }

        // Subscribe to health change events - COPY EXACT PATTERN FROM ENEMY HEALTH BARS
        ClientPlayerStats.OnHealthChanged += OnHealthChanged;
        ClientPlayerStats.OnStatsUpdated += OnStatsUpdated;
        
        Debug.Log("[SimplePlayerHealthBar] Successfully subscribed to health events");
        
        // Initial health display
        UpdateHealthDisplay();
    }

    private void SetupUI()
    {
        // Setup health slider - COPY EXACT PATTERN FROM ENEMY HEALTH BARS
        if (HealthSlider != null)
        {
            HealthSlider.minValue = 0f;
            HealthSlider.maxValue = 1f;
            HealthSlider.value = 1f;
            
            Debug.Log($"[SimplePlayerHealthBar] Slider setup - Min: {HealthSlider.minValue}, Max: {HealthSlider.maxValue}, Value: {HealthSlider.value}");
            Debug.Log($"[SimplePlayerHealthBar] Slider fillRect: {HealthSlider.fillRect != null}, targetGraphic: {HealthSlider.targetGraphic != null}");
            
            if (HealthFillImage != null)
            {
                Debug.Log($"[SimplePlayerHealthBar] FillImage type: {HealthFillImage.type}, fillMethod: {HealthFillImage.fillMethod}, fillAmount: {HealthFillImage.fillAmount}");
            }
        }

        // Setup colors
        if (HealthFillImage != null)
        {
            HealthFillImage.color = FullHealthColor;
        }

        if (BackgroundImage != null)
        {
            BackgroundImage.color = BackgroundColor;
        }
    }

    private void OnHealthChanged(int newHealth, int healthChange)
    {
        Debug.Log($"[SimplePlayerHealthBar] OnHealthChanged called: {healthChange} -> {newHealth}");
        UpdateHealthDisplay();
    }

    private void OnStatsUpdated(ClientPlayerStats stats)
    {
        Debug.Log($"[SimplePlayerHealthBar] OnStatsUpdated called");
        UpdateHealthDisplay();
    }

    private void UpdateHealthDisplay()
    {
        if (_playerStats == null) 
        {
            Debug.LogWarning("[SimplePlayerHealthBar] _playerStats is null in UpdateHealthDisplay");
            return;
        }

        if (_playerStats.MaxHealth <= 0) return;

        float healthPercentage = (float)_playerStats.Health / _playerStats.MaxHealth;
        
        Debug.Log($"[SimplePlayerHealthBar] Updating health display: {_playerStats.Health}/{_playerStats.MaxHealth} = {healthPercentage:P1}");
        
        // Update the slider value - COPY THE EXACT PATTERN FROM WORKING ENEMY HEALTH BARS
        if (HealthSlider != null)
        {
            float oldValue = HealthSlider.value;
            HealthSlider.value = healthPercentage;
            
            Debug.Log($"[SimplePlayerHealthBar] Health slider updated: {oldValue:F3} -> {HealthSlider.value:F3} (percentage: {healthPercentage:P1})");
        }

        // Update health text
        if (HealthText != null)
        {
            HealthText.text = $"{_playerStats.Health}/{_playerStats.MaxHealth}";
            Debug.Log($"[SimplePlayerHealthBar] Health text updated to: {HealthText.text}");
        }

        // Update health bar color based on percentage - COPY EXACT PATTERN FROM ENEMY HEALTH BARS
        UpdateHealthBarColor(healthPercentage);
    }

    private void UpdateHealthBarColor(float healthPercentage)
    {
        if (HealthFillImage == null) return;

        Color targetColor;
        
        if (healthPercentage > 0.6f)
        {
            // Interpolate between full and mid health color
            float t = (healthPercentage - 0.6f) / 0.4f;
            targetColor = Color.Lerp(MidHealthColor, FullHealthColor, t);
        }
        else if (healthPercentage > 0.3f)
        {
            // Interpolate between mid and low health color
            float t = (healthPercentage - 0.3f) / 0.3f;
            targetColor = Color.Lerp(LowHealthColor, MidHealthColor, t);
        }
        else
        {
            // Low health - pure red
            targetColor = LowHealthColor;
        }

        HealthFillImage.color = targetColor;
        Debug.Log($"[SimplePlayerHealthBar] Updated health bar color to {targetColor} for {healthPercentage:P1} health");
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        ClientPlayerStats.OnHealthChanged -= OnHealthChanged;
        ClientPlayerStats.OnStatsUpdated -= OnStatsUpdated;
        
        Debug.Log("[SimplePlayerHealthBar] OnDestroy - unsubscribed from events");
    }

    // Public method for manual testing
    public void TestHealthChange()
    {
        if (_playerStats != null)
        {
            Debug.Log("[SimplePlayerHealthBar] Manual test - simulating health change");
            UpdateHealthDisplay();
        }
    }
}