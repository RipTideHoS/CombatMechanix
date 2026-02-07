using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI component that displays level completion stats and a continue button
/// Shows when all enemies are defeated, hides when player clicks continue
/// </summary>
public class LevelCompleteUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panel;
    public Text titleText;
    public Text killsText;
    public Text experienceText;
    public Text damageText;
    public Text timeText;
    public Button continueButton;
    public Text continueButtonText;

    private int _nextLevel;
    private NetworkManager _networkManager;

    private void Awake()
    {
        // Hide panel on start
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    private void Start()
    {
        _networkManager = FindObjectOfType<NetworkManager>();

        // Subscribe to level complete event
        NetworkManager.OnLevelComplete += HandleLevelComplete;

        // Set up continue button
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueClicked);
        }

        Debug.Log("[LevelCompleteUI] Initialized and subscribed to OnLevelComplete");
    }

    private void OnDestroy()
    {
        NetworkManager.OnLevelComplete -= HandleLevelComplete;

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(OnContinueClicked);
        }
    }

    private void HandleLevelComplete(LevelCompleteMessage message)
    {
        Debug.Log($"[LevelCompleteUI] Received level complete: Level {message.completedLevel}");

        _nextLevel = message.nextLevel;

        // Update UI text
        if (titleText != null)
        {
            titleText.text = $"LEVEL {message.completedLevel} COMPLETE!";
        }

        if (killsText != null)
        {
            killsText.text = $"Enemies Killed: {message.enemiesKilled}";
        }

        if (experienceText != null)
        {
            experienceText.text = $"Experience Earned: {message.experienceEarned}";
        }

        if (damageText != null)
        {
            damageText.text = $"Damage Dealt: {message.damageDealt:F0}";
        }

        if (timeText != null)
        {
            int minutes = (int)(message.timeTaken / 60);
            int seconds = (int)(message.timeTaken % 60);
            timeText.text = $"Time: {minutes}:{seconds:D2}";
        }

        if (continueButtonText != null)
        {
            continueButtonText.text = $"Continue to Level {message.nextLevel}";
        }

        // Show the panel
        if (panel != null)
        {
            panel.SetActive(true);
        }

        // Optionally pause the game or disable player controls
        Time.timeScale = 0.1f; // Slow down but don't fully pause (so UI still works)

        Debug.Log("[LevelCompleteUI] Panel shown");
    }

    private async void OnContinueClicked()
    {
        Debug.Log($"[LevelCompleteUI] Continue clicked, proceeding to level {_nextLevel}");

        // Hide the panel
        if (panel != null)
        {
            panel.SetActive(false);
        }

        // Resume normal time
        Time.timeScale = 1f;

        // Send continue message to server
        if (_networkManager != null)
        {
            await _networkManager.SendLevelContinue(_nextLevel);
        }
        else
        {
            Debug.LogError("[LevelCompleteUI] NetworkManager not found!");
        }
    }

    /// <summary>
    /// Create the UI programmatically (called from AutoSceneSetup)
    /// </summary>
    public static LevelCompleteUI CreateUI(Transform canvasTransform)
    {
        // Create main panel
        GameObject panelObj = new GameObject("LevelCompletePanel");
        panelObj.transform.SetParent(canvasTransform, false);

        // Add RectTransform
        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(500, 400);

        // Add background image
        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0.1f, 0.1f, 0.2f, 0.95f);

        // Create component and assign panel
        LevelCompleteUI ui = panelObj.AddComponent<LevelCompleteUI>();
        ui.panel = panelObj;

        // Title text
        ui.titleText = CreateText(panelObj.transform, "TitleText", "LEVEL COMPLETE!",
            new Vector2(0, 150), 36, Color.yellow, FontStyle.Bold);

        // Stats texts
        ui.killsText = CreateText(panelObj.transform, "KillsText", "Enemies Killed: 0",
            new Vector2(0, 80), 24, Color.white, FontStyle.Normal);

        ui.experienceText = CreateText(panelObj.transform, "ExperienceText", "Experience Earned: 0",
            new Vector2(0, 40), 24, Color.cyan, FontStyle.Normal);

        ui.damageText = CreateText(panelObj.transform, "DamageText", "Damage Dealt: 0",
            new Vector2(0, 0), 24, Color.white, FontStyle.Normal);

        ui.timeText = CreateText(panelObj.transform, "TimeText", "Time: 0:00",
            new Vector2(0, -40), 24, Color.white, FontStyle.Normal);

        // Continue button
        GameObject buttonObj = new GameObject("ContinueButton");
        buttonObj.transform.SetParent(panelObj.transform, false);

        RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(0, -120);
        buttonRect.sizeDelta = new Vector2(300, 60);

        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.6f, 0.2f, 1f);

        ui.continueButton = buttonObj.AddComponent<Button>();
        ui.continueButton.targetGraphic = buttonImage;

        // Button hover colors
        ColorBlock colors = ui.continueButton.colors;
        colors.normalColor = new Color(0.2f, 0.6f, 0.2f, 1f);
        colors.highlightedColor = new Color(0.3f, 0.8f, 0.3f, 1f);
        colors.pressedColor = new Color(0.15f, 0.4f, 0.15f, 1f);
        ui.continueButton.colors = colors;

        // Button text
        ui.continueButtonText = CreateText(buttonObj.transform, "ButtonText", "Continue to Next Level",
            Vector2.zero, 24, Color.white, FontStyle.Bold);
        ui.continueButtonText.alignment = TextAnchor.MiddleCenter;

        // Hide panel initially
        panelObj.SetActive(false);

        Debug.Log("[LevelCompleteUI] Created UI programmatically");

        return ui;
    }

    private static Text CreateText(Transform parent, string name, string content,
        Vector2 position, int fontSize, Color color, FontStyle style)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);

        RectTransform rect = textObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(450, 40);

        Text text = textObj.AddComponent<Text>();
        text.text = content;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.color = color;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;

        return text;
    }
}
