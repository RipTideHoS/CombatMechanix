using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Enhanced player health UI component that can display both in main UI and optionally above player
/// Integrates with ClientPlayerStats for server-authoritative health updates
/// </summary>
public class PlayerHealthUI : MonoBehaviour
{
    [Header("UI References")]
    public Slider HealthSlider;
    public Text HealthText;
    public Image HealthFillImage;
    public Image HealthBackgroundImage;
    public GameObject HealthBarContainer;

    [Header("World Space Display (Optional)")]
    public bool ShowAbovePlayer = false;
    public GameObject PlayerHealthBarPrefab;
    public Vector3 WorldOffset = new Vector3(0, 2.5f, 0);

    [Header("Animation Settings")]
    public bool AnimateHealthChanges = true;
    public float AnimationDuration = 0.5f;
    public AnimationCurve HealthChangeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Visual Effects")]
    public bool ShowDamageFlash = true;
    public Color DamageFlashColor = Color.red;
    public Color HealFlashColor = Color.green;
    public float FlashDuration = 0.3f;

    [Header("Colors")]
    public Color FullHealthColor = Color.green;
    public Color MidHealthColor = Color.yellow;
    public Color LowHealthColor = Color.red;
    public Color CriticalHealthColor = new Color(0.8f, 0, 0); // Dark red
    public Color BackgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);

    [Header("Low Health Warning")]
    public bool EnableLowHealthWarning = true;
    public float LowHealthThreshold = 0.25f;
    public float CriticalHealthThreshold = 0.15f;
    public float PulseSpeed = 2f;

    private ClientPlayerStats _playerStats;
    private float _currentDisplayedHealth;
    private float _maxDisplayedHealth;
    private bool _isAnimating = false;
    private GameObject _worldHealthBar;
    private Canvas _worldCanvas;
    private Transform _playerTransform;
    private Coroutine _animationCoroutine;
    private Coroutine _flashCoroutine;
    private Coroutine _pulseCoroutine;

    private void Start()
    {
        // Find player stats component
        _playerStats = FindObjectOfType<ClientPlayerStats>();
        if (_playerStats == null)
        {
            Debug.LogWarning("[PlayerHealthUI] ClientPlayerStats not found! Health display may not work correctly.");
        }

        // Subscribe to health change events
        ClientPlayerStats.OnHealthChanged += OnHealthChanged;
        ClientPlayerStats.OnStatsUpdated += OnStatsUpdated;

        SetupUI();
        InitializeWorldHealthBar();
        UpdateHealthDisplay();
    }

    private void SetupUI()
    {
        // Setup health slider
        if (HealthSlider != null)
        {
            HealthSlider.minValue = 0f;
            HealthSlider.maxValue = 1f;
            HealthSlider.value = 1f;
        }

        // Setup colors
        if (HealthFillImage != null)
        {
            HealthFillImage.color = FullHealthColor;
        }

        if (HealthBackgroundImage != null)
        {
            HealthBackgroundImage.color = BackgroundColor;
        }

        // Initial health display
        if (_playerStats != null)
        {
            _currentDisplayedHealth = _playerStats.Health;
            _maxDisplayedHealth = _playerStats.MaxHealth;
        }
        else
        {
            _currentDisplayedHealth = 100f;
            _maxDisplayedHealth = 100f;
        }
    }

    private void InitializeWorldHealthBar()
    {
        if (!ShowAbovePlayer || PlayerHealthBarPrefab == null) return;

        // Find the local player
        var playerController = FindObjectOfType<PlayerController>();
        if (playerController == null)
        {
            Debug.LogWarning("[PlayerHealthUI] PlayerController not found for world health bar");
            return;
        }

        // Store player transform for position updates
        _playerTransform = playerController.transform;

        // Create world space health bar
        _worldHealthBar = Instantiate(PlayerHealthBarPrefab, _playerTransform);
        _worldHealthBar.transform.localPosition = WorldOffset;

        // Setup world canvas
        _worldCanvas = _worldHealthBar.GetComponentInChildren<Canvas>();
        if (_worldCanvas != null)
        {
            _worldCanvas.renderMode = RenderMode.WorldSpace;
            _worldCanvas.worldCamera = Camera.main;
            _worldCanvas.transform.localScale = Vector3.one * 0.01f;
        }

        Debug.Log("[PlayerHealthUI] World space health bar created above player");
    }

    private void Update()
    {
        // Update world health bar position and rotation
        if (_worldHealthBar != null && _playerTransform != null)
        {
            UpdateWorldHealthBarPosition();
        }
    }

    private void UpdateWorldHealthBarPosition()
    {
        // Update position to follow player
        _worldHealthBar.transform.position = _playerTransform.position + WorldOffset;
        
        // Make health bar face the camera
        if (Camera.main != null)
        {
            Vector3 directionToCamera = Camera.main.transform.position - _worldHealthBar.transform.position;
            _worldHealthBar.transform.rotation = Quaternion.LookRotation(directionToCamera);
        }
    }

    private void OnHealthChanged(int newHealth, int healthChange)
    {
        if (_playerStats == null) return;

        float oldHealth = _currentDisplayedHealth;
        _currentDisplayedHealth = newHealth;
        _maxDisplayedHealth = _playerStats.MaxHealth;

        // Trigger visual effects
        if (ShowDamageFlash)
        {
            Color flashColor = healthChange < 0 ? DamageFlashColor : HealFlashColor;
            StartHealthFlash(flashColor);
        }

        // Animate health change
        if (AnimateHealthChanges && !_isAnimating)
        {
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
            }
            _animationCoroutine = StartCoroutine(AnimateHealthChange(oldHealth, _currentDisplayedHealth));
        }
        else
        {
            UpdateHealthDisplay();
        }

        // Handle low health warning
        HandleLowHealthWarning();

        Debug.Log($"[PlayerHealthUI] Health changed: {healthChange} -> {newHealth}/{_maxDisplayedHealth}");
    }

    private void OnStatsUpdated(ClientPlayerStats stats)
    {
        if (stats == null) return;

        _currentDisplayedHealth = stats.Health;
        _maxDisplayedHealth = stats.MaxHealth;
        
        if (!_isAnimating)
        {
            UpdateHealthDisplay();
        }
    }

    private void UpdateHealthDisplay()
    {
        if (_maxDisplayedHealth <= 0) return;

        float healthPercentage = _currentDisplayedHealth / _maxDisplayedHealth;
        
        // Update slider
        if (HealthSlider != null)
        {
            HealthSlider.value = healthPercentage;
        }

        // Update text
        if (HealthText != null)
        {
            HealthText.text = $"{_currentDisplayedHealth:F0}/{_maxDisplayedHealth:F0}";
        }

        // Update color
        UpdateHealthBarColor(healthPercentage);

        // Update world health bar if it exists
        UpdateWorldHealthBar(healthPercentage);
    }

    private void UpdateHealthBarColor(float healthPercentage)
    {
        if (HealthFillImage == null) return;

        Color targetColor;

        if (healthPercentage <= CriticalHealthThreshold)
        {
            targetColor = CriticalHealthColor;
        }
        else if (healthPercentage <= LowHealthThreshold)
        {
            float t = (healthPercentage - CriticalHealthThreshold) / (LowHealthThreshold - CriticalHealthThreshold);
            targetColor = Color.Lerp(CriticalHealthColor, LowHealthColor, t);
        }
        else if (healthPercentage <= 0.6f)
        {
            float t = (healthPercentage - LowHealthThreshold) / (0.6f - LowHealthThreshold);
            targetColor = Color.Lerp(LowHealthColor, MidHealthColor, t);
        }
        else
        {
            float t = (healthPercentage - 0.6f) / 0.4f;
            targetColor = Color.Lerp(MidHealthColor, FullHealthColor, t);
        }

        HealthFillImage.color = targetColor;
    }

    private void UpdateWorldHealthBar(float healthPercentage)
    {
        if (_worldHealthBar == null) return;

        // Find slider in world health bar
        var worldSlider = _worldHealthBar.GetComponentInChildren<Slider>();
        if (worldSlider != null)
        {
            worldSlider.value = healthPercentage;
        }

        // Find health text in world health bar
        var worldText = _worldHealthBar.GetComponentInChildren<Text>();
        if (worldText != null)
        {
            worldText.text = $"{_currentDisplayedHealth:F0}/{_maxDisplayedHealth:F0}";
        }

        // Update world health bar color
        var worldFillImage = _worldHealthBar.GetComponentInChildren<Image>();
        if (worldFillImage != null && worldFillImage.type == Image.Type.Filled)
        {
            UpdateHealthBarColor(healthPercentage);
            worldFillImage.color = HealthFillImage.color;
        }
    }

    private IEnumerator AnimateHealthChange(float fromHealth, float toHealth)
    {
        _isAnimating = true;
        float elapsed = 0f;

        while (elapsed < AnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / AnimationDuration;
            float curveValue = HealthChangeCurve.Evaluate(t);
            
            float currentHealth = Mathf.Lerp(fromHealth, toHealth, curveValue);
            
            // Update display with interpolated value
            float healthPercentage = _maxDisplayedHealth > 0 ? currentHealth / _maxDisplayedHealth : 0f;
            
            if (HealthSlider != null)
            {
                HealthSlider.value = healthPercentage;
            }
            
            if (HealthText != null)
            {
                HealthText.text = $"{currentHealth:F0}/{_maxDisplayedHealth:F0}";
            }
            
            UpdateHealthBarColor(healthPercentage);
            UpdateWorldHealthBar(healthPercentage);
            
            yield return null;
        }

        _isAnimating = false;
        UpdateHealthDisplay(); // Ensure final values are correct
    }

    private void StartHealthFlash(Color flashColor)
    {
        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
        }
        _flashCoroutine = StartCoroutine(HealthFlashCoroutine(flashColor));
    }

    private IEnumerator HealthFlashCoroutine(Color flashColor)
    {
        if (HealthFillImage == null) yield break;

        Color originalColor = HealthFillImage.color;
        float elapsed = 0f;

        // Flash to flash color
        while (elapsed < FlashDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (FlashDuration / 2f);
            HealthFillImage.color = Color.Lerp(originalColor, flashColor, t);
            yield return null;
        }

        elapsed = 0f;
        // Flash back to original color
        while (elapsed < FlashDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (FlashDuration / 2f);
            HealthFillImage.color = Color.Lerp(flashColor, originalColor, t);
            yield return null;
        }

        HealthFillImage.color = originalColor;
    }

    private void HandleLowHealthWarning()
    {
        if (!EnableLowHealthWarning) return;

        float healthPercentage = _maxDisplayedHealth > 0 ? _currentDisplayedHealth / _maxDisplayedHealth : 1f;

        if (healthPercentage <= LowHealthThreshold)
        {
            if (_pulseCoroutine == null)
            {
                _pulseCoroutine = StartCoroutine(LowHealthPulse());
            }
        }
        else
        {
            if (_pulseCoroutine != null)
            {
                StopCoroutine(_pulseCoroutine);
                _pulseCoroutine = null;
            }
        }
    }

    private IEnumerator LowHealthPulse()
    {
        while (true)
        {
            if (HealthBarContainer != null)
            {
                float pulse = (Mathf.Sin(Time.time * PulseSpeed) + 1f) * 0.5f;
                float alpha = Mathf.Lerp(0.7f, 1f, pulse);
                
                var canvasGroup = HealthBarContainer.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = HealthBarContainer.AddComponent<CanvasGroup>();
                }
                canvasGroup.alpha = alpha;
            }
            yield return null;
        }
    }

    public void SetShowAbovePlayer(bool show)
    {
        ShowAbovePlayer = show;
        if (show && _worldHealthBar == null)
        {
            InitializeWorldHealthBar();
        }
        else if (!show && _worldHealthBar != null)
        {
            Destroy(_worldHealthBar);
            _worldHealthBar = null;
        }
    }

    public void SetWorldOffset(Vector3 offset)
    {
        WorldOffset = offset;
        if (_worldHealthBar != null)
        {
            _worldHealthBar.transform.localPosition = offset;
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        ClientPlayerStats.OnHealthChanged -= OnHealthChanged;
        ClientPlayerStats.OnStatsUpdated -= OnStatsUpdated;

        // Stop all coroutines
        if (_animationCoroutine != null)
        {
            StopCoroutine(_animationCoroutine);
        }
        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
        }
        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
        }

        // Clean up world health bar
        if (_worldHealthBar != null)
        {
            Destroy(_worldHealthBar);
        }
    }
}