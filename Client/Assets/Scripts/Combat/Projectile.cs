using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Header("Projectile Properties")]
    public float Speed = 20f;
    public float Range = 25f;
    public float Accuracy = 1.0f;
    public string WeaponType = "Ranged";
    
    [Header("Visual Effects")]
    public TrailRenderer Trail;
    public ParticleSystem HitEffect;
    public AudioClip ProjectileSound;
    public AudioClip HitSound;
    
    private Vector3 _startPosition;
    private Vector3 _targetPosition;
    private Vector3 _direction;
    private float _distanceTraveled = 0f;
    private bool _hasHit = false;
    private AudioSource _audioSource;
    
    // Phase 1: New collision-based damage system
    private string _projectileId = string.Empty;
    private bool _isCollisionBased = false;
    
    // Event for when projectile hits target or max range
    public delegate void ProjectileHitHandler(Vector3 hitPosition, bool hitTarget);
    public event ProjectileHitHandler OnProjectileHit;
    
    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
    }
    
    private void Start()
    {
        _startPosition = transform.position;
        
        // Apply accuracy - perfect accuracy (1.0) means no deviation
        if (Accuracy < 1.0f)
        {
            float maxDeviation = (1.0f - Accuracy) * 5f; // Max 5 degree deviation for 0 accuracy
            float deviationAngle = Random.Range(-maxDeviation, maxDeviation);
            Vector3 deviatedDirection = Quaternion.AngleAxis(deviationAngle, Vector3.up) * _direction;
            _direction = deviatedDirection.normalized;
        }
        
        // Orient projectile to face direction of travel
        transform.LookAt(transform.position + _direction);
        
        // Play projectile launch sound
        if (ProjectileSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(ProjectileSound);
        }
        
        Debug.Log($"Projectile launched: Speed={Speed}, Range={Range}, Accuracy={Accuracy}");
    }
    
    private void Update()
    {
        if (_hasHit) return;
        
        // Move projectile
        Vector3 movement = _direction * Speed * Time.deltaTime;
        transform.Translate(movement, Space.World);
        _distanceTraveled += movement.magnitude;
        
        // Check if projectile has traveled max range
        if (_distanceTraveled >= Range)
        {
            HitTarget(transform.position, false);
            return;
        }
        
        // Raycast to check for collisions
        RaycastHit hit;
        if (Physics.Raycast(transform.position, _direction, out hit, Speed * Time.deltaTime))
        {
            // Check what we hit
            bool hitValidTarget = false;
            
            // Check for enemies
            var enemy = hit.collider.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                hitValidTarget = true;
                Debug.Log($"Projectile hit enemy: {enemy.EnemyId}");
            }
            
            // Check for other players (if PvP is enabled)
            var player = hit.collider.GetComponent<RemotePlayer>();
            if (player != null)
            {
                hitValidTarget = true;
                Debug.Log($"Projectile hit player: {player.PlayerId}");
            }
            
            // Check for terrain/obstacles (using safe tag comparison)
            if (hit.collider.gameObject.tag == "Terrain" || hit.collider.gameObject.tag == "Obstacle" || 
                hit.collider.gameObject.name.Contains("Ground") || hit.collider.gameObject.name.Contains("Terrain"))
            {
                Debug.Log("Projectile hit terrain/obstacle");
            }
            
            HitTarget(hit.point, hitValidTarget);
        }
    }
    
    public void Initialize(Vector3 targetPosition, float speed, float range, float accuracy)
    {
        _targetPosition = targetPosition;
        Speed = speed;
        Range = range;
        Accuracy = accuracy;
        _isCollisionBased = false;
        
        // Calculate direction to target
        _direction = (targetPosition - transform.position).normalized;
    }
    
    /// <summary>
    /// Initialize projectile for Phase 1 collision-based damage system
    /// </summary>
    public void Initialize(string projectileId, Vector3 launchPosition, Vector3 targetPosition, float speed, float range)
    {
        _projectileId = projectileId;
        _targetPosition = targetPosition;
        Speed = speed;
        Range = range;
        _isCollisionBased = true;
        
        Debug.Log($"[Projectile] Initialized collision-based projectile: {projectileId}");
        
        // Calculate direction to target
        _direction = (targetPosition - launchPosition).normalized;
        
        // Set position (may be different from current transform position)
        transform.position = launchPosition;
        _startPosition = launchPosition;
        
        Debug.Log($"[Projectile] Direction: {_direction}, Speed: {speed}, Range: {range}");
    }
    
    private void HitTarget(Vector3 hitPosition, bool hitValidTarget)
    {
        if (_hasHit) return;
        _hasHit = true;
        
        // Stop the projectile
        if (Trail != null)
        {
            Trail.enabled = false;
        }
        
        // Play hit effect
        if (HitEffect != null)
        {
            var effect = Instantiate(HitEffect, hitPosition, Quaternion.identity);
            Destroy(effect.gameObject, 2f);
        }
        
        // Play hit sound
        if (HitSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(HitSound);
        }
        
        // Phase 1: Send collision report to server for collision-based projectiles
        if (_isCollisionBased && !string.IsNullOrEmpty(_projectileId))
        {
            SendCollisionReportToServer(hitPosition, hitValidTarget);
        }
        
        // Notify listeners
        OnProjectileHit?.Invoke(hitPosition, hitValidTarget);
        
        Debug.Log($"Projectile hit at {hitPosition}, valid target: {hitValidTarget}");
        
        // Destroy projectile after a short delay to allow effects to play
        Destroy(gameObject, 1f);
    }
    
    #region Phase 1: Collision-Based Damage System

    /// <summary>
    /// Send collision report to server for damage validation
    /// </summary>
    private async void SendCollisionReportToServer(Vector3 hitPosition, bool hitValidTarget)
    {
        try
        {
            // Determine what was hit and build collision report
            string targetId = "";
            string targetType = "Terrain";
            string collisionContext = "";

            if (hitValidTarget)
            {
                // Try to identify the specific target that was hit
                Collider[] colliders = Physics.OverlapSphere(hitPosition, 0.1f);
                foreach (var collider in colliders)
                {
                    var enemy = collider.GetComponent<EnemyBase>();
                    if (enemy != null)
                    {
                        targetId = enemy.EnemyId;
                        targetType = "Enemy";
                        collisionContext = $"Enemy:{enemy.EnemyName}";
                        break;
                    }

                    var player = collider.GetComponent<RemotePlayer>();
                    if (player != null)
                    {
                        targetId = player.PlayerId;
                        targetType = "Player";
                        collisionContext = $"Player:{player.PlayerName}";
                        break;
                    }
                }
            }
            else
            {
                collisionContext = "TerrainHit";
            }

            Debug.Log($"[Projectile] Sending collision report: {_projectileId} hit {targetType} {targetId} at {hitPosition}");

            // Send collision report to server
            var networkManager = GameManager.Instance?.NetworkManager ?? FindObjectOfType<NetworkManager>();
            if (networkManager != null)
            {
                await networkManager.SendProjectileHit(
                    _projectileId,
                    targetId,
                    targetType,
                    hitPosition,
                    collisionContext
                );
            }
            else
            {
                Debug.LogError("[Projectile] NetworkManager not found - cannot send collision report");
            }

            Debug.Log($"[Projectile] ✅ Collision report sent successfully");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Projectile] Failed to send collision report: {ex.Message}");
        }
    }

    #endregion

    private void OnDestroy()
    {
        Debug.Log("Projectile destroyed");
    }
}