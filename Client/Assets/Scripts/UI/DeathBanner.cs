using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Death banner that displays when player dies with respawn button
/// Shows "YOU ARE DEAD!" with fade in/out animation and respawn functionality
/// </summary>
public class DeathBanner : MonoBehaviour
{
    [Header("Animation Settings")]
    public float DisplayDuration = 0f; // Stay visible until respawn
    public float FadeInDuration = 0.5f;
    public float FadeOutDuration = 0.5f;
    public AnimationCurve FadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public AnimationCurve ScaleCurve = new AnimationCurve(
        new Keyframe(0f, 0.5f), 
        new Keyframe(0.3f, 1.2f), 
        new Keyframe(1f, 1f)
    );
    
    [Header("Visual Settings")]
    public string BannerText = "YOU ARE DEAD!";
    public Color BannerColor = Color.red;
    public int FontSize = 48;
    public FontStyle FontStyle = FontStyle.Bold;
    public string RespawnButtonText = "RESPAWN";
    
    [Header("Sound Settings")]
    public bool PlaySoundEffect = true;
    public AudioClip DeathSound;
    public float SoundVolume = 0.8f;
    
    // Components
    private Text _bannerText;
    private Button _respawnButton;
    private CanvasGroup _canvasGroup;
    private RectTransform _rectTransform;
    private AudioSource _audioSource;
    
    // Animation state
    private bool _isAnimating = false;
    private bool _isVisible = false;
    private Vector3 _originalScale;
    
    private void Awake()
    {
        InitializeComponents();
        
        // Subscribe to ClientPlayerStats death events
        ClientPlayerStats.OnPlayerDeath += HandlePlayerDeath;
        
        // Subscribe to NetworkManager respawn response
        NetworkManager.OnRespawnResponse += HandleRespawnResponse;
        
        Debug.Log("[DeathBanner] Subscribed to death and respawn events in Awake()");
        
        // Start hidden
        gameObject.SetActive(false);
    }
    
    private void Start()
    {
        Debug.Log("[DeathBanner] Start() called - verifying event subscriptions");
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        ClientPlayerStats.OnPlayerDeath -= HandlePlayerDeath;
        NetworkManager.OnRespawnResponse -= HandleRespawnResponse;
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
        _rectTransform.sizeDelta = new Vector2(800, 200);
        
        // Store original scale
        _originalScale = _rectTransform.localScale;
        
        // Get or create CanvasGroup for alpha animation
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
        
        // Create background image
        var backgroundImage = GetComponent<Image>();
        if (backgroundImage == null)
        {
            backgroundImage = gameObject.AddComponent<Image>();
        }
        backgroundImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f); // Dark background
        
        // Create child GameObject for banner text
        GameObject textObj = new GameObject("BannerText");
        textObj.transform.SetParent(transform, false);
        
        _bannerText = textObj.AddComponent<Text>();
        
        // Configure banner text appearance
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            _bannerText.font = font;
        }
        else
        {
            Debug.LogWarning("[DeathBanner] Could not load LegacyRuntime.ttf font, using default");
            // Try alternative font
            _bannerText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        _bannerText.fontSize = FontSize;
        _bannerText.fontStyle = FontStyle;
        _bannerText.alignment = TextAnchor.MiddleCenter;
        _bannerText.color = BannerColor;
        _bannerText.text = BannerText;
        
        // Position text in the upper portion of the banner
        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0.5f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;
        
        // Add text outline for better visibility
        var outline = textObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(3f, 3f);
        
        // Create respawn button
        CreateRespawnButton();
        
        // Get or create AudioSource for sound effects
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _audioSource.clip = DeathSound;
        _audioSource.volume = SoundVolume;
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
        
        Debug.Log("[DeathBanner] Components initialized");
    }
    
    private void CreateRespawnButton()
    {
        // Create button GameObject
        GameObject buttonObj = new GameObject("RespawnButton");
        buttonObj.transform.SetParent(transform, false);
        
        // Set button position (below the death text)
        var buttonRect = buttonObj.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.3f, 0.1f);
        buttonRect.anchorMax = new Vector2(0.7f, 0.35f);
        buttonRect.anchoredPosition = Vector2.zero;
        buttonRect.sizeDelta = Vector2.zero;
        
        // Add button component
        _respawnButton = buttonObj.AddComponent<Button>();
        
        // Add button background image
        var buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.6f, 0.2f, 0.2f, 0.9f); // Dark red button
        _respawnButton.targetGraphic = buttonImage;
        
        // Create button text
        GameObject buttonTextObj = new GameObject("ButtonText");
        buttonTextObj.transform.SetParent(buttonObj.transform, false);
        
        var buttonText = buttonTextObj.AddComponent<Text>();
        buttonText.text = RespawnButtonText;
        var buttonFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (buttonFont != null)
        {
            buttonText.font = buttonFont;
        }
        else
        {
            Debug.LogWarning("[DeathBanner] Could not load LegacyRuntime.ttf font for button, using default");
            buttonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        buttonText.fontSize = 24;
        buttonText.color = Color.white;
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.fontStyle = FontStyle.Bold;
        
        var buttonTextRect = buttonTextObj.GetComponent<RectTransform>();
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.anchoredPosition = Vector2.zero;
        buttonTextRect.sizeDelta = Vector2.zero;
        
        // Add button click handler
        _respawnButton.onClick.AddListener(OnRespawnButtonClicked);
        
        Debug.Log($"[DeathBanner] Respawn button created and configured. Button interactable: {_respawnButton.interactable}");
        Debug.Log($"[DeathBanner] Button position: {buttonRect.anchoredPosition}, size: {buttonRect.sizeDelta}");
    }
    
    /// <summary>
    /// Handle player death event from ClientPlayerStats
    /// </summary>
    private void HandlePlayerDeath()
    {
        Debug.Log("[DeathBanner] HandlePlayerDeath called - showing death banner");
        ShowDeathBanner();
    }
    
    /// <summary>
    /// Handle respawn response from server
    /// </summary>
    private void HandleRespawnResponse(NetworkMessages.RespawnResponseMessage respawnMsg)
    {
        Debug.Log($"[DeathBanner] HandleRespawnResponse called - Success: {respawnMsg.Success}");
        
        if (respawnMsg.Success)
        {
            // Hide the death banner
            HideDeathBanner();
        }
        else
        {
            // Show error message and re-enable button for retry
            Debug.LogWarning($"[DeathBanner] Respawn failed: {respawnMsg.ErrorMessage}");
            if (_respawnButton != null)
            {
                _respawnButton.interactable = true;
            }
        }
    }
    
    /// <summary>
    /// Show the death banner with animation
    /// </summary>
    public void ShowDeathBanner()
    {
        if (_isVisible)
        {
            Debug.LogWarning("[DeathBanner] Banner is already visible, skipping new death");
            return;
        }
        
        // Play sound effect
        if (PlaySoundEffect && _audioSource != null && DeathSound != null)
        {
            _audioSource.Play();
            Debug.Log("[DeathBanner] Playing death sound");
        }
        
        // Show and animate
        gameObject.SetActive(true);
        _isVisible = true;
        
        // Ensure raycasts work and button is interactable (backup in case animation fails)
        if (_canvasGroup != null)
        {
            _canvasGroup.blocksRaycasts = true;
        }
        if (_respawnButton != null)
        {
            _respawnButton.interactable = true;
        }
        
        StartCoroutine(AnimateIn());
        
        Debug.Log("[DeathBanner] Showing death banner");
    }
    
    /// <summary>
    /// Hide the death banner with animation
    /// </summary>
    public void HideDeathBanner()
    {
        if (!_isVisible)
        {
            Debug.LogWarning("[DeathBanner] Banner is not visible, skipping hide");
            return;
        }
        
        _isVisible = false;
        StartCoroutine(AnimateOut());
        
        Debug.Log("[DeathBanner] Hiding death banner");
    }
    
    /// <summary>
    /// Handle respawn button click
    /// </summary>
    private void OnRespawnButtonClicked()
    {
        Debug.Log("[DeathBanner] Respawn button clicked - sending respawn request");
        
        // Disable button to prevent multiple clicks
        _respawnButton.interactable = false;
        
        // Try multiple ways to find NetworkManager
        var networkManager = GameManager.Instance?.NetworkManager ?? FindObjectOfType<NetworkManager>();
        
        if (networkManager == null)
        {
            Debug.LogError("[DeathBanner] NetworkManager not found in GameManager or scene - cannot send respawn request");
            _respawnButton.interactable = true; // Re-enable button
            return;
        }
        
        if (!networkManager.IsConnected)
        {
            Debug.LogError("[DeathBanner] NetworkManager not connected - cannot send respawn request");
            _respawnButton.interactable = true; // Re-enable button
            return;
        }
        
        Debug.Log($"[DeathBanner] Found NetworkManager, IsConnected: {networkManager.IsConnected}, ConnectionId: {networkManager.GetPlayerId()}");
        
        // Send respawn request to server
        try
        {
            networkManager.SendRespawnRequest();
            Debug.Log("[DeathBanner] Respawn request sent successfully");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DeathBanner] Failed to send respawn request: {ex.Message}");
            _respawnButton.interactable = true; // Re-enable button
        }
    }
    
    /// <summary>
    /// Fade in animation coroutine
    /// </summary>
    private IEnumerator AnimateIn()
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
        _canvasGroup.blocksRaycasts = true;
        _rectTransform.localScale = _originalScale * ScaleCurve.Evaluate(1f);
        
        // Re-enable respawn button
        _respawnButton.interactable = true;
        
        _isAnimating = false;
        
        Debug.Log("[DeathBanner] Fade in animation completed");
    }
    
    /// <summary>
    /// Fade out animation coroutine
    /// </summary>
    private IEnumerator AnimateOut()
    {
        _isAnimating = true;
        
        // Fade out animation
        float elapsed = 0f;
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
        _canvasGroup.blocksRaycasts = false;
        
        // Hide the GameObject
        gameObject.SetActive(false);
        _isAnimating = false;
        
        Debug.Log("[DeathBanner] Fade out animation completed");
    }
    
    /// <summary>
    /// Test method to trigger death banner (for testing purposes)
    /// </summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Always)]
    public void TestDeathBanner()
    {
        Debug.Log("[DeathBanner] TestDeathBanner called - manually showing death banner");
        ShowDeathBanner();
    }

    /// <summary>
    /// Force trigger the death banner (for debugging sync issues)
    /// </summary>
    public void ForceShowDeathBanner()
    {
        Debug.Log("[DeathBanner] ForceShowDeathBanner called - manually triggering death event");
        HandlePlayerDeath();
    }
    
    /// <summary>
    /// Check if the banner is currently visible
    /// </summary>
    public bool IsVisible => _isVisible;
    
    /// <summary>
    /// Check if the banner is currently animating
    /// </summary>
    public bool IsAnimating => _isAnimating;
    
    /// <summary>
    /// Update banner settings at runtime
    /// </summary>
    public void UpdateBannerSettings(Color? color = null, int? fontSize = null, string bannerText = null)
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
        
        if (!string.IsNullOrEmpty(bannerText))
        {
            BannerText = bannerText;
            if (_bannerText != null)
                _bannerText.text = BannerText;
        }
        
        Debug.Log($"[DeathBanner] Updated banner settings - Color: {BannerColor}, FontSize: {FontSize}, Text: {BannerText}");
    }
}