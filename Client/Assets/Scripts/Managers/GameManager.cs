using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System.Linq;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Player Settings")]
    public string LocalPlayerId;
    public string LocalPlayerName;

    [Header("Game State")]
    public bool IsAuthenticated = false;
    public bool IsInGame = false;
    public float ServerTimeOffset = 0f;

    // Game Systems - assigned in Inspector or found automatically
    public NetworkManager NetworkManager { get; private set; }
    public PlayerController LocalPlayer { get; private set; }
    public WorldManager WorldManager { get; private set; }
    public UIManager UIManager { get; private set; }
    public ChatSystem ChatSystem { get; private set; }
    public CombatSystem CombatSystem { get; private set; }
    public InventoryManager InventoryManager { get; private set; }

    // Events
    public static event Action OnGameStarted;
    public static event Action OnGameEnded;

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeGame();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeGame()
    {
        // Generate unique player ID if not set
        if (string.IsNullOrEmpty(LocalPlayerId))
        {
            LocalPlayerId = System.Guid.NewGuid().ToString();
        }

        if (string.IsNullOrEmpty(LocalPlayerName))
        {
            LocalPlayerName = $"Player_{LocalPlayerId.Substring(0, 8)}";
        }

        // Initialize game systems - get components from this GameObject
        NetworkManager = GetComponent<NetworkManager>();
        WorldManager = GetComponent<WorldManager>();
        UIManager = GetComponent<UIManager>();
        ChatSystem = GetComponent<ChatSystem>();
        CombatSystem = GetComponent<CombatSystem>();
        InventoryManager = GetComponent<InventoryManager>();

        
        // If NetworkManager is null, try to find it in scene as fallback
        if (NetworkManager == null)
        {
            NetworkManager = FindObjectOfType<NetworkManager>();
        }

        // Find LocalPlayer in scene
        LocalPlayer = FindObjectOfType<PlayerController>();

        // Subscribe to static network events
        global::NetworkManager.OnConnected += OnNetworkConnected;
        global::NetworkManager.OnDisconnected += OnNetworkDisconnected;

    }

    /// <summary>
    /// Force re-initialization of NetworkManager - call this if NetworkManager is added after GameManager initialization
    /// </summary>
    public void RefreshNetworkManager()
    {
        Debug.Log($"[GameManager] RefreshNetworkManager called. Current NetworkManager: {(NetworkManager != null ? "exists" : "null")}");
        
        // Always try to refresh, even if we think we have one
        var oldNetworkManager = NetworkManager;
        NetworkManager = GetComponent<NetworkManager>();
        
        Debug.Log($"[GameManager] GetComponent<NetworkManager>() result: {(NetworkManager != null ? "found" : "null")}");
        
        if (NetworkManager == null)
        {
            NetworkManager = FindObjectOfType<NetworkManager>();
            Debug.Log($"[GameManager] FindObjectOfType<NetworkManager>() result: {(NetworkManager != null ? "found" : "null")}");
        }
        
        // Subscribe to network events if we found it (static events)
        if (NetworkManager != null)
        {
            Debug.Log("[GameManager] NetworkManager successfully re-initialized");
        }
        else
        {
            Debug.LogError("[GameManager] NetworkManager still not found after refresh");
            
            // Debug all GameObjects in scene with NetworkManager component
            var allNetworkManagers = FindObjectsOfType<NetworkManager>();
            Debug.Log($"[GameManager] Total NetworkManager components in scene: {allNetworkManagers.Length}");
            for (int i = 0; i < allNetworkManagers.Length; i++)
            {
                Debug.Log($"[GameManager] NetworkManager {i}: {allNetworkManagers[i].name} (GameObject: {allNetworkManagers[i].gameObject.name})");
            }
        }
    }

    private async void OnNetworkConnected()
    {
        // Note: Authentication is now handled by the new login system (LoginUI)
        // The old automatic authentication is disabled to enforce proper login
    }

    private void OnNetworkDisconnected()
    {
        IsAuthenticated = false;
        IsInGame = false;
        UIManager?.ShowConnectionLostUI();
    }

    public void OnPlayerAuthenticated()
    {
        IsAuthenticated = true;
        IsInGame = true;
        UIManager?.ShowGameUI();
        OnGameStarted?.Invoke();
    }

    public long GetServerTime()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (long)(ServerTimeOffset * 1000);
    }

    public void ExitGame()
    {
        IsInGame = false;
        OnGameEnded?.Invoke();
        Application.Quit();
    }

    private void OnDestroy()
    {
        // Unsubscribe from static events
        global::NetworkManager.OnConnected -= OnNetworkConnected;
        global::NetworkManager.OnDisconnected -= OnNetworkDisconnected;
    }

    // Public methods for other systems to use
    public void SetLocalPlayerName(string newName)
    {
        LocalPlayerName = newName;
    }

    public bool IsPlayerReady()
    {
        return IsAuthenticated && IsInGame && LocalPlayer != null;
    }
}