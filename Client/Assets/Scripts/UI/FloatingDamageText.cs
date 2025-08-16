using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Individual floating damage text component that handles animation and lifecycle
/// Designed for enemy damage display with color coding and smooth animations
/// </summary>
public class FloatingDamageText : MonoBehaviour
{
    [Header("Animation Settings")]
    public float Duration = 2f;
    public float FloatDistance = 50f;
    public AnimationCurve MovementCurve = new AnimationCurve(new Keyframe(0, 0, 0, 2), new Keyframe(1, 1, 0, 0));
    public AnimationCurve FadeCurve = AnimationCurve.Linear(0, 1, 1, 0);
    public AnimationCurve ScaleCurve = new AnimationCurve(new Keyframe(0, 1.2f), new Keyframe(1, 1));
    
    [Header("Visual Settings")]
    public float FontSize = 24f;
    public FontStyle FontStyle = FontStyle.Bold;
    public Color DefaultColor = Color.red;
    
    // Components
    private Text _textComponent;
    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;
    
    // Animation state
    private Vector3 _startPosition;
    private Vector3 _endPosition;
    private Vector3 _startScale;
    private bool _isAnimating = false;
    
    private void Awake()
    {
        InitializeComponents();
    }
    
    private void InitializeComponents()
    {
        // Get or create Text component
        _textComponent = GetComponent<Text>();
        if (_textComponent == null)
        {
            _textComponent = gameObject.AddComponent<Text>();
        }
        
        // Configure text appearance
        _textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _textComponent.fontSize = (int)FontSize;
        _textComponent.fontStyle = FontStyle;
        _textComponent.alignment = TextAnchor.MiddleCenter;
        _textComponent.color = DefaultColor;
        
        // Get RectTransform
        _rectTransform = GetComponent<RectTransform>();
        if (_rectTransform == null)
        {
            _rectTransform = gameObject.AddComponent<RectTransform>();
        }
        
        // Get or create CanvasGroup for alpha animation
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        
        // Store initial scale
        _startScale = transform.localScale;
    }
    
    /// <summary>
    /// Initialize and start the floating damage animation
    /// </summary>
    /// <param name="damage">Damage amount to display</param>
    /// <param name="damageType">Type of damage for color coding</param>
    /// <param name="worldPosition">World position to start from</param>
    public void Initialize(float damage, DamageType damageType, Vector3 worldPosition)
    {
        if (_isAnimating)
        {
            Debug.LogWarning("[FloatingDamageText] Already animating, skipping new initialization");
            return;
        }
        
        // Set text content
        string damageText = FormatDamageText(damage, damageType);
        _textComponent.text = damageText;
        
        // Set color based on damage type
        _textComponent.color = GetDamageColor(damageType);
        
        // Convert world position to screen position
        Vector3 screenPosition = ConvertWorldToScreenPosition(worldPosition);
        
        // Set initial position
        _rectTransform.position = screenPosition;
        _startPosition = screenPosition;
        _endPosition = screenPosition + Vector3.up * FloatDistance;
        
        // Reset animation state
        _canvasGroup.alpha = 1f;
        transform.localScale = _startScale;
        
        // Start animation
        StartCoroutine(AnimateFloatingText());
    }
    
    /// <summary>
    /// Format damage text based on type
    /// </summary>
    private string FormatDamageText(float damage, DamageType damageType)
    {
        switch (damageType)
        {
            case DamageType.Healing:
                return $"+{damage:F0}";
            case DamageType.Critical:
                return $"-{damage:F0}!";
            case DamageType.Regular:
            default:
                return $"-{damage:F0}";
        }
    }
    
    /// <summary>
    /// Get color based on damage type
    /// </summary>
    private Color GetDamageColor(DamageType damageType)
    {
        switch (damageType)
        {
            case DamageType.Healing:
                return Color.green;
            case DamageType.Critical:
                return Color.red;
            case DamageType.Regular:
            default:
                return new Color(1f, 0.5f, 0f); // Orange color for better visibility
        }
    }
    
    /// <summary>
    /// Convert world position to screen position for UI display
    /// </summary>
    private Vector3 ConvertWorldToScreenPosition(Vector3 worldPosition)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("[FloatingDamageText] No main camera found, using world position directly");
            return worldPosition;
        }
        
        return mainCamera.WorldToScreenPoint(worldPosition);
    }
    
    /// <summary>
    /// Main animation coroutine
    /// </summary>
    private IEnumerator AnimateFloatingText()
    {
        _isAnimating = true;
        float elapsed = 0f;
        
        while (elapsed < Duration)
        {
            float t = elapsed / Duration;
            
            // Animate position using curve
            float positionT = MovementCurve.Evaluate(t);
            _rectTransform.position = Vector3.Lerp(_startPosition, _endPosition, positionT);
            
            // Animate alpha using curve
            float fadeT = FadeCurve.Evaluate(t);
            _canvasGroup.alpha = fadeT;
            
            // Animate scale using curve
            float scaleT = ScaleCurve.Evaluate(t);
            transform.localScale = _startScale * scaleT;
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Ensure final state
        _canvasGroup.alpha = 0f;
        _isAnimating = false;
        
        // Return to pool or destroy
        ReturnToPool();
    }
    
    /// <summary>
    /// Return this object to the pool or destroy it
    /// </summary>
    private void ReturnToPool()
    {
        // Try to return to pool first
        var damageTextManager = FindObjectOfType<EnemyDamageTextManager>();
        if (damageTextManager != null)
        {
            damageTextManager.ReturnToPool(this);
        }
        else
        {
            // No pool available, destroy
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Reset this component for reuse in object pool
    /// </summary>
    public void ResetForReuse()
    {
        _isAnimating = false;
        _canvasGroup.alpha = 1f;
        transform.localScale = _startScale;
        _textComponent.text = "";
        _textComponent.color = DefaultColor;
        gameObject.SetActive(false);
    }
    
    /// <summary>
    /// Check if this text is currently animating
    /// </summary>
    public bool IsAnimating => _isAnimating;
}

/// <summary>
/// Enum for different types of damage for color coding
/// </summary>
public enum DamageType
{
    Regular,    // Orange
    Critical,   // Red
    Healing     // Green
}