using UnityEngine;

/// <summary>
/// Test script for the Level Up Banner system
/// Provides keyboard shortcuts and GUI buttons to test level up animations
/// </summary>
public class LevelUpBannerTester : MonoBehaviour
{
    [Header("Test Settings")]
    public bool EnableTestMode = true;
    public int TestLevel = 2;
    
    [Header("Test Keys")]
    public KeyCode TestLevelUpKey = KeyCode.L;
    public KeyCode TestRandomLevelKey = KeyCode.Backslash;
    public KeyCode AdminResetKey = KeyCode.R;
    
    private LevelUpBanner _levelUpBanner;
    private AudioManager _audioManager;
    
    private void Start()
    {
        if (!EnableTestMode) return;
        
        _levelUpBanner = FindObjectOfType<LevelUpBanner>();
        _audioManager = AudioManager.Instance;
        
        Debug.Log("[LevelUpBannerTester] Test mode enabled. Controls:");
        Debug.Log($"  {TestLevelUpKey} - Test level up banner");
        Debug.Log($"  {TestRandomLevelKey} - Test random level (1-10)");
        Debug.Log($"  {AdminResetKey} - ADMIN: Reset to Level 1");
        Debug.Log("  GUI buttons available on screen");
    }
    
    private void Update()
    {
        if (!EnableTestMode) return;
        
        // Test level up banner
        if (Input.GetKeyDown(TestLevelUpKey))
        {
            TestLevelUpBanner();
        }
        
        // Test random level
        if (Input.GetKeyDown(TestRandomLevelKey))
        {
            TestRandomLevel();
        }
        
        // Admin reset stats
        if (Input.GetKeyDown(AdminResetKey))
        {
            ResetPlayerStats();
        }
    }
    
    private void TestLevelUpBanner()
    {
        if (_levelUpBanner != null)
        {
            _levelUpBanner.TestLevelUpBanner(TestLevel);
            Debug.Log($"[LevelUpBannerTester] Triggered level up banner for level {TestLevel}");
        }
        else
        {
            Debug.LogWarning("[LevelUpBannerTester] LevelUpBanner not found in scene");
        }
    }
    
    private void TestRandomLevel()
    {
        int randomLevel = Random.Range(1, 11);
        
        if (_levelUpBanner != null)
        {
            _levelUpBanner.TestLevelUpBanner(randomLevel);
            Debug.Log($"[LevelUpBannerTester] Triggered level up banner for random level {randomLevel}");
        }
        else
        {
            Debug.LogWarning("[LevelUpBannerTester] LevelUpBanner not found in scene");
        }
    }
    
    private void TestAudioOnly()
    {
        if (_audioManager != null)
        {
            _audioManager.PlayLevelUpSound();
            Debug.Log("[LevelUpBannerTester] Played level up sound");
        }
        else
        {
            Debug.LogWarning("[LevelUpBannerTester] AudioManager not found");
        }
    }
    
    private void TestExperienceGain()
    {
        var clientPlayerStats = FindObjectOfType<ClientPlayerStats>();
        if (clientPlayerStats != null)
        {
            // Test with enough XP to level up (100 XP should be enough for level 1->2)
            clientPlayerStats.TestExperienceGain(100, "Level Up Test");
            Debug.Log("[LevelUpBannerTester] Triggered experience gain test (100 XP)");
        }
        else
        {
            Debug.LogWarning("[LevelUpBannerTester] ClientPlayerStats not found");
        }
    }
    
    private async void ResetPlayerStats()
    {
        var networkManager = FindObjectOfType<NetworkManager>();
        if (networkManager != null)
        {
            // Send admin reset message to server using the correct API
            await networkManager.SendMessage("AdminResetStats", new { });
            Debug.Log("[LevelUpBannerTester] Sent admin reset stats command");
        }
        else
        {
            Debug.LogWarning("[LevelUpBannerTester] NetworkManager not found");
        }
    }
    
    private void OnGUI()
    {
        if (!EnableTestMode) return;
        
        // Show test controls on screen
        GUILayout.BeginArea(new Rect(10, 300, 300, 200));
        GUILayout.Label("Level Up Banner Tester", GUI.skin.box);
        
        if (GUILayout.Button($"Test Level {TestLevel} ({TestLevelUpKey})"))
        {
            TestLevelUpBanner();
        }
        
        if (GUILayout.Button($"Test Random Level ({TestRandomLevelKey})"))
        {
            TestRandomLevel();
        }
        
        if (GUILayout.Button("Test Audio Only"))
        {
            TestAudioOnly();
        }
        
        if (GUILayout.Button("Test Experience Gain (100 XP)"))
        {
            TestExperienceGain();
        }
        
        if (GUILayout.Button("RESET to Level 1 (Admin)"))
        {
            ResetPlayerStats();
        }
        
        GUILayout.Space(10);
        
        // Level input
        GUILayout.BeginHorizontal();
        GUILayout.Label("Test Level:", GUILayout.Width(70));
        if (int.TryParse(GUILayout.TextField(TestLevel.ToString(), GUILayout.Width(50)), out int newLevel))
        {
            TestLevel = Mathf.Clamp(newLevel, 1, 100);
        }
        GUILayout.EndHorizontal();
        
        // Status information
        GUILayout.Space(10);
        GUILayout.Label($"Banner Found: {_levelUpBanner != null}");
        GUILayout.Label($"Audio Manager: {_audioManager != null}");
        if (_levelUpBanner != null)
        {
            GUILayout.Label($"Banner Animating: {_levelUpBanner.IsAnimating}");
        }
        
        GUILayout.EndArea();
    }
}