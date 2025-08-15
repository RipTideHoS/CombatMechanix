using UnityEngine;
using UnityEngine.UI;
using ClientUtilities;
using System.Collections;

/// <summary>
/// Handles the login UI for username/password authentication
/// Supports both new logins and automatic reconnection via session tokens
/// </summary>
public class LoginUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject LoginPanel;
    public InputField UsernameInput;
    public InputField PasswordInput;
    public Button LoginButton;
    public Button ReconnectButton;
    public Text StatusText;
    public Text SessionInfoText;
    public Toggle RememberMeToggle;

    [Header("Settings")]
    public bool ShowPasswordAsText = false;
    
    private bool _isConnecting = false;
    private bool _hasTriedAutoReconnect = false;

    private void Start()
    {
        Debug.Log("LoginUI: Start() called");
        
        // Check if we can find GameManager and NetworkManager
        Debug.Log($"LoginUI: GameManager.Instance = {(GameManager.Instance != null ? "Found" : "NULL")}");
        if (GameManager.Instance != null)
        {
            Debug.Log($"LoginUI: GameManager.NetworkManager = {(GameManager.Instance.NetworkManager != null ? "Found" : "NULL")}");
        }
        
        var nm = FindObjectOfType<NetworkManager>();
        Debug.Log($"LoginUI: FindObjectOfType<NetworkManager>() = {(nm != null ? "Found" : "NULL")}");
        
        SetupUI();
        
        // Subscribe to network events
        NetworkManager.OnConnected += OnConnected;
        NetworkManager.OnDisconnected += OnDisconnected;
        
        // Check for existing session on start
        UpdateSessionInfo();
        
        // Always show login screen initially - disable auto-reconnection for now
        // This ensures users must explicitly authenticate
        Debug.Log("LoginUI: Forcing login screen to be visible");
        ShowLoginPanel();
        
        // Completely disable auto-reconnection for now to prevent connection loops
        Debug.Log("LoginUI: Auto-reconnection disabled to prevent connection loops");
        
        // Future enhancement: Uncomment below for auto-reconnection
        // if (SessionManager.ShouldAttemptReconnection() && !_hasTriedAutoReconnect)
        // {
        //     StartCoroutine(AttemptAutoReconnection());
        // }
    }

    private void SetupUI()
    {
        // Setup password field
        if (PasswordInput != null)
        {
            PasswordInput.contentType = ShowPasswordAsText ? 
                InputField.ContentType.Standard : 
                InputField.ContentType.Password;
        }

        // Setup button listeners
        if (LoginButton != null)
        {
            LoginButton.onClick.AddListener(OnLoginButtonClicked);
        }
        
        if (ReconnectButton != null)
        {
            ReconnectButton.onClick.AddListener(OnReconnectButtonClicked);
        }

        // Pre-fill username if we have it stored
        string storedUsername = SessionManager.GetStoredUsername();
        if (UsernameInput != null && !string.IsNullOrEmpty(storedUsername))
        {
            UsernameInput.text = storedUsername;
        }

        // Set up input field behaviors
        SetupInputFields();
        
        // Update UI state
        UpdateUIState();
    }

    private void SetupInputFields()
    {
        if (UsernameInput != null)
        {
            UsernameInput.onEndEdit.AddListener(OnUsernameEndEdit);
        }
        
        if (PasswordInput != null)
        {
            PasswordInput.onEndEdit.AddListener(OnPasswordEndEdit);
        }
    }

    private void OnUsernameEndEdit(string value)
    {
        // Move to password field when Enter is pressed
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (PasswordInput != null)
            {
                PasswordInput.Select();
                PasswordInput.ActivateInputField();
            }
        }
    }

    private void OnPasswordEndEdit(string value)
    {
        // Login when Enter is pressed in password field
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            OnLoginButtonClicked();
        }
    }

    private void OnLoginButtonClicked()
    {
        if (_isConnecting)
        {
            UpdateStatus("Already connecting...", Color.yellow);
            return;
        }

        string username = UsernameInput?.text?.Trim();
        string password = PasswordInput?.text;

        // Validate input
        if (string.IsNullOrEmpty(username))
        {
            UpdateStatus("Please enter a username", Color.red);
            return;
        }

        if (!PasswordHasher.IsValidUsername(username))
        {
            UpdateStatus("Invalid username format (alphanumeric, _, -, . only)", Color.red);
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            UpdateStatus("Please enter a password", Color.red);
            return;
        }

        if (!PasswordHasher.IsValidPassword(password))
        {
            UpdateStatus("Invalid password", Color.red);
            return;
        }

        // Start login process
        StartCoroutine(LoginProcess(username, password));
    }

    private void OnReconnectButtonClicked()
    {
        if (_isConnecting)
        {
            UpdateStatus("Already connecting...", Color.yellow);
            return;
        }

        StartCoroutine(ReconnectProcess());
    }

    private IEnumerator LoginProcess(string username, string password)
    {
        _isConnecting = true;
        UpdateUIState();
        
        UpdateStatus("Connecting to server...", Color.yellow);

        // Hash password for secure transport
        string hashedPassword = PasswordHasher.HashPasswordForTransport(password, username);
        
        // Connect to server first - use multiple methods to find NetworkManager
        var networkManager = GetNetworkManager();
        if (networkManager == null)
        {
            UpdateStatus("Network manager not available", Color.red);
            _isConnecting = false;
            UpdateUIState();
            yield break;
        }

        // Store login credentials for after connection
        PlayerPrefs.SetString("PendingUsername", username);
        PlayerPrefs.SetString("PendingPasswordHash", hashedPassword);
        PlayerPrefs.Save();

        // Connect to server
        networkManager.ConnectToServer();
        
        // Wait for connection or timeout
        float timeout = 10f;
        float elapsed = 0f;
        
        while (!networkManager.IsConnected && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            UpdateStatus($"Connecting... ({timeout - elapsed:F0}s)", Color.yellow);
            yield return new WaitForSeconds(0.1f);
        }

        if (!networkManager.IsConnected)
        {
            UpdateStatus("Failed to connect to server", Color.red);
            _isConnecting = false;
            UpdateUIState();
            yield break;
        }

        UpdateStatus("Authenticating...", Color.yellow);
        
        // Send login message using coroutine-safe method
        var loginMessage = new
        {
            Username = username,
            ClientHashedPassword = hashedPassword
        };

        // Use StartCoroutine for sending the message
        yield return StartCoroutine(SendLoginMessage(networkManager, loginMessage));
        
        // Clear password field for security
        if (PasswordInput != null)
        {
            PasswordInput.text = "";
        }

        _isConnecting = false;
        UpdateUIState();
    }

    private IEnumerator SendLoginMessage(NetworkManager networkManager, object loginMessage)
    {
        // Convert async call to coroutine-compatible approach
        var sendTask = networkManager.SendMessage("Login", loginMessage);
        
        // Wait for the task to complete (simple polling approach)
        while (!sendTask.IsCompleted)
        {
            yield return new WaitForSeconds(0.1f);
        }

        if (sendTask.Exception != null)
        {
            Debug.LogError($"Login message send error: {sendTask.Exception}");
            UpdateStatus("Failed to send login request", Color.red);
        }
        else if (sendTask.IsFaulted)
        {
            Debug.LogError($"Login message task faulted");
            UpdateStatus("Failed to send login request", Color.red);
        }
    }

    private IEnumerator ReconnectProcess()
    {
        _isConnecting = true;
        UpdateUIState();
        
        UpdateStatus("Reconnecting with saved session...", Color.yellow);

        string sessionToken = SessionManager.GetValidSessionToken();
        if (string.IsNullOrEmpty(sessionToken))
        {
            UpdateStatus("No valid session found", Color.red);
            _isConnecting = false;
            UpdateUIState();
            yield break;
        }

        var networkManager = GetNetworkManager();
        if (networkManager == null)
        {
            UpdateStatus("Network manager not available", Color.red);
            _isConnecting = false;
            UpdateUIState();
            yield break;
        }

        // Connect to server first
        networkManager.ConnectToServer();
        
        // Wait for connection
        float timeout = 10f;
        float elapsed = 0f;
        
        while (!networkManager.IsConnected && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            UpdateStatus($"Connecting... ({timeout - elapsed:F0}s)", Color.yellow);
            yield return new WaitForSeconds(0.1f);
        }

        if (!networkManager.IsConnected)
        {
            UpdateStatus("Failed to connect to server", Color.red);
            _isConnecting = false;
            UpdateUIState();
            yield break;
        }

        UpdateStatus("Validating session...", Color.yellow);
        
        // Send session validation message
        var sessionMessage = new
        {
            SessionToken = sessionToken
        };

        yield return StartCoroutine(SendSessionMessage(networkManager, sessionMessage));

        _isConnecting = false;
        UpdateUIState();
    }

    private IEnumerator SendSessionMessage(NetworkManager networkManager, object sessionMessage)
    {
        // Convert async call to coroutine-compatible approach
        var sendTask = networkManager.SendMessage("SessionValidation", sessionMessage);
        
        // Wait for the task to complete
        while (!sendTask.IsCompleted)
        {
            yield return new WaitForSeconds(0.1f);
        }

        if (sendTask.Exception != null)
        {
            Debug.LogError($"Session validation send error: {sendTask.Exception}");
            UpdateStatus("Failed to send session validation", Color.red);
        }
        else if (sendTask.IsFaulted)
        {
            Debug.LogError($"Session validation task faulted");
            UpdateStatus("Failed to send session validation", Color.red);
        }
    }

    private IEnumerator AttemptAutoReconnection()
    {
        _hasTriedAutoReconnect = true;
        
        UpdateStatus("Attempting automatic reconnection...", Color.cyan);
        yield return new WaitForSeconds(1f); // Brief delay to show message
        
        yield return StartCoroutine(ReconnectProcess());
    }

    // Network event handlers
    private void OnConnected()
    {
        UpdateStatus("Connected to server", Color.green);
    }

    private void OnDisconnected()
    {
        if (!_isConnecting)
        {
            UpdateStatus("Disconnected from server", Color.red);
            ShowLoginPanel();
        }
    }

    // UI Helper Methods
    public void ShowLoginPanel()
    {
        if (LoginPanel != null)
        {
            LoginPanel.SetActive(true);
        }
        UpdateSessionInfo();
    }

    public void HideLoginPanel()
    {
        if (LoginPanel != null)
        {
            LoginPanel.SetActive(false);
        }
    }

    private void UpdateStatus(string message, Color color)
    {
        if (StatusText != null)
        {
            StatusText.text = message;
            StatusText.color = color;
        }
        Debug.Log($"Login Status: {message}");
    }

    private void UpdateSessionInfo()
    {
        if (SessionInfoText != null)
        {
            SessionInfoText.text = SessionManager.GetSessionInfo();
        }
    }

    private void UpdateUIState()
    {
        bool canInteract = !_isConnecting;
        
        if (LoginButton != null)
            LoginButton.interactable = canInteract;
        
        if (ReconnectButton != null)
            ReconnectButton.interactable = canInteract && SessionManager.HasStoredSession();
            
        if (UsernameInput != null)
            UsernameInput.interactable = canInteract;
            
        if (PasswordInput != null)
            PasswordInput.interactable = canInteract;
    }

    public void OnLoginSuccess(string sessionToken, string username, string playerName, string playerId)
    {
        // Save session if remember me is checked or if we want to always save
        bool shouldSaveSession = RememberMeToggle?.isOn ?? true;
        
        if (shouldSaveSession && !string.IsNullOrEmpty(sessionToken))
        {
            SessionManager.SaveSession(sessionToken, username, playerName, playerId);
        }

        UpdateStatus("Login successful!", Color.green);
        
        // Hide login panel after brief success message
        StartCoroutine(HideLoginAfterDelay(1.5f));
    }

    public void OnLoginFailed(string errorMessage)
    {
        UpdateStatus($"Login failed: {errorMessage}", Color.red);
        
        // Clear sensitive data
        PlayerPrefs.DeleteKey("PendingUsername");
        PlayerPrefs.DeleteKey("PendingPasswordHash");
    }

    private IEnumerator HideLoginAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideLoginPanel();
        
        // Notify UI manager to show game UI
        var uiManager = FindObjectOfType<UIManager>();
        if (uiManager != null)
        {
            uiManager.ShowGameUI();
        }
    }

    public void OnLogoutButtonClicked()
    {
        SessionManager.ClearSession();
        UpdateStatus("Logged out", Color.gray);
        ShowLoginPanel();
    }

    // Helper method to find NetworkManager using multiple approaches
    private NetworkManager GetNetworkManager()
    {
        NetworkManager networkManager = null;
        
        // Method 1: Try GameManager.Instance
        if (GameManager.Instance != null)
        {
            networkManager = GameManager.Instance.NetworkManager;
            if (networkManager != null)
            {
                Debug.Log("Found NetworkManager via GameManager.Instance");
                return networkManager;
            }
        }
        else
        {
            Debug.LogWarning("GameManager.Instance is null");
        }
        
        // Method 2: Try FindObjectOfType as fallback
        networkManager = FindObjectOfType<NetworkManager>();
        if (networkManager != null)
        {
            Debug.Log("Found NetworkManager via FindObjectOfType");
            return networkManager;
        }
        
        // Method 3: Try finding GameManager first, then get NetworkManager
        var gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            networkManager = gameManager.GetComponent<NetworkManager>();
            if (networkManager != null)
            {
                Debug.Log("Found NetworkManager via GameManager component");
                return networkManager;
            }
        }
        
        Debug.LogError("NetworkManager not found using any method!");
        Debug.LogError("Available GameObjects in scene:");
        
        // Debug: List all GameObjects to help troubleshoot
        var allObjects = FindObjectsOfType<MonoBehaviour>();
        foreach (var obj in allObjects)
        {
            if (obj.name.Contains("Game") || obj.name.Contains("Network") || obj.name.Contains("Manager"))
            {
                Debug.LogError($"Found object: {obj.name} ({obj.GetType().Name})");
            }
        }
        
        return null;
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        NetworkManager.OnConnected -= OnConnected;
        NetworkManager.OnDisconnected -= OnDisconnected;
        
        // Clean up sensitive data
        PlayerPrefs.DeleteKey("PendingUsername");
        PlayerPrefs.DeleteKey("PendingPasswordHash");
    }
}