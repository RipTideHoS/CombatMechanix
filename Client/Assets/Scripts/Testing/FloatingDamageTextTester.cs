using UnityEngine;

/// <summary>
/// Test script for the floating damage text system
/// Provides keyboard shortcuts to test different damage types and scenarios
/// This can be attached to any GameObject for testing purposes
/// </summary>
public class FloatingDamageTextTester : MonoBehaviour
{
    [Header("Test Settings")]
    public bool EnableTestMode = true;
    public float TestDamageAmount = 25f;
    public float TestHealAmount = 15f;
    public float TestCriticalDamage = 75f;
    
    [Header("Test Keys")]
    public KeyCode TestRegularDamageKey = KeyCode.Alpha1;
    public KeyCode TestCriticalDamageKey = KeyCode.Alpha2;
    public KeyCode TestHealingKey = KeyCode.Alpha3;
    public KeyCode TestRandomDamageKey = KeyCode.Alpha4;
    public KeyCode ShowStatsKey = KeyCode.Alpha9;
    
    private EnemyDamageTextManager _damageTextManager;
    private Camera _mainCamera;
    
    private void Start()
    {
        if (!EnableTestMode) return;
        
        _damageTextManager = FindObjectOfType<EnemyDamageTextManager>();
        _mainCamera = Camera.main;
        
        Debug.Log("[FloatingDamageTextTester] Test mode enabled. Controls:");
        Debug.Log($"  {TestRegularDamageKey} - Test regular damage");
        Debug.Log($"  {TestCriticalDamageKey} - Test critical damage");
        Debug.Log($"  {TestHealingKey} - Test healing");
        Debug.Log($"  {TestRandomDamageKey} - Test random damage at mouse position");
        Debug.Log($"  {ShowStatsKey} - Show damage text manager stats");
    }
    
    private void Update()
    {
        if (!EnableTestMode) return;
        
        // Test regular damage
        if (Input.GetKeyDown(TestRegularDamageKey))
        {
            TestDamageOnRandomEnemy(TestDamageAmount, DamageType.Regular);
        }
        
        // Test critical damage
        if (Input.GetKeyDown(TestCriticalDamageKey))
        {
            TestDamageOnRandomEnemy(TestCriticalDamage, DamageType.Critical);
        }
        
        // Test healing
        if (Input.GetKeyDown(TestHealingKey))
        {
            TestDamageOnRandomEnemy(TestHealAmount, DamageType.Healing);
        }
        
        // Test random damage at mouse position
        if (Input.GetKeyDown(TestRandomDamageKey))
        {
            TestDamageAtMousePosition();
        }
        
        // Show stats
        if (Input.GetKeyDown(ShowStatsKey))
        {
            ShowDamageTextStats();
        }
    }
    
    private void TestDamageOnRandomEnemy(float damage, DamageType damageType)
    {
        EnemyBase[] enemies = FindObjectsOfType<EnemyBase>();
        
        if (enemies.Length == 0)
        {
            Debug.LogWarning("[FloatingDamageTextTester] No enemies found in scene for testing");
            return;
        }
        
        // Pick random enemy
        EnemyBase randomEnemy = enemies[Random.Range(0, enemies.Length)];
        
        if (_damageTextManager != null)
        {
            bool isHealing = damageType == DamageType.Healing;
            _damageTextManager.ShowDamageText(randomEnemy, damage, isHealing);
            
            Debug.Log($"[FloatingDamageTextTester] Showed {damageType} damage: {damage} on {randomEnemy.EnemyName}");
        }
        else
        {
            Debug.LogWarning("[FloatingDamageTextTester] EnemyDamageTextManager not found");
        }
    }
    
    private void TestDamageAtMousePosition()
    {
        if (_mainCamera == null) return;
        
        // Convert mouse position to world position
        Vector3 mousePosition = Input.mousePosition;
        mousePosition.z = 10f; // Distance from camera
        Vector3 worldPosition = _mainCamera.ScreenToWorldPoint(mousePosition);
        
        // Random damage type and amount
        DamageType[] damageTypes = { DamageType.Regular, DamageType.Critical, DamageType.Healing };
        DamageType randomType = damageTypes[Random.Range(0, damageTypes.Length)];
        float randomDamage = Random.Range(10f, 100f);
        
        if (_damageTextManager != null)
        {
            _damageTextManager.ShowDamageAtWorldPosition(worldPosition, randomDamage, randomType);
            
            Debug.Log($"[FloatingDamageTextTester] Showed {randomType} damage: {randomDamage:F1} at mouse position {worldPosition}");
        }
    }
    
    private void ShowDamageTextStats()
    {
        if (_damageTextManager != null)
        {
            _damageTextManager.LogStatistics();
        }
        else
        {
            Debug.LogWarning("[FloatingDamageTextTester] EnemyDamageTextManager not found");
        }
    }
    
    private void OnGUI()
    {
        if (!EnableTestMode) return;
        
        // Show test controls on screen
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label("Floating Damage Text Tester", GUI.skin.box);
        
        if (GUILayout.Button($"Regular Damage ({TestRegularDamageKey})"))
        {
            TestDamageOnRandomEnemy(TestDamageAmount, DamageType.Regular);
        }
        
        if (GUILayout.Button($"Critical Damage ({TestCriticalDamageKey})"))
        {
            TestDamageOnRandomEnemy(TestCriticalDamage, DamageType.Critical);
        }
        
        if (GUILayout.Button($"Healing ({TestHealingKey})"))
        {
            TestDamageOnRandomEnemy(TestHealAmount, DamageType.Healing);
        }
        
        if (GUILayout.Button($"Random at Mouse ({TestRandomDamageKey})"))
        {
            TestDamageAtMousePosition();
        }
        
        if (GUILayout.Button($"Show Stats ({ShowStatsKey})"))
        {
            ShowDamageTextStats();
        }
        
        GUILayout.EndArea();
    }
}