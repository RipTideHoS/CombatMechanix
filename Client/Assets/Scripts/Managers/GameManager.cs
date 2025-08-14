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

        // Find LocalPlayer in scene
        LocalPlayer = FindObjectOfType<PlayerController>();

        // Subscribe to network events
        if (NetworkManager != null)
        {
            NetworkManager.OnConnected += OnNetworkConnected;
            NetworkManager.OnDisconnected += OnNetworkDisconnected;
        }

        Debug.Log($"Game initialized for player: {LocalPlayerName} ({LocalPlayerId})");
    }

    private async void OnNetworkConnected()
    {
        Debug.Log($"Network connected, authenticating player: {LocalPlayerName} ({LocalPlayerId})");
        if (NetworkManager != null)
        {
            await NetworkManager.AuthenticatePlayer(LocalPlayerId, LocalPlayerName);
            IsAuthenticated = true;
            Debug.Log("Player authentication sent to server");
        }
        else
        {
            Debug.LogError("NetworkManager is null during authentication!");
        }
    }

    private void OnNetworkDisconnected()
    {
        Debug.Log("Network disconnected");
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
        Debug.Log("Player authenticated and in game!");
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
        // Unsubscribe from events
        if (NetworkManager != null)
        {
            NetworkManager.OnConnected -= OnNetworkConnected;
            NetworkManager.OnDisconnected -= OnNetworkDisconnected;
        }
    }

    // Public methods for other systems to use
    public void SetLocalPlayerName(string newName)
    {
        LocalPlayerName = newName;
        Debug.Log($"Player name changed to: {LocalPlayerName}");
    }

    public bool IsPlayerReady()
    {
        return IsAuthenticated && IsInGame && LocalPlayer != null;
    }
}