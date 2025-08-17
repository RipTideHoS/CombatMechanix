using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Animated level-up banner that displays when player levels up
/// Shows "LEVEL X ACHIEVED!" with fade in/out animation for 4 seconds
/// </summary>
public class LevelUpBanner : MonoBehaviour
{
    [Header("Animation Settings")]
    public float DisplayDuration = 4f;
    public float FadeInDuration = 0.5f;
    public float FadeOutDuration = 0.5f;
    public AnimationCurve FadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public AnimationCurve ScaleCurve = new AnimationCurve(
        new Keyframe(0f, 0.5f), 
        new Keyframe(0.3f, 1.2f), 
        new Keyframe(1f, 1f)
    );
    
    [Header("Visual Settings")]
    public string BannerTextFormat = "LEVEL {0} ACHIEVED!";
    public Color BannerColor = Color.yellow;
    public int FontSize = 48;
    public FontStyle FontStyle = FontStyle.Bold;
    
    [Header("Sound Settings")]
    public bool PlaySoundEffect = true;
    public AudioClip LevelUpSound;
    public float SoundVolume = 0.8f;
    
    // Components
    private Text _bannerText;
    private CanvasGroup _canvasGroup;
    private RectTransform _rectTransform;
    private AudioSource _audioSource;
    
    // Animation state
    private bool _isAnimating = false;
    private Vector3 _originalScale;
    
    private void Awake()
    {
        InitializeComponents();
        
        // Subscribe to events immediately in Awake() to ensure they're connected
        // even if Start() is delayed or not called
        NetworkManager.OnLevelUp += HandleNetworkLevelUp;
        Debug.Log("[LevelUpBanner] Subscribed to NetworkManager level up events in Awake()");
        
        // Also subscribe to ClientPlayerStats events as fallback
        ClientPlayerStats.OnLevelUp += HandleLevelUp;
        
        Debug.Log("[LevelUpBanner] Subscribed to level up events in Awake()");
        
        // Start hidden
        gameObject.SetActive(false);
    }
    
    private void Start()
    {
        // Verify subscriptions are still active in Start()
        Debug.Log("[LevelUpBanner] Start() called - verifying event subscriptions");
        Debug.Log("[LevelUpBanner] Event subscriptions verified in Start()");
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        NetworkManager.OnLevelUp -= HandleNetworkLevelUp;
        ClientPlayerStats.OnLevelUp -= HandleLevelUp;
    }
    
    private void InitializeComponents()
    {
        // Get or create RectTransform
        _rectTransform = GetComponent<RectTransform>();
        if (_rectTransform == null)
        {
            _rectTransform = gameObject.AddComponent<RectTransform>();
        }
        
        // Set up RectTransform for center screen positioning
        _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _rectTransform.anchoredPosition = Vector2.zero;
        _rectTransform.sizeDelta = new Vector2(600, 100);
        
        // Store original scale
        _originalScale = _rectTransform.localScale;
        
        // Get or create CanvasGroup for alpha animation
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        _canvasGroup.alpha = 0f;
        
        // Get or create Text component
        _bannerText = GetComponent<Text>();
        if (_bannerText == null)
        {
            _bannerText = gameObject.AddComponent<Text>();
        }
        
        // Configure text appearance
        _bannerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _bannerText.fontSize = FontSize;
        _bannerText.fontStyle = FontStyle;
        _bannerText.alignment = TextAnchor.MiddleCenter;
        _bannerText.color = BannerColor;
        _bannerText.text = "";
        
        // Add text outline for better visibility
        var outline = GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, 2f);
        
        // Get or create AudioSource for sound effects
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _audioSource.clip = LevelUpSound;
        _audioSource.volume = SoundVolume;
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
        
        Debug.Log("[LevelUpBanner] Components initialized");
    }
    
    /// <summary>
    /// Handle level up event from ClientPlayerStats
    /// </summary>
    private void HandleLevelUp(int newLevel)
    {
        ShowLevelUpBanner(newLevel);
    }
    
    /// <summary>
    /// Handle level up event from NetworkManager
    /// </summary>
    private void HandleNetworkLevelUp(NetworkMessages.LevelUpMessage levelUpMsg)
    {
        Debug.Log($"[LevelUpBanner] HandleNetworkLevelUp called - Level: {levelUpMsg.NewLevel}");
        ShowLevelUpBanner(levelUpMsg.NewLevel);
    }
    
    /// <summary>
    /// Show the level up banner with animation
    /// </summary>
    public void ShowLevelUpBanner(int level)
    {
        if (_isAnimating)
        {
            Debug.LogWarning("[LevelUpBanner] Banner is already animating, skipping new level up");
            return;
        }
        
        // Set banner text
        _bannerText.text = string.Format(BannerTextFormat, level);
        
        // Play sound effect
        if (PlaySoundEffect)
        {
            // Try to use AudioManager first, fallback to local AudioSource
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayLevelUpSound();
            }
            else if (_audioSource != null && LevelUpSound != null)
            {
                _audioSource.Play();
            }
            Debug.Log($"[LevelUpBanner] Playing level up sound for level {level}");
        }
        
        // Show and animate
        gameObject.SetActive(true);
        StartCoroutine(AnimateBanner());
        
        Debug.Log($"[LevelUpBanner] Showing level up banner for level {level}");
    }
    
    /// <summary>
    /// Main animation coroutine
    /// </summary>
    private IEnumerator AnimateBanner()
    {
        _isAnimating = true;
        
        // Reset initial state
        _canvasGroup.alpha = 0f;
        _rectTransform.localScale = _originalScale * ScaleCurve.Evaluate(0f);
        
        // Fade in and scale animation
        float elapsed = 0f;
        while (elapsed < FadeInDuration)
        {
            float t = elapsed / FadeInDuration;
            
            // Apply fade curve
            float fadeT = FadeCurve.Evaluate(t);
            _canvasGroup.alpha = fadeT;
            
            // Apply scale curve
            float scaleT = ScaleCurve.Evaluate(t);
            _rectTransform.localScale = _originalScale * scaleT;
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Ensure full visibility
        _canvasGroup.alpha = 1f;
        _rectTransform.localScale = _originalScale * ScaleCurve.Evaluate(1f);
        
        // Hold at full visibility
        float holdDuration = DisplayDuration - FadeInDuration - FadeOutDuration;
        if (holdDuration > 0)
        {
            yield return new WaitForSeconds(holdDuration);
        }
        
        // Fade out animation
        elapsed = 0f;
        while (elapsed < FadeOutDuration)
        {
            float t = elapsed / FadeOutDuration;
            
            // Reverse fade curve for fade out
            float fadeT = FadeCurve.Evaluate(1f - t);
            _canvasGroup.alpha = fadeT;
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Ensure fully hidden
        _canvasGroup.alpha = 0f;
        
        // Hide the GameObject
        gameObject.SetActive(false);
        _isAnimating = false;
        
        Debug.Log("[LevelUpBanner] Animation completed");
    }
    
    /// <summary>
    /// Test method to trigger level up banner (for testing purposes)
    /// </summary>
    public void TestLevelUpBanner(int level = 2)
    {
        ShowLevelUpBanner(level);
    }
    
    /// <summary>
    /// Check if the banner is currently animating
    /// </summary>
    public bool IsAnimating => _isAnimating;
    
    /// <summary>
    /// Update banner settings at runtime
    /// </summary>
    public void UpdateBannerSettings(Color? color = null, int? fontSize = null, string textFormat = null)
    {
        if (color.HasValue)
        {
            BannerColor = color.Value;
            if (_bannerText != null)
                _bannerText.color = BannerColor;
        }
        
        if (fontSize.HasValue)
        {
            FontSize = fontSize.Value;
            if (_bannerText != null)
                _bannerText.fontSize = FontSize;
        }
        
        if (!string.IsNullOrEmpty(textFormat))
        {
            BannerTextFormat = textFormat;
        }
        
        Debug.Log($"[LevelUpBanner] Updated banner settings - Color: {BannerColor}, FontSize: {FontSize}, Format: {BannerTextFormat}");
    }
}