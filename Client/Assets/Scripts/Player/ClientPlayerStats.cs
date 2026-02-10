using System;
using UnityEngine;

/// <summary>
/// Client-side player statistics manager that receives authoritative data from server
/// </summary>
public class ClientPlayerStats : MonoBehaviour
{
    [Header("Current Player Stats")]
    public int Level = 1;
    public long Experience = 0;
    public int Health = 100;
    public int MaxHealth = 100;
    public int Strength = 10;
    public int Defense = 10;
    public int Speed = 10;
    public long ExperienceToNextLevel = 100;
    public int Gold = 0;

    [Header("Display Settings")]
    public bool ShowDebugStats = true;

    // Events for UI updates
    public static event Action<ClientPlayerStats> OnStatsUpdated;
    public static event Action<int> OnLevelUp;
    public static event Action<int, int> OnHealthChanged; // (newHealth, healthChange)
    public static event Action<long, string> OnExperienceGained; // (experience, source)
    public static event Action<int> OnGoldChanged; // (newGold)
    public static event Action OnPlayerDeath; // New death event

    private void Awake()
    {
        Debug.Log("[CLIENT] ClientPlayerStats Awake() called");
        // Subscribe immediately in Awake so we never miss early messages (e.g. LoginResponse stats)
        NetworkManager.OnPlayerStatsUpdate += HandlePlayerStatsUpdate;
        NetworkManager.OnLevelUp += HandleLevelUp;
        NetworkManager.OnHealthChange += HandleHealthChange;
        NetworkManager.OnExperienceGain += HandleExperienceGain;
        Debug.Log("[CLIENT] ClientPlayerStats subscribed to network events in Awake");
    }

    private void OnDestroy()
    {
        // Unsubscribe from network events
        NetworkManager.OnPlayerStatsUpdate -= HandlePlayerStatsUpdate;
        NetworkManager.OnLevelUp -= HandleLevelUp;
        NetworkManager.OnHealthChange -= HandleHealthChange;
        NetworkManager.OnExperienceGain -= HandleExperienceGain;
    }

    private void HandlePlayerStatsUpdate(NetworkMessages.PlayerStatsUpdateMessage statsUpdate)
    {
        Debug.Log($"Received player stats update: Level {statsUpdate.Level}, Health {statsUpdate.Health}/{statsUpdate.MaxHealth}, Experience {statsUpdate.Experience}, Gold {statsUpdate.Gold}");
        
        // Track gold change
        int previousGold = Gold;
        
        // Update all stats from server
        Level = statsUpdate.Level;
        Experience = statsUpdate.Experience;
        Health = statsUpdate.Health;
        MaxHealth = statsUpdate.MaxHealth;
        Strength = statsUpdate.Strength;
        Defense = statsUpdate.Defense;
        Speed = statsUpdate.Speed;
        ExperienceToNextLevel = statsUpdate.ExperienceToNextLevel;
        Gold = statsUpdate.Gold;
        
        // Fire gold changed event if gold actually changed
        if (Gold != previousGold)
        {
            OnGoldChanged?.Invoke(Gold);
        }

        // Check if player is dead and trigger death event if needed
        if (Health <= 0)
        {
            Debug.Log("[CLIENT] Player loaded as dead - triggering OnPlayerDeath event");
            OnPlayerDeath?.Invoke();
        }

        // Notify UI systems
        OnStatsUpdated?.Invoke(this);

        if (ShowDebugStats)
        {
            Debug.Log($"Stats Updated - Level: {Level}, Health: {Health}/{MaxHealth}, Experience: {Experience}/{Experience + ExperienceToNextLevel}, Str: {Strength}, Def: {Defense}, Spd: {Speed}, Gold: {Gold}");
        }
    }
    
    /// <summary>
    /// Public method to update gold from external sources (like VendorUI) and fire events
    /// </summary>
    public void UpdateGold(int newGold)
    {
        int previousGold = Gold;
        Gold = newGold;
        
        // Fire events if gold actually changed
        if (Gold != previousGold)
        {
            OnGoldChanged?.Invoke(Gold);
            OnStatsUpdated?.Invoke(this);
            Debug.Log($"[ClientPlayerStats] Gold updated from {previousGold} to {Gold}");
        }
    }

    private void HandleLevelUp(NetworkMessages.LevelUpMessage levelUpMessage)
    {
        Debug.Log($"🎉 LEVEL UP! New Level: {levelUpMessage.NewLevel}, Stat Points Gained: {levelUpMessage.StatPointsGained}");
        
        // Update stats from the level up message
        if (levelUpMessage.NewStats != null)
        {
            HandlePlayerStatsUpdate(levelUpMessage.NewStats);
        }

        // Trigger level up event
        OnLevelUp?.Invoke(levelUpMessage.NewLevel);

        // Show level up notification
        var uiManager = GameManager.Instance?.UIManager;
        if (uiManager != null)
        {
            uiManager.ShowNotification($"LEVEL UP! You are now level {levelUpMessage.NewLevel}!", Color.yellow);
        }
    }

    private void HandleHealthChange(NetworkMessages.HealthChangeMessage healthChange)
    {
        Debug.Log($"[CLIENT] HandleHealthChange called: PlayerId={healthChange.PlayerId}, Health changed by {healthChange.HealthChange} to {healthChange.NewHealth} from {healthChange.Source}");
        
        // Check if this health change is for the local player
        var gameManager = GameManager.Instance;
        if (gameManager == null || gameManager.LocalPlayerId != healthChange.PlayerId)
        {
            Debug.Log($"[CLIENT] HealthChange not for local player. Local={gameManager?.LocalPlayerId}, Message={healthChange.PlayerId}");
            return; // This health change is not for us
        }
        
        Debug.Log($"[CLIENT] Current Health before change: {Health}");
        
        int oldHealth = Health;
        Health = healthChange.NewHealth;
        
        Debug.Log($"[CLIENT] Health updated from {oldHealth} to {Health}");

        // Check for death (health reached 0)
        if (Health <= 0 && oldHealth > 0)
        {
            Debug.Log("[CLIENT] Player died - triggering OnPlayerDeath event");
            OnPlayerDeath?.Invoke();
        }

        // Trigger health change event
        Debug.Log($"[CLIENT] About to invoke OnHealthChanged static event. Subscribers: {OnHealthChanged?.GetInvocationList()?.Length ?? 0}");
        OnHealthChanged?.Invoke(Health, healthChange.HealthChange);
        Debug.Log($"[CLIENT] OnHealthChanged static event invoked");

        // Update UI
        var uiManager = gameManager.UIManager;
        Debug.Log($"[CLIENT] UIManager found: {uiManager != null}");
        if (uiManager != null)
        {
            uiManager.UpdateHealth(healthChange.HealthChange);
            Debug.Log($"[CLIENT] Called UIManager.UpdateHealth({healthChange.HealthChange})");
        }

        // Show damage/healing effects
        if (healthChange.HealthChange != 0)
        {
            Color effectColor = healthChange.HealthChange > 0 ? Color.green : Color.red;
            string effectText = healthChange.HealthChange > 0 ? $"+{healthChange.HealthChange}" : $"{healthChange.HealthChange}";
            
            if (uiManager != null)
            {
                uiManager.ShowNotification($"{effectText} HP", effectColor);
                Debug.Log($"[CLIENT] Called UIManager.ShowNotification(\"{effectText} HP\")");
            }
        }
        
        Debug.Log($"[CLIENT] HandleHealthChange completed");
    }

    private void HandleExperienceGain(NetworkMessages.ExperienceGainMessage expGain)
    {
        Debug.Log($"Gained {expGain.ExperienceGained} experience from {expGain.Source}");
        
        // Trigger experience gain event
        OnExperienceGained?.Invoke(expGain.ExperienceGained, expGain.Source);

        // Show experience gain notification
        var uiManager = GameManager.Instance?.UIManager;
        if (uiManager != null)
        {
            uiManager.ShowNotification($"+{expGain.ExperienceGained} XP ({expGain.Source})", Color.cyan);
        }
    }

    // Public methods for other systems to use
    public float GetHealthPercentage()
    {
        return MaxHealth > 0 ? (float)Health / MaxHealth : 0f;
    }

    public float GetExperiencePercentage()
    {
        long totalExpForLevel = Experience + ExperienceToNextLevel;
        return totalExpForLevel > 0 ? (float)Experience / totalExpForLevel : 0f;
    }

    public bool IsAlive()
    {
        return Health > 0;
    }

    public bool IsMaxLevel()
    {
        return ExperienceToNextLevel <= 0;
    }

    // Method to request experience gain (for testing purposes)
    public void TestExperienceGain(long amount, string source = "Test")
    {
        if (GameManager.Instance?.NetworkManager != null)
        {
            var expMessage = new NetworkMessages.ExperienceGainMessage
            {
                PlayerId = GameManager.Instance.LocalPlayerId,
                ExperienceGained = amount,
                Source = source
            };
            
            _ = GameManager.Instance.NetworkManager.SendMessage("ExperienceGain", expMessage);
            Debug.Log($"Sent test experience gain request: {amount} from {source}");
        }
    }

    // NOTE: TestHealthChange method removed - clients should NOT send health changes back to server
    // In a server-authoritative system, only the server calculates and sends health changes to clients

    // Debug display for inspector
    private void OnGUI()
    {
        if (!ShowDebugStats) return;

        GUILayout.BeginArea(new Rect(10, 100, 300, 300));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label("Player Stats (Server Authoritative)");
        GUILayout.Label($"Level: {Level}");
        GUILayout.Label($"Health: {Health}/{MaxHealth} ({GetHealthPercentage():P1})");
        GUILayout.Label($"Experience: {Experience} (Need {ExperienceToNextLevel} more)");
        GUILayout.Label($"Progress: {GetExperiencePercentage():P1}");
        GUILayout.Label($"Strength: {Strength} | Defense: {Defense} | Speed: {Speed}");
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("Test +50 XP"))
        {
            TestExperienceGain(50, "GUI Test");
        }
        
        if (GUILayout.Button("Test Health (Server Only)"))
        {
            Debug.Log("Health changes are now server-authoritative only. No client test available.");
        }
        
        if (GUILayout.Button("Force Death Check"))
        {
            Debug.Log($"[CLIENT] Current Health: {Health}, IsAlive: {IsAlive()}");
            if (Health <= 0)
            {
                Debug.Log("[CLIENT] Health <= 0, triggering OnPlayerDeath");
                OnPlayerDeath?.Invoke();
            }
            else
            {
                Debug.Log("[CLIENT] Health > 0, no death event triggered");
            }
        }
        
        if (GUILayout.Button("Show Death Banner"))
        {
            var deathBanner = FindObjectOfType<DeathBanner>();
            if (deathBanner != null)
            {
                deathBanner.ForceShowDeathBanner();
            }
            else
            {
                Debug.LogError("DeathBanner not found!");
            }
        }
        
        if (GUILayout.Button("Test Event Subscription"))
        {
            Debug.Log("[CLIENT] Testing if HandleHealthChange method works...");
            var testMessage = new NetworkMessages.HealthChangeMessage
            {
                PlayerId = GameManager.Instance?.LocalPlayerId ?? "test",
                NewHealth = 95,
                HealthChange = -5,
                Source = "Manual Test"
            };
            HandleHealthChange(testMessage);
        }
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}