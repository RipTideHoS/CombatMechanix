using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class UIManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject LoginPanel;
    public GameObject GameUI;
    public GameObject ConnectionLostPanel;
    public GameObject ChatPanel;
    public GameObject InventoryPanel;

    [Header("HUD Elements")]
    public Slider HealthBar;
    public Text HealthText;
    public Text PlayerInfoText;
    public Text NotificationText;

    [Header("Login UI")]
    public InputField PlayerNameInput;
    public Text StatusText;
    public Button ConnectButton;

    [Header("Chat UI")]
    public InputField ChatInput;
    public Text ChatDisplay;
    public Dropdown ChannelDropdown;

    [Header("Floating Damage")]
    public GameObject FloatingDamageText;

    private Queue<string> _chatMessages = new Queue<string>();
    private const int MaxChatMessages = 50;

    private void Start()
    {
        // Subscribe to network events
        NetworkManager.OnChatMessage += HandleChatMessage;
        NetworkManager.OnSystemNotification += HandleSystemNotification;

        // Initialize UI
        ShowLoginPanel();
        SetupChatUI();
    }

    public void ShowLoginPanel()
    {
        if (LoginPanel != null) LoginPanel.SetActive(true);
        if (GameUI != null) GameUI.SetActive(false);
        if (ConnectionLostPanel != null) ConnectionLostPanel.SetActive(false);
    }

    public void ShowGameUI()
    {
        if (LoginPanel != null) LoginPanel.SetActive(false);
        if (GameUI != null) GameUI.SetActive(true);
        if (ConnectionLostPanel != null) ConnectionLostPanel.SetActive(false);
        
        UpdatePlayerInfo();
    }

    public void ShowConnectionLostUI()
    {
        if (ConnectionLostPanel != null) ConnectionLostPanel.SetActive(true);
        if (GameUI != null) GameUI.SetActive(false);
    }

    private void SetupChatUI()
    {
        if (ChatInput != null)
        {
            ChatInput.onEndEdit.AddListener(OnChatInputSubmit);
        }

        if (ChannelDropdown != null)
        {
            ChannelDropdown.options.Clear();
            ChannelDropdown.options.Add(new Dropdown.OptionData("Global"));
            ChannelDropdown.options.Add(new Dropdown.OptionData("Local"));
            ChannelDropdown.options.Add(new Dropdown.OptionData("Private"));
        }
    }

    private void Update()
    {
        // Handle UI input
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (ChatInput != null && !ChatInput.isFocused)
            {
                ChatInput.Select();
                ChatInput.ActivateInputField();
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (ChatInput != null && ChatInput.isFocused)
            {
                ChatInput.DeactivateInputField();
            }
        }

        // Toggle inventory
        if (Input.GetKeyDown(KeyCode.I))
        {
            ToggleInventory();
        }

        // Toggle chat
        if (Input.GetKeyDown(KeyCode.T))
        {
            ToggleChat();
        }
    }

private void OnLoginButtonClicked()
{
    // Get player name from input
    string playerName = PlayerNameInput?.text?.Trim();
    
    if (string.IsNullOrEmpty(playerName))
    {
        if (StatusText != null)
            StatusText.text = "Please enter a player name";
        return;
    }
    
    // Set player name in GameManager
    if (GameManager.Instance != null)
    {
        GameManager.Instance.SetLocalPlayerName(playerName);
    }
    
    // Update status
    if (StatusText != null)
        StatusText.text = "Connecting...";
    
    // Disable button to prevent multiple clicks
    if (ConnectButton != null)
        ConnectButton.interactable = false;
    
    // Start connection
    if (GameManager.Instance?.NetworkManager != null)
    {
        GameManager.Instance.NetworkManager.ConnectToServer();
    }
}


    private void OnChatInputSubmit(string message)
    {
        if (string.IsNullOrEmpty(message.Trim())) return;

        string channel = GetSelectedChannel();
        if (GameManager.Instance.NetworkManager != null)
        {
            _ = GameManager.Instance.NetworkManager.SendChatMessage(message, channel);
        }

        if (ChatInput != null)
        {
            ChatInput.text = "";
            ChatInput.ActivateInputField();
        }
    }

    private string GetSelectedChannel()
    {
        if (ChannelDropdown != null)
        {
            return ChannelDropdown.options[ChannelDropdown.value].text;
        }
        return "Global";
    }

    private void HandleChatMessage(NetworkMessages.ChatMessage chatMessage)
    {
        string formattedMessage = $"[{chatMessage.ChannelType}] {chatMessage.SenderName}: {chatMessage.Message}";
        AddChatMessage(formattedMessage);
    }

    private void HandleSystemNotification(NetworkMessages.SystemNotification notification)
    {
        string formattedMessage = $"[SYSTEM] {notification.Message}";
        AddChatMessage(formattedMessage);
        
        ShowNotification(notification.Message, GetNotificationColor(notification.Priority));
    }

    private void AddChatMessage(string message)
    {
        _chatMessages.Enqueue(message);
        
        while (_chatMessages.Count > MaxChatMessages)
        {
            _chatMessages.Dequeue();
        }

        UpdateChatDisplay();
    }

    private void UpdateChatDisplay()
    {
        if (ChatDisplay != null)
        {
            ChatDisplay.text = string.Join("\n", _chatMessages);
        }
    }

    public void UpdateHealth(float damage)
    {
        // This would be called when local player takes damage
        // Update health bar and text
        if (HealthBar != null)
        {
            // Get current health from game manager or player controller
            float currentHealth = 100f; // Placeholder - get from actual player data
            float maxHealth = 100f;
            
            HealthBar.value = currentHealth / maxHealth;
        }

        if (HealthText != null)
        {
            HealthText.text = $"Health: {100}/{100}"; // Placeholder
        }
    }

    public void UpdatePlayerInfo()
    {
        if (PlayerInfoText != null)
        {
            PlayerInfoText.text = $"Player: {GameManager.Instance.LocalPlayerName}\nLevel: 1\nExperience: 0";
        }
    }

    public void ShowNotification(string message, Color color)
    {
        if (NotificationText != null)
        {
            NotificationText.text = message;
            NotificationText.color = color;
            
            // Auto-hide after 3 seconds
            StartCoroutine(HideNotificationAfterDelay(3f));
        }
    }

    public void ShowFloatingDamage(Vector3 worldPosition, float damage)
    {
        if (FloatingDamageText != null && Camera.main != null)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPosition);
            
            var damageText = Instantiate(FloatingDamageText, transform);
            damageText.transform.position = screenPos;
            var textComponent = damageText.GetComponent<Text>();
            if (textComponent != null)
            {
                textComponent.text = $"-{damage:F0}";
            }
            
            // Animate floating text
            StartCoroutine(AnimateFloatingText(damageText));
        }
    }

    public void ShowMessage(string message)
    {
        ShowNotification(message, Color.white);
    }

    private void ToggleInventory()
    {
        if (InventoryPanel != null)
        {
            InventoryPanel.SetActive(!InventoryPanel.activeSelf);
        }
    }

    private void ToggleChat()
    {
        if (ChatPanel != null)
        {
            ChatPanel.SetActive(!ChatPanel.activeSelf);
        }
    }

    private Color GetNotificationColor(string priority)
    {
        switch (priority?.ToLower())
        {
            case "low": return Color.white;
            case "medium": return Color.yellow;
            case "high": return new Color(1f, 0.5f, 0f); // Orange
            case "critical": return Color.red;
            default: return Color.white;
        }
    }

    private IEnumerator HideNotificationAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (NotificationText != null)
        {
            NotificationText.text = "";
        }
    }

    private IEnumerator AnimateFloatingText(GameObject textObj)
    {
        Vector3 startPos = textObj.transform.position;
        Vector3 endPos = startPos + Vector3.up * 50f;
        
        float duration = 1.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            textObj.transform.position = Vector3.Lerp(startPos, endPos, t);
            
            var text = textObj.GetComponent<Text>();
            if (text != null)
            {
                Color color = text.color;
                color.a = 1f - t;
                text.color = color;
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(textObj);
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        NetworkManager.OnChatMessage -= HandleChatMessage;
        NetworkManager.OnSystemNotification -= HandleSystemNotification;
    }
}