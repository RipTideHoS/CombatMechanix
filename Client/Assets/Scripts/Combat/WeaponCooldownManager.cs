using System;
using UnityEngine;

/// <summary>
/// Manages client-side weapon cooldown validation to prevent attack spam
/// and provide instant feedback to players about attack timing
/// </summary>
public class WeaponCooldownManager : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    
    // Current weapon timing info
    private WeaponTimingMessage _currentWeaponTiming;
    private long _lastAttackTime = 0;
    private long _serverTimeOffset = 0; // For clock sync
    
    // UI feedback (optional)
    public event Action<float> OnCooldownProgress; // 0.0 = ready, 1.0 = just attacked
    public event Action<bool> OnAttackAvailable; // true = can attack, false = on cooldown
    
    private void Start()
    {
        // Set default weapon timing (unarmed)
        SetDefaultWeaponTiming();
        
        Debug.Log("[WeaponCooldownManager] Initialized with default weapon timing");
    }
    
    /// <summary>
    /// Update weapon timing information from server
    /// </summary>
    public void UpdateWeaponTiming(WeaponTimingMessage timingMessage)
    {
        if (timingMessage == null) return;
        
        _currentWeaponTiming = timingMessage;
        
        // Update server time offset for sync
        long clientTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _serverTimeOffset = timingMessage.ServerTime - clientTime;
        
        if (showDebugInfo)
        {
            Debug.Log($"[WeaponCooldownManager] Updated weapon timing: " +
                     $"{timingMessage.WeaponName} ({timingMessage.AttackSpeed} attacks/sec, " +
                     $"{timingMessage.CooldownMs}ms cooldown)");
            Debug.Log($"[WeaponCooldownManager] Server time offset: {_serverTimeOffset}ms");
        }
        
        // Reset cooldown when switching weapons
        _lastAttackTime = 0;
        OnAttackAvailable?.Invoke(true);
    }
    
    /// <summary>
    /// Check if an attack can be performed right now
    /// </summary>
    public bool CanAttack()
    {
        if (_currentWeaponTiming == null) return true; // Allow if no timing data
        
        long currentTime = GetSyncedTime();
        long timeSinceLastAttack = currentTime - _lastAttackTime;
        
        // TEMPORARY: Reduce cooldown for Phase 1 testing
        int testCooldown = Mathf.Min(_currentWeaponTiming.CooldownMs, 500); // Max 500ms cooldown for testing
        bool canAttack = timeSinceLastAttack >= testCooldown;
        
        if (showDebugInfo && !canAttack)
        {
            long remainingMs = testCooldown - timeSinceLastAttack;
            Debug.Log($"[WeaponCooldownManager] Attack blocked: {remainingMs}ms remaining " +
                     $"(last: {_lastAttackTime}, now: {currentTime}, diff: {timeSinceLastAttack}ms, testCooldown: {testCooldown}ms)");
        }
        
        return canAttack;
    }
    
    /// <summary>
    /// Record that an attack was performed (call this when sending attack to server)
    /// </summary>
    public void RecordAttack()
    {
        _lastAttackTime = GetSyncedTime();
        OnAttackAvailable?.Invoke(false);
        
        if (showDebugInfo)
        {
            Debug.Log($"[WeaponCooldownManager] Attack recorded at {_lastAttackTime}ms " +
                     $"(cooldown: {_currentWeaponTiming?.CooldownMs ?? 1000}ms)");
        }
    }
    
    /// <summary>
    /// Get remaining cooldown time in seconds (0 if ready)
    /// </summary>
    public float GetRemainingCooldownSeconds()
    {
        if (_currentWeaponTiming == null) return 0f;
        
        long currentTime = GetSyncedTime();
        long timeSinceLastAttack = currentTime - _lastAttackTime;
        long remainingMs = _currentWeaponTiming.CooldownMs - timeSinceLastAttack;
        
        return Mathf.Max(0f, remainingMs / 1000f);
    }
    
    /// <summary>
    /// Get cooldown progress (0.0 = ready, 1.0 = just attacked)
    /// </summary>
    public float GetCooldownProgress()
    {
        if (_currentWeaponTiming == null) return 0f;
        
        long currentTime = GetSyncedTime();
        long timeSinceLastAttack = currentTime - _lastAttackTime;
        
        if (timeSinceLastAttack >= _currentWeaponTiming.CooldownMs)
            return 0f; // Ready
        
        return 1f - (float)timeSinceLastAttack / _currentWeaponTiming.CooldownMs;
    }
    
    /// <summary>
    /// Get current weapon info for display
    /// </summary>
    public string GetWeaponInfo()
    {
        if (_currentWeaponTiming == null) return "Unknown Weapon";
        
        return $"{_currentWeaponTiming.WeaponName} " +
               $"({_currentWeaponTiming.AttackSpeed:F1}/sec)";
    }
    
    private void Update()
    {
        // Update UI events
        if (_currentWeaponTiming != null)
        {
            float progress = GetCooldownProgress();
            OnCooldownProgress?.Invoke(progress);
            
            bool wasAvailable = progress == 0f;
            OnAttackAvailable?.Invoke(wasAvailable);
        }
    }
    
    private void SetDefaultWeaponTiming()
    {
        _currentWeaponTiming = new WeaponTimingMessage
        {
            AttackSpeed = 1.0m,
            CooldownMs = 1000,
            WeaponType = "Melee",
            WeaponName = "Unarmed",
            HasWeaponEquipped = false,
            ServerTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }
    
    private long GetSyncedTime()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + _serverTimeOffset;
    }
    
    private void OnGUI()
    {
        if (!showDebugInfo) return;
        
        // Debug UI overlay
        GUILayout.BeginArea(new Rect(10, 100, 300, 150));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label("Weapon Cooldown Debug", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
        
        if (_currentWeaponTiming != null)
        {
            GUILayout.Label($"Weapon: {_currentWeaponTiming.WeaponName}");
            GUILayout.Label($"Cooldown: {_currentWeaponTiming.CooldownMs}ms");
            
            float remaining = GetRemainingCooldownSeconds();
            bool canAttack = CanAttack();
            
            GUI.color = canAttack ? Color.green : Color.red;
            GUILayout.Label(canAttack ? "✅ READY" : $"⏳ {remaining:F1}s");
            GUI.color = Color.white;
        }
        else
        {
            GUILayout.Label("No weapon timing data");
        }
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}