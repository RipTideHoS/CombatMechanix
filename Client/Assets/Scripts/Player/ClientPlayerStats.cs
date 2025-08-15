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

    [Header("Display Settings")]
    public bool ShowDebugStats = true;

    // Events for UI updates
    public static event Action<ClientPlayerStats> OnStatsUpdated;
    public static event Action<int> OnLevelUp;
    public static event Action<int, int> OnHealthChanged; // (newHealth, healthChange)
    public static event Action<long, string> OnExperienceGained; // (experience, source)

    private void Start()
    {
        // Subscribe to network events
        NetworkManager.OnPlayerStatsUpdate += HandlePlayerStatsUpdate;
        NetworkManager.OnLevelUp += HandleLevelUp;
        NetworkManager.OnHealthChange += HandleHealthChange;
        NetworkManager.OnExperienceGain += HandleExperienceGain;

        Debug.Log("ClientPlayerStats initialized and subscribed to network events");
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
        Debug.Log($"Received player stats update: Level {statsUpdate.Level}, Health {statsUpdate.Health}/{statsUpdate.MaxHealth}, Experience {statsUpdate.Experience}");
        
        // Update all stats from server
        Level = statsUpdate.Level;
        Experience = statsUpdate.Experience;
        Health = statsUpdate.Health;
        MaxHealth = statsUpdate.MaxHealth;
        Strength = statsUpdate.Strength;
        Defense = statsUpdate.Defense;
        Speed = statsUpdate.Speed;
        ExperienceToNextLevel = statsUpdate.ExperienceToNextLevel;

        // Notify UI systems
        OnStatsUpdated?.Invoke(this);

        if (ShowDebugStats)
        {
            Debug.Log($"Stats Updated - Level: {Level}, Health: {Health}/{MaxHealth}, Experience: {Experience}/{Experience + ExperienceToNextLevel}, Str: {Strength}, Def: {Defense}, Spd: {Speed}");
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
        Debug.Log($"Health changed by {healthChange.HealthChange} to {healthChange.NewHealth} from {healthChange.Source}");
        
        int oldHealth = Health;
        Health = healthChange.NewHealth;

        // Trigger health change event
        OnHealthChanged?.Invoke(Health, healthChange.HealthChange);

        // Update UI
        var uiManager = GameManager.Instance?.UIManager;
        if (uiManager != null)
        {
            uiManager.UpdateHealth(healthChange.HealthChange);
        }

        // Show damage/healing effects
        if (healthChange.HealthChange != 0)
        {
            Color effectColor = healthChange.HealthChange > 0 ? Color.green : Color.red;
            string effectText = healthChange.HealthChange > 0 ? $"+{healthChange.HealthChange}" : $"{healthChange.HealthChange}";
            
            if (uiManager != null)
            {
                uiManager.ShowNotification($"{effectText} HP", effectColor);
            }
        }
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

    // Method to request health change (for testing purposes)
    public void TestHealthChange(int healthChange, string source = "Test")
    {
        if (GameManager.Instance?.NetworkManager != null)
        {
            int newHealth = Mathf.Clamp(Health + healthChange, 0, MaxHealth);
            
            var healthMessage = new NetworkMessages.HealthChangeMessage
            {
                PlayerId = GameManager.Instance.LocalPlayerId,
                NewHealth = newHealth,
                HealthChange = healthChange,
                Source = source
            };
            
            _ = GameManager.Instance.NetworkManager.SendMessage("HealthChange", healthMessage);
            Debug.Log($"Sent test health change request: {healthChange} to {newHealth} from {source}");
        }
    }

    // Debug display for inspector
    private void OnGUI()
    {
        if (!ShowDebugStats) return;

        GUILayout.BeginArea(new Rect(10, 100, 300, 200));
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
        
        if (GUILayout.Button("Test -10 Health"))
        {
            TestHealthChange(-10, "GUI Test");
        }
        
        if (GUILayout.Button("Test +5 Health"))
        {
            TestHealthChange(5, "GUI Test");
        }
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}