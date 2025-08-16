using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space health bar that displays above enemies
/// Automatically follows enemy position and updates based on health changes
/// </summary>
public class EnemyHealthBar : MonoBehaviour
{
    [Header("UI References")]
    public Canvas HealthBarCanvas;
    public Slider HealthSlider;
    public Text HealthText;
    public Image HealthFillImage;
    public Image BackgroundImage;

    [Header("Settings")]
    public Vector3 WorldOffset = new Vector3(0, 3f, 0);
    public bool ShowHealthText = true;
    public bool ShowWhenFullHealth = true;
    public float FadeOutDelay = 3f;
    public float FadeSpeed = 2f;

    [Header("Colors")]
    public Color FullHealthColor = Color.green;
    public Color MidHealthColor = Color.yellow;
    public Color LowHealthColor = Color.red;
    public Color BackgroundColor = new Color(0, 0, 0, 0.5f);

    private EnemyBase _targetEnemy;
    private Camera _mainCamera;
    private CanvasGroup _canvasGroup;
    private float _lastDamageTime;
    private bool _isVisible = true;

    private void Awake()
    {
        // Get or create canvas group for fading
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // Find main camera
        _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            _mainCamera = FindObjectOfType<Camera>();
        }

        SetupUI();
    }

    private void SetupUI()
    {
        // Setup canvas for world space rendering
        if (HealthBarCanvas != null)
        {
            HealthBarCanvas.renderMode = RenderMode.WorldSpace;
            HealthBarCanvas.worldCamera = _mainCamera;
            HealthBarCanvas.sortingOrder = 100; // High sorting order for visibility
            
            // Scale the canvas appropriately - don't override the AutoSceneSetup values
            var rectTransform = HealthBarCanvas.GetComponent<RectTransform>();
            if (rectTransform != null && rectTransform.sizeDelta == Vector2.zero)
            {
                // Only set if not already set by AutoSceneSetup
                rectTransform.sizeDelta = new Vector2(500f, 100f);
                HealthBarCanvas.transform.localScale = Vector3.one * 0.1f;
            }
            
            // Canvas setup complete
        }

        // Setup health slider
        if (HealthSlider != null)
        {
            HealthSlider.minValue = 0f;
            HealthSlider.maxValue = 1f;
            HealthSlider.value = 1f;
            
            Debug.Log($"[EnemyHealthBar] Slider setup - Min: {HealthSlider.minValue}, Max: {HealthSlider.maxValue}, Value: {HealthSlider.value}");
            Debug.Log($"[EnemyHealthBar] Slider fillRect: {HealthSlider.fillRect != null}, targetGraphic: {HealthSlider.targetGraphic != null}");
            
            if (HealthFillImage != null)
            {
                Debug.Log($"[EnemyHealthBar] FillImage type: {HealthFillImage.type}, fillMethod: {HealthFillImage.fillMethod}, fillAmount: {HealthFillImage.fillAmount}");
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

        // Hide health text if disabled
        if (HealthText != null && !ShowHealthText)
        {
            HealthText.gameObject.SetActive(false);
        }
    }

    public void Initialize(EnemyBase enemy)
    {
        // Clear previous enemy assignment to prevent cross-wiring
        if (_targetEnemy != null)
        {
            _targetEnemy.OnHealthChanged -= OnHealthChanged;
            _targetEnemy.OnEnemyDeath -= OnEnemyDeath;
            Debug.Log($"[EnemyHealthBar] Cleared previous assignment to {_targetEnemy.EnemyName}");
        }
        
        _targetEnemy = enemy;
        
        if (_targetEnemy != null)
        {
            // Subscribe to health changes
            _targetEnemy.OnHealthChanged += OnHealthChanged;
            _targetEnemy.OnEnemyDeath += OnEnemyDeath;
            
            // Initial health update
            float currentHealth = _targetEnemy.GetHealthPercentage() * 100f;
            OnHealthChanged(currentHealth, 100f);
            
            // Force visibility for debugging
            SetVisibility(true);
            
            Debug.Log($"[EnemyHealthBar] Initialized for enemy: {_targetEnemy.EnemyName} (ID: {_targetEnemy.GetInstanceID()}) at {_targetEnemy.transform.position}");
        }
    }

    private void Update()
    {
        if (_targetEnemy == null || _mainCamera == null) return;

        // Update position to follow enemy
        UpdatePosition();
        
        // Update rotation to face camera
        UpdateRotation();
        
        // Handle auto-fade
        HandleAutoFade();
        
        // Debug logging removed - health bars are working!
    }

    private void UpdatePosition()
    {
        Vector3 worldPosition = _targetEnemy.transform.position + WorldOffset;
        transform.position = worldPosition;
    }

    private void UpdateRotation()
    {
        // Always face the camera
        Vector3 directionToCamera = _mainCamera.transform.position - transform.position;
        transform.rotation = Quaternion.LookRotation(directionToCamera);
    }

    private void HandleAutoFade()
    {
        if (!ShowWhenFullHealth && _targetEnemy.GetHealthPercentage() >= 1f)
        {
            // Check if we should start fading
            if (Time.time - _lastDamageTime > FadeOutDelay && _isVisible)
            {
                StartFadeOut();
            }
        }
        else if (!_isVisible)
        {
            StartFadeIn();
        }
    }

    private void StartFadeOut()
    {
        _isVisible = false;
        StartCoroutine(FadeCanvasGroup(0f));
    }

    private void StartFadeIn()
    {
        _isVisible = true;
        StartCoroutine(FadeCanvasGroup(1f));
    }

    private System.Collections.IEnumerator FadeCanvasGroup(float targetAlpha)
    {
        float startAlpha = _canvasGroup.alpha;
        float elapsed = 0f;
        float duration = 1f / FadeSpeed;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        _canvasGroup.alpha = targetAlpha;
    }

    private void OnHealthChanged(float currentHealth, float maxHealth)
    {
        if (HealthSlider == null) 
        {
            Debug.LogError($"[EnemyHealthBar] HealthSlider is null for {_targetEnemy?.EnemyName}!");
            return;
        }

        if (_targetEnemy == null)
        {
            Debug.LogError("[EnemyHealthBar] OnHealthChanged called but _targetEnemy is null!");
            return;
        }

        float healthPercentage = maxHealth > 0 ? currentHealth / maxHealth : 0f;
        
        // Update the slider value - this should automatically control the fill area
        float oldValue = HealthSlider.value;
        HealthSlider.value = healthPercentage;
        
        Debug.Log($"[EnemyHealthBar] {_targetEnemy.EnemyName} (ID: {_targetEnemy.GetInstanceID()}) health changed: {currentHealth:F1}/{maxHealth:F1} = {healthPercentage:P1}, Slider value: {oldValue:F3} -> {HealthSlider.value:F3}");

        // Update health text
        if (HealthText != null && ShowHealthText)
        {
            HealthText.text = $"{currentHealth:F0}/{maxHealth:F0}";
        }

        // Update health bar color based on percentage
        UpdateHealthBarColor(healthPercentage);

        // Record damage time for auto-fade
        if (healthPercentage < 1f)
        {
            _lastDamageTime = Time.time;
        }

        // Show health bar when damage is taken
        if (!_isVisible && healthPercentage < 1f)
        {
            StartFadeIn();
        }
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
    }

    private void OnEnemyDeath(EnemyBase enemy)
    {
        // Hide health bar when enemy dies
        _canvasGroup.alpha = 0f;
        _isVisible = false;
        
        Debug.Log($"[EnemyHealthBar] Enemy {enemy.EnemyName} died, hiding health bar");
    }

    public void SetVisibility(bool visible)
    {
        _isVisible = visible;
        _canvasGroup.alpha = visible ? 1f : 0f;
    }

    public void SetWorldOffset(Vector3 offset)
    {
        WorldOffset = offset;
    }

    public void SetShowHealthText(bool show)
    {
        ShowHealthText = show;
        if (HealthText != null)
        {
            HealthText.gameObject.SetActive(show);
        }
    }

    public void ResetHealthBar()
    {
        // Unsubscribe from current enemy events
        if (_targetEnemy != null)
        {
            _targetEnemy.OnHealthChanged -= OnHealthChanged;
            _targetEnemy.OnEnemyDeath -= OnEnemyDeath;
            Debug.Log($"[EnemyHealthBar] Reset health bar, unsubscribed from {_targetEnemy.EnemyName}");
        }
        
        // Clear target enemy
        _targetEnemy = null;
        
        // Reset UI state
        if (HealthSlider != null)
        {
            HealthSlider.value = 1f;
        }
        
        if (HealthFillImage != null)
        {
            HealthFillImage.color = FullHealthColor;
        }
        
        if (HealthText != null && ShowHealthText)
        {
            HealthText.text = "100/100";
        }
        
        // Reset visibility
        _isVisible = true;
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
        }
        
        Debug.Log("[EnemyHealthBar] Health bar reset to default state");
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        ResetHealthBar();
    }

    // Debug visualization
    private void OnDrawGizmosSelected()
    {
        if (_targetEnemy != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(_targetEnemy.transform.position + WorldOffset, new Vector3(2f, 0.3f, 0.1f));
        }
    }
}