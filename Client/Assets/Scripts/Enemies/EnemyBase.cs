using UnityEngine;

/// <summary>
/// Base class for all enemies in Combat Mechanix
/// Provides level-scaled health, damage, collision detection and damage handling
/// </summary>
public class EnemyBase : MonoBehaviour
{
    [Header("Enemy Stats")]
    public int Level = 1;
    public float BaseHealth = 100f;
    public float BaseDamage = 10f;
    public float HealthPerLevel = 25f;
    public float DamagePerLevel = 2.5f;
    
    [Header("Enemy Info")]
    public string EnemyName = "Enemy";
    public string EnemyType = "Basic";
    
    [Header("Combat Settings")]
    public bool IsInvulnerable = false;
    public float DamageFlashDuration = 0.2f;
    
    // Current stats (calculated from level)
    [Header("Current Stats (Read Only)")]
    [SerializeField] private float _currentHealth;
    [SerializeField] private float _maxHealth;
    [SerializeField] private float _currentDamage;
    [SerializeField] private bool _isDead = false;
    
    // Components
    private Renderer _renderer;
    private Color _originalColor;
    private Collider _collider;
    
    // Events
    public System.Action<float, float> OnHealthChanged; // current, max
    public System.Action<EnemyBase> OnEnemyDeath;
    public System.Action<float> OnDamageTaken; // damage amount
    
    private void Start()
    {
        InitializeEnemy();
    }
    
    private void InitializeEnemy()
    {
        // Get components
        _renderer = GetComponent<Renderer>();
        _collider = GetComponent<Collider>();
        
        if (_renderer != null)
        {
            _originalColor = _renderer.material.color;
        }
        
        // Calculate level-based stats
        _maxHealth = BaseHealth + (HealthPerLevel * (Level - 1));
        _currentHealth = _maxHealth;
        _currentDamage = BaseDamage + (DamagePerLevel * (Level - 1));
        
        Debug.Log($"[EnemyBase] {EnemyName} Level {Level} initialized - Health: {_maxHealth}, Damage: {_currentDamage}");
        
        // Trigger initial health event
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
    }
    
    /// <summary>
    /// Deal damage to this enemy
    /// </summary>
    /// <param name="damage">Amount of damage to deal</param>
    /// <param name="source">Source of the damage (optional)</param>
    public void TakeDamage(float damage, GameObject source = null)
    {
        if (_isDead || IsInvulnerable)
        {
            Debug.Log($"[EnemyBase] {EnemyName} is dead or invulnerable, ignoring {damage} damage");
            return;
        }
        
        // Apply damage
        _currentHealth -= damage;
        _currentHealth = Mathf.Max(0, _currentHealth);
        
        Debug.Log($"[EnemyBase] {EnemyName} took {damage} damage. Health: {_currentHealth}/{_maxHealth}");
        
        // Trigger events
        OnDamageTaken?.Invoke(damage);
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        
        // Visual feedback
        StartCoroutine(DamageFlash());
        
        // Check for death
        if (_currentHealth <= 0 && !_isDead)
        {
            Die();
        }
    }
    
    /// <summary>
    /// Heal this enemy
    /// </summary>
    /// <param name="healAmount">Amount to heal</param>
    public void Heal(float healAmount)
    {
        if (_isDead) return;
        
        _currentHealth += healAmount;
        _currentHealth = Mathf.Min(_maxHealth, _currentHealth);
        
        Debug.Log($"[EnemyBase] {EnemyName} healed for {healAmount}. Health: {_currentHealth}/{_maxHealth}");
        
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
    }
    
    /// <summary>
    /// Get the damage this enemy deals
    /// </summary>
    public float GetDamage()
    {
        return _currentDamage;
    }
    
    /// <summary>
    /// Get current health percentage (0-1)
    /// </summary>
    public float GetHealthPercentage()
    {
        return _maxHealth > 0 ? _currentHealth / _maxHealth : 0f;
    }
    
    /// <summary>
    /// Check if enemy is alive
    /// </summary>
    public bool IsAlive()
    {
        return !_isDead && _currentHealth > 0;
    }
    
    /// <summary>
    /// Set enemy level and recalculate stats
    /// </summary>
    public void SetLevel(int newLevel)
    {
        Level = Mathf.Max(1, newLevel);
        
        // Recalculate stats
        float healthPercentage = GetHealthPercentage();
        _maxHealth = BaseHealth + (HealthPerLevel * (Level - 1));
        _currentHealth = _maxHealth * healthPercentage; // Maintain health percentage
        _currentDamage = BaseDamage + (DamagePerLevel * (Level - 1));
        
        Debug.Log($"[EnemyBase] {EnemyName} level set to {Level} - Health: {_maxHealth}, Damage: {_currentDamage}");
        
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
    }
    
    private void Die()
    {
        _isDead = true;
        _currentHealth = 0;
        
        Debug.Log($"[EnemyBase] {EnemyName} has died!");
        
        // Disable collision
        if (_collider != null)
        {
            _collider.enabled = false;
        }
        
        // Visual changes for death
        if (_renderer != null)
        {
            Color deathColor = _originalColor;
            deathColor.a = 0.5f; // Semi-transparent
            _renderer.material.color = deathColor;
        }
        
        // Trigger death event
        OnEnemyDeath?.Invoke(this);
        
        // Optional: Destroy after delay
        // Destroy(gameObject, 3f);
    }
    
    private System.Collections.IEnumerator DamageFlash()
    {
        if (_renderer == null) yield break;
        
        // Flash red
        Color flashColor = Color.red;
        flashColor.a = _originalColor.a;
        _renderer.material.color = flashColor;
        
        yield return new WaitForSeconds(DamageFlashDuration);
        
        // Return to original color (unless dead)
        if (!_isDead)
        {
            _renderer.material.color = _originalColor;
        }
    }
    
    /// <summary>
    /// Reset enemy to full health and alive state
    /// </summary>
    public void Reset()
    {
        _isDead = false;
        _currentHealth = _maxHealth;
        
        if (_collider != null)
        {
            _collider.enabled = true;
        }
        
        if (_renderer != null)
        {
            _renderer.material.color = _originalColor;
        }
        
        Debug.Log($"[EnemyBase] {EnemyName} reset to full health");
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
    }
    
    // Debug info for inspector
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        
        Level = Mathf.Max(1, Level);
        BaseHealth = Mathf.Max(1, BaseHealth);
        BaseDamage = Mathf.Max(0, BaseDamage);
    }
    
    // Gizmos for editor visualization
    private void OnDrawGizmosSelected()
    {
        // Draw health bar above enemy
        Vector3 healthBarPos = transform.position + Vector3.up * 2f;
        float healthPercentage = Application.isPlaying ? GetHealthPercentage() : 1f;
        
        // Background
        Gizmos.color = Color.red;
        Gizmos.DrawCube(healthBarPos, new Vector3(2f, 0.2f, 0.1f));
        
        // Health
        Gizmos.color = Color.green;
        Vector3 healthBarSize = new Vector3(2f * healthPercentage, 0.2f, 0.1f);
        Vector3 healthBarOffset = new Vector3(-(1f - healthPercentage), 0, 0);
        Gizmos.DrawCube(healthBarPos + healthBarOffset, healthBarSize);
        
        // Level indicator
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 2.5f, 0.3f);
    }
}