using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CombatSystem : MonoBehaviour
{
    [Header("Combat Effects")]
    public ParticleSystem AttackEffect;
    public ParticleSystem HitEffect;
    public AudioClip AttackSound;
    public AudioClip HitSound;

    [Header("Projectile Settings")]
    public GameObject ProjectilePrefab;
    public Transform ProjectileSpawnPoint;
    
    [Header("Screen Effects")]
    public float ScreenShakeIntensity = 0.1f;
    public float ScreenShakeDuration = 0.2f;

    private AudioSource _audioSource;
    private Camera _mainCamera;
    private Vector3 _originalCameraPos;
    private bool _isShaking = false;

    private void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        _mainCamera = Camera.main;
        if (_mainCamera != null)
        {
            _originalCameraPos = _mainCamera.transform.localPosition;
        }

        Debug.Log("CombatSystem initialized");
    }

    public void PlayAttackEffect(Vector3 attackerPos, Vector3 targetPos)
    {
        Debug.Log($"[CombatSystem] PlayAttackEffect called - this should only be used as fallback");
        Debug.Log($"Attack effect: {attackerPos} -> {targetPos}");
        
        // This method should only be used as a fallback when weapon data is not available
        // Default to melee attack with standard range since most weapons are melee
        Debug.LogWarning("[CombatSystem] Using fallback attack effect - weapon type unknown, defaulting to melee");
        PlayMeleeAttackEffect(attackerPos, targetPos, 2.0f); // Default melee range
    }
    
    /// <summary>
    /// Play attack effect with weapon data (enhanced version)
    /// </summary>
    public void PlayAttackEffectWithWeapon(Vector3 attackerPos, Vector3 targetPos, string weaponType, float weaponRange)
    {
        Debug.Log($"[CombatSystem] 🎯 PlayAttackEffectWithWeapon called: {attackerPos} -> {targetPos}");
        Debug.Log($"[CombatSystem] 🔍 Weapon Type: '{weaponType}' | Range: {weaponRange}");
        
        float attackDistance = Vector3.Distance(attackerPos, targetPos);
        Debug.Log($"[CombatSystem] 📏 Attack Distance: {attackDistance:F2} units");
        
        if (weaponType == "Ranged")
        {
            Debug.Log($"[CombatSystem] 🏹 RANGED WEAPON DETECTED - Should fire projectile!");
            Debug.Log($"[CombatSystem] ➡️ Calling FireProjectileWithWeaponData()");
            FireProjectileWithWeaponData(attackerPos, targetPos, weaponRange);
        }
        else
        {
            Debug.Log($"[CombatSystem] ⚔️ MELEE WEAPON DETECTED - Should show swipe!");
            Debug.Log($"[CombatSystem] 📏 Melee Range: {weaponRange}, Distance: {attackDistance:F2}");
            
            if (attackDistance <= weaponRange + 0.5f) // Small tolerance for player movement/lag
            {
                Debug.Log($"[CombatSystem] ✅ Target within melee range - playing swipe effect");
                PlayMeleeAttackEffect(attackerPos, targetPos, weaponRange);
            }
            else
            {
                Debug.LogWarning($"[CombatSystem] ⚠️ Target too far for melee weapon! Distance: {attackDistance:F2}, Max Range: {weaponRange}");
                // Still play the effect but limit it to weapon range
                Vector3 direction = (targetPos - attackerPos).normalized;
                Vector3 limitedTarget = attackerPos + direction * weaponRange;
                Debug.Log($"[CombatSystem] 🎯 Playing limited swipe to max range: {limitedTarget}");
                PlayMeleeAttackEffect(attackerPos, limitedTarget, weaponRange);
            }
        }
    }
    
    private void FireProjectileWithWeaponData(Vector3 attackerPos, Vector3 targetPos, float weaponRange)
    {
        // Enhanced projectile firing with actual weapon data
        Vector3 spawnPos;
        if (ProjectileSpawnPoint != null)
        {
            spawnPos = ProjectileSpawnPoint.position + Vector3.up * 0.5f;
        }
        else
        {
            spawnPos = attackerPos + Vector3.up * 0.5f;
        }
        
        Debug.Log($"[CombatSystem] Firing projectile with weapon data: Range={weaponRange}, From={spawnPos} To={targetPos}");
        
        GameObject projectileObj;
        
        if (ProjectilePrefab != null)
        {
            projectileObj = Instantiate(ProjectilePrefab, spawnPos, Quaternion.identity);
            Debug.Log("[CombatSystem] Firing projectile from prefab");
        }
        else
        {
            projectileObj = CreateProjectileDynamically(spawnPos);
            Debug.Log("[CombatSystem] Firing projectile created dynamically");
        }
        
        var projectile = projectileObj.GetComponent<Projectile>();
        
        if (projectile != null)
        {
            // Use weapon data for projectile initialization
            float projectileSpeed = 20f; // Default speed if not provided
            float accuracy = 0.7f; // Default accuracy if not provided
            
            projectile.Initialize(
                targetPos,
                projectileSpeed,
                weaponRange,
                accuracy
            );
            
            Debug.Log($"[CombatSystem] Projectile initialized with weapon data: Speed={projectileSpeed}, Range={weaponRange}, Accuracy={accuracy}");
        }
        else
        {
            Debug.LogError("[CombatSystem] Projectile object does not have Projectile component!");
            Destroy(projectileObj);
            PlayMeleeAttackEffect(attackerPos, targetPos, 2.0f); // Fallback to melee
        }
    }
    
    private void FireProjectileWithDefaults(Vector3 attackerPos, Vector3 targetPos)
    {
        // Determine spawn position with better positioning
        Vector3 spawnPos;
        if (ProjectileSpawnPoint != null)
        {
            spawnPos = ProjectileSpawnPoint.position + Vector3.up * 0.5f; // Spawn slightly above player
        }
        else
        {
            spawnPos = attackerPos + Vector3.up * 0.5f; // Spawn slightly above attack position
        }
        
        Debug.Log($"[CombatSystem] Firing projectile from {spawnPos} to {targetPos}");
        Debug.Log($"[CombatSystem] Attack distance: {Vector3.Distance(attackerPos, targetPos):F2}");
        
        // Create projectile dynamically (following loot system pattern)
        GameObject projectileObj;
        
        if (ProjectilePrefab != null)
        {
            // Use prefab if available
            projectileObj = Instantiate(ProjectilePrefab, spawnPos, Quaternion.identity);
            Debug.Log("[CombatSystem] Firing projectile from prefab");
        }
        else
        {
            // Fallback: create projectile dynamically like loot system does
            projectileObj = CreateProjectileDynamically(spawnPos);
            Debug.Log("[CombatSystem] Firing projectile created dynamically");
        }
        
        Debug.Log($"[CombatSystem] Projectile object created: {projectileObj.name} at position {projectileObj.transform.position}");
        Debug.Log($"[CombatSystem] Projectile active: {projectileObj.activeInHierarchy}, scale: {projectileObj.transform.localScale}");
        
        var projectile = projectileObj.GetComponent<Projectile>();
        
        if (projectile != null)
        {
            // Use default values for now - these should match wooden bow stats
            projectile.Initialize(
                targetPos,
                20f,  // Default projectile speed
                25f,  // Default range (matches wooden bow)
                0.7f  // Default accuracy (matches wooden bow)
            );
            
            Debug.Log($"[CombatSystem] Projectile initialized: Speed=20, Range=25, Accuracy=0.7");
        }
        else
        {
            Debug.LogError("[CombatSystem] Projectile object does not have Projectile component!");
            Destroy(projectileObj);
            PlayMeleeAttackEffect(attackerPos, targetPos, 2.0f); // Default melee range for fallback
        }
    }
    
    private GameObject CreateProjectileDynamically(Vector3 spawnPos)
    {
        Debug.Log($"[CombatSystem] Creating projectile dynamically at position: {spawnPos}");
        
        // Create projectile similar to how loot system creates objects
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = "Projectile_Dynamic";
        projectile.transform.position = spawnPos;
        
        Debug.Log($"[CombatSystem] Base sphere created at: {projectile.transform.position}");
        
        // Scale it down to be arrow-like but keep it visible
        projectile.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f); // Made larger for visibility
        
        Debug.Log($"[CombatSystem] Projectile scaled to: {projectile.transform.localScale}");
        
        // Set color to make it very visible - similar to enemy setup
        var renderer = projectile.GetComponent<Renderer>();
        if (renderer != null)
        {
            // Create a new material to ensure visibility
            Material projectileMaterial = new Material(Shader.Find("Standard"));
            projectileMaterial.color = Color.red; // Bright red for high visibility
            projectileMaterial.SetFloat("_Metallic", 0.0f);
            projectileMaterial.SetFloat("_Glossiness", 0.5f);
            renderer.material = projectileMaterial;
            Debug.Log("[CombatSystem] Set bright red material for visibility");
        }
        else
        {
            Debug.LogError("[CombatSystem] No renderer found on projectile!");
        }
        
        // Add Projectile component
        var projectileComponent = projectile.AddComponent<Projectile>();
        Debug.Log($"[CombatSystem] Projectile component added: {projectileComponent != null}");
        
        // Add a TrailRenderer for visual effect
        var trailRenderer = projectile.AddComponent<TrailRenderer>();
        trailRenderer.time = 1.0f; // Longer trail for visibility
        trailRenderer.startWidth = 0.2f; // Wider trail
        trailRenderer.endWidth = 0.05f;
        trailRenderer.material = CreateTrailMaterial();
        Debug.Log("[CombatSystem] TrailRenderer added");
        
        // Add Rigidbody but make it kinematic (we'll control movement manually)
        var rigidbody = projectile.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;
        Debug.Log("[CombatSystem] Rigidbody added (kinematic)");
        
        // Remove the default collider and add a trigger collider
        var originalCollider = projectile.GetComponent<SphereCollider>();
        if (originalCollider != null)
        {
            DestroyImmediate(originalCollider);
        }
        var collider = projectile.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 0.15f; // Slightly larger for better collision
        Debug.Log("[CombatSystem] Trigger collider added");
        
        // Add AudioSource for sound effects
        var audioSource = projectile.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1.0f; // 3D sound
        Debug.Log("[CombatSystem] AudioSource added");
        
        Debug.Log($"[CombatSystem] Projectile creation completed - Final position: {projectile.transform.position}, Active: {projectile.activeInHierarchy}");
        return projectile;
    }
    
    private Material CreateTrailMaterial()
    {
        // Create a simple trail material
        Material trailMaterial = new Material(Shader.Find("Sprites/Default"));
        trailMaterial.color = new Color(1f, 0.8f, 0.2f, 0.8f); // Orange-yellow trail
        return trailMaterial;
    }
    
    private void PlayMeleeAttackEffect(Vector3 attackerPos, Vector3 targetPos, float weaponRange)
    {
        // Create the visual swipe effect
        CreateMeleeSwipeEffect(attackerPos, targetPos, weaponRange);
        
        // Play the original particle effect if available
        if (AttackEffect != null)
        {
            var effect = Instantiate(AttackEffect, attackerPos, Quaternion.LookRotation(targetPos - attackerPos));
            Destroy(effect.gameObject, 2f);
        }

        if (AttackSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(AttackSound);
        }
    }
    
    private void CreateMeleeSwipeEffect(Vector3 attackerPos, Vector3 targetPos, float weaponRange)
    {
        Debug.Log($"[CombatSystem] Creating melee swipe effect: Range={weaponRange}, From={attackerPos} To={targetPos}");
        
        // Create a temporary GameObject for the swipe effect
        GameObject swipeEffectObj = new GameObject("MeleeSwipeEffect");
        swipeEffectObj.transform.position = attackerPos;
        
        // Add the MeleeSwipeEffect component
        var swipeEffect = swipeEffectObj.AddComponent<MeleeSwipeEffect>();
        
        // Configure swipe parameters based on weapon type/range
        float swipeWidth = CalculateSwipeWidth(weaponRange);
        float swipeDuration = CalculateSwipeDuration(weaponRange);
        float swipeThickness = CalculateSwipeThickness(weaponRange);
        
        swipeEffect.SetSwipeParameters(swipeWidth, swipeDuration, swipeThickness);
        
        Debug.Log($"[CombatSystem] Swipe parameters: Width={swipeWidth}°, Duration={swipeDuration}s, Thickness={swipeThickness}");
        
        // Play the swipe effect
        swipeEffect.PlaySwipeEffect(attackerPos, targetPos, weaponRange);
    }
    
    private float CalculateSwipeWidth(float weaponRange)
    {
        // Larger weapons have wider swipes
        // Range 1.5-2.5 units -> 45-75 degrees
        return Mathf.Lerp(45f, 75f, (weaponRange - 1.5f) / 1.0f);
    }
    
    private float CalculateSwipeDuration(float weaponRange)
    {
        // Larger weapons have slightly longer swipe animations
        // Range 1.5-2.5 units -> 0.25-0.4 seconds
        return Mathf.Lerp(0.25f, 0.4f, (weaponRange - 1.5f) / 1.0f);
    }
    
    private float CalculateSwipeThickness(float weaponRange)
    {
        // Larger weapons have thicker swipe effects
        // Range 1.5-2.5 units -> 0.15-0.3 thickness
        return Mathf.Lerp(0.15f, 0.3f, (weaponRange - 1.5f) / 1.0f);
    }
    

    public void PlayDamageEffect(float damage)
    {
        Debug.Log($"Damage effect: {damage}");
        
        if (HitEffect != null && PlayerController.Instance != null)
        {
            var effect = Instantiate(HitEffect, PlayerController.Instance.transform.position, Quaternion.identity);
            Destroy(effect.gameObject, 2f);
        }

        if (HitSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(HitSound);
        }

        PlayScreenShake();
    }

    public void PlayScreenShake()
    {
        if (!_isShaking && _mainCamera != null)
        {
            StartCoroutine(ScreenShakeCoroutine());
        }
    }

    public void UpdateCombatEffect(CombatEffect effect)
    {
        Debug.Log($"Updating combat effect: {effect.EffectType}");
    }

    private IEnumerator ScreenShakeCoroutine()
    {
        _isShaking = true;
        float elapsed = 0f;

        while (elapsed < ScreenShakeDuration)
        {
            float strength = ScreenShakeIntensity * (1f - (elapsed / ScreenShakeDuration));
            Vector3 randomOffset = Random.insideUnitSphere * strength;
            randomOffset.z = 0;
            
            if (_mainCamera != null)
            {
                _mainCamera.transform.localPosition = _originalCameraPos + randomOffset;
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (_mainCamera != null)
        {
            _mainCamera.transform.localPosition = _originalCameraPos;
        }
        _isShaking = false;
    }

    #region Phase 1: New Projectile Collision System

    /// <summary>
    /// Spawn visual projectile for collision-based damage system (single projectile)
    /// </summary>
    public void SpawnProjectile(string projectileId, Vector3Data launchPosition, Vector3Data targetPosition, NetworkMessages.ProjectileWeaponData weaponData)
    {
        Debug.Log($"[CombatSystem] SpawnProjectile (single): {projectileId} from {launchPosition.X},{launchPosition.Y},{launchPosition.Z} to {targetPosition.X},{targetPosition.Y},{targetPosition.Z}");

        Vector3 launchPos = launchPosition.ToVector3();
        Vector3 targetPos = targetPosition.ToVector3();

        // Create and configure single projectile
        SpawnIndividualProjectile(projectileId, launchPos, targetPos, weaponData.ProjectileSpeed, weaponData.WeaponRange);
    }

    /// <summary>
    /// Phase 3: Spawn multiple visual projectiles for multi-projectile weapons
    /// </summary>
    public void SpawnMultipleProjectiles(List<NetworkMessages.ProjectileData> projectiles, NetworkMessages.ProjectileWeaponData weaponData)
    {
        Debug.Log($"[CombatSystem] SpawnMultipleProjectiles: {projectiles.Count} projectiles");

        foreach (var projectileData in projectiles)
        {
            Vector3 launchPos = projectileData.LaunchPosition.ToVector3();
            Vector3 targetPos = projectileData.TargetPosition.ToVector3();

            // Apply individual projectile modifiers
            float speed = weaponData.ProjectileSpeed * projectileData.SpeedMultiplier;
            float range = weaponData.WeaponRange; // Range doesn't change per projectile

            SpawnIndividualProjectile(projectileData.ProjectileId, launchPos, targetPos, speed, range);
        }
    }

    /// <summary>
    /// Phase 3: Helper method to spawn individual projectile
    /// </summary>
    private void SpawnIndividualProjectile(string projectileId, Vector3 launchPos, Vector3 targetPos, float speed, float range)
    {
        // Create projectile dynamically (reuse existing method)
        GameObject projectileObj = CreateProjectileDynamically(launchPos);
        if (projectileObj == null)
        {
            Debug.LogError("[CombatSystem] Failed to create projectile!");
            return;
        }

        // Add or get Projectile component
        Projectile projectileComponent = projectileObj.GetComponent<Projectile>();
        if (projectileComponent == null)
        {
            projectileComponent = projectileObj.AddComponent<Projectile>();
        }

        // Configure projectile with server data
        projectileComponent.Initialize(
            projectileId,
            launchPos,
            targetPos,
            speed,
            range
        );

        Debug.Log($"[CombatSystem] ✅ Projectile {projectileId} spawned and initialized (Speed: {speed:F1}, Range: {range:F1})");
    }

    /// <summary>
    /// Play damage effects when server confirms damage
    /// </summary>
    public void PlayDamageEffect(Vector3Data damagePosition, float damage, bool isCritical)
    {
        Vector3 effectPos = damagePosition.ToVector3();
        
        Debug.Log($"[CombatSystem] Playing damage effect at {effectPos} - Damage: {damage}, Critical: {isCritical}");
        
        // Create simple damage effect (can be enhanced later)
        GameObject effect = GameObject.CreatePrimitive(PrimitiveType.Cube);
        effect.name = "DamageEffect";
        effect.transform.position = effectPos;
        effect.transform.localScale = Vector3.one * 0.2f;
        
        // Color based on critical hit
        var renderer = effect.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material effectMaterial = new Material(Shader.Find("Standard"));
            effectMaterial.color = isCritical ? Color.yellow : Color.white;
            renderer.material = effectMaterial;
        }
        
        // Camera shake for damage
        // StartCoroutine(CameraShake(0.1f, isCritical ? 0.3f : 0.1f)); // TODO: Implement CameraShake
        
        // Destroy effect after short duration
        Destroy(effect, 0.5f);
    }

    #endregion
}