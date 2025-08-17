using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Individual floating loot text component
/// Handles animation, lifecycle, and visual effects for loot pickup feedback
/// </summary>
public class FloatingLootText : MonoBehaviour
{
    [Header("Animation Settings")]
    public float FloatDistance = 100f; // Distance to float upward (in screen pixels)
    public AnimationCurve MovementCurve = new AnimationCurve(new Keyframe(0, 0, 0, 2), new Keyframe(1, 1, 0, 0));
    public AnimationCurve FadeCurve = AnimationCurve.Linear(0, 1, 1, 0);
    public AnimationCurve ScaleCurve = new AnimationCurve(new Keyframe(0, 1.2f), new Keyframe(0.2f, 1f), new Keyframe(1, 0.8f));
    
    [Header("Visual Settings")]
    public float FontSize = 22f;
    public FontStyle FontStyle = FontStyle.Bold;
    public Color DefaultColor = Color.white;
    
    // Components
    private Text _textComponent;
    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;
    
    // Animation state
    private Vector3 _startPosition;
    private Vector3 _endPosition;
    private Vector3 _startScale;
    private float _duration;
    private bool _isAnimating = false;
    private LootTextManager _manager;
    
    private void Awake()
    {
        Debug.Log($"[FloatingLootText] *** FLOATING TEXT DEBUG *** Awake called, initializing components");
        InitializeComponents();
        EnsureAnimationCurves();
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
    
    private void EnsureAnimationCurves()
    {
        Debug.Log($"[FloatingLootText] *** FLOATING TEXT DEBUG *** Ensuring animation curves are properly initialized");
        
        // Create default curves if they're null or empty
        if (MovementCurve == null || MovementCurve.keys.Length == 0)
        {
            Debug.Log($"[FloatingLootText] *** FLOATING TEXT DEBUG *** Creating default MovementCurve");
            MovementCurve = new AnimationCurve(new Keyframe(0, 0, 0, 2), new Keyframe(1, 1, 0, 0));
        }
        
        if (FadeCurve == null || FadeCurve.keys.Length == 0)
        {
            Debug.Log($"[FloatingLootText] *** FLOATING TEXT DEBUG *** Creating default FadeCurve");
            FadeCurve = AnimationCurve.Linear(0, 1, 1, 0);
        }
        
        if (ScaleCurve == null || ScaleCurve.keys.Length == 0)
        {
            Debug.Log($"[FloatingLootText] *** FLOATING TEXT DEBUG *** Creating default ScaleCurve");
            ScaleCurve = new AnimationCurve(new Keyframe(0, 1.2f), new Keyframe(0.2f, 1f), new Keyframe(1, 0.8f));
        }
        
        Debug.Log($"[FloatingLootText] *** FLOATING TEXT DEBUG *** Animation curves initialized - Movement keys: {MovementCurve.keys.Length}, Fade keys: {FadeCurve.keys.Length}, Scale keys: {ScaleCurve.keys.Length}");
    }
    
    /// <summary>
    /// Initialize and start the floating loot text animation
    /// </summary>
    /// <param name="text">Text to display</param>
    /// <param name="screenPosition">Starting screen position</param>
    /// <param name="color">Text color</param>
    /// <param name="duration">Animation duration</param>
    /// <param name="manager">Manager to return to when complete</param>
    public void Initialize(string text, Vector3 screenPosition, Color color, float duration, LootTextManager manager)
    {
        Debug.Log($"[FloatingLootText] *** FLOATING TEXT DEBUG *** Initialize called with text: '{text}', duration: {duration}");
        
        if (_isAnimating)
        {
            Debug.LogWarning("[FloatingLootText] Already animating, forcing completion of previous animation");
            ForceComplete();
        }
        
        // Set text content and color
        _textComponent.text = text;
        _textComponent.color = color;
        _duration = duration;
        _manager = manager;
        
        Debug.Log($"[FloatingLootText] *** FLOATING TEXT DEBUG *** Manager assigned: {_manager != null}");
        Debug.Log($"[FloatingLootText] *** FLOATING TEXT DEBUG *** Text component text set to: '{_textComponent.text}'");
        Debug.Log($"[FloatingLootText] *** FLOATING TEXT DEBUG *** Screen position: {screenPosition}");
        
        // Set initial position
        _rectTransform.position = screenPosition;
        _startPosition = screenPosition;
        _endPosition = screenPosition + Vector3.up * FloatDistance;
        
        // Reset animation state
        _canvasGroup.alpha = 1f;
        transform.localScale = _startScale;
        
        Debug.Log($"[FloatingLootText] *** FLOATING TEXT DEBUG *** Starting animation coroutine");
        Debug.Log($"[FloatingLootText] *** FLOATING TEXT DEBUG *** Component state - enabled: {enabled}, gameObject active: {gameObject.activeInHierarchy}");
        Debug.Log($"[FloatingLootText] *** FLOATING TEXT DEBUG *** GameObject name: {gameObject.name}, parent: {transform.parent?.name ?? "NULL"}");
        Debug.Log($"[FloatingLootText] *** FLOATING TEXT DEBUG *** Animation curves - Movement: {MovementCurve != null}, Fade: {FadeCurve != null}, Scale: {ScaleCurve != null}");
        Debug.Log($"[FloatingLootText] *** FLOATING TEXT DEBUG *** Duration: {_duration}, StartPos: {_startPosition}, EndPos: {_endPosition}");
        
        Debug.Log($"[FloatingLootText] *** FLOATING TEXT DEBUG *** Attempting to start coroutine...");
        
        // Start animation
        try
        {
            StartCoroutine(AnimateFloatingText());
            Debug.Log($"[FloatingLootText] *** FLOATING TEXT DEBUG *** Coroutine started successfully!");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[FloatingLootText] *** FLOATING TEXT DEBUG *** Exception starting coroutine: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Main animation coroutine
    /// </summary>
    private IEnumerator AnimateFloatingText()
    {
        Debug.Log($"[FloatingLootText] *** FLOATING TEXT DEBUG *** AnimateFloatingText coroutine started for text: '{_textComponent.text}'");
        
        _isAnimating = true;
        float elapsed = 0f;
        
        while (elapsed < _duration)
        {
            float t = elapsed / _duration;
            
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
        
        Debug.Log($"[FloatingLootText] *** FLOATING TEXT DEBUG *** Animation completed for text: '{_textComponent.text}', calling ReturnToPool");
        
        // Ensure final state
        _canvasGroup.alpha = 0f;
        _isAnimating = false;
        
        // Return to pool
        ReturnToPool();
    }
    
    /// <summary>
    /// Force complete the animation immediately
    /// </summary>
    public void ForceComplete()
    {
        if (_isAnimating)
        {
            StopAllCoroutines();
            _isAnimating = false;
        }
        
        // Set final state
        _canvasGroup.alpha = 0f;
        transform.localScale = _startScale * 0.8f;
        
        // Return to pool
        ReturnToPool();
    }
    
    /// <summary>
    /// Return this object to the pool
    /// </summary>
    private void ReturnToPool()
    {
        Debug.Log($"[FloatingLootText] *** FLOATING TEXT DEBUG *** ReturnToPool called for text: '{_textComponent.text}', Manager available: {_manager != null}");
        
        if (_manager != null)
        {
            Debug.Log($"[FloatingLootText] *** FLOATING TEXT DEBUG *** Calling manager.ReturnToPool");
            _manager.ReturnToPool(this);
        }
        else
        {
            Debug.LogWarning($"[FloatingLootText] *** FLOATING TEXT DEBUG *** No manager available, destroying GameObject");
            // No manager available, destroy
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
        _manager = null;
        gameObject.SetActive(false);
    }
    
    /// <summary>
    /// Check if this text is currently animating
    /// </summary>
    public bool IsAnimating => _isAnimating;
    
    /// <summary>
    /// Get the current text content
    /// </summary>
    public string GetText()
    {
        return _textComponent != null ? _textComponent.text : "";
    }
    
    /// <summary>
    /// Set text color (useful for dynamic color changes)
    /// </summary>
    public void SetColor(Color color)
    {
        if (_textComponent != null)
        {
            _textComponent.color = color;
        }
    }
    
    /// <summary>
    /// Update the animation curves (useful for different types of loot text)
    /// </summary>
    public void SetAnimationCurves(AnimationCurve movement, AnimationCurve fade, AnimationCurve scale)
    {
        MovementCurve = movement;
        FadeCurve = fade;
        ScaleCurve = scale;
    }
}