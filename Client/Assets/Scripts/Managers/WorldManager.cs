using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class WorldManager : MonoBehaviour
{
    [Header("World Settings")]
    public GameObject RemotePlayerPrefab;
    public GameObject ResourceNodePrefab;
    public float InterpolationRate = 15f;

    // World State
    private Dictionary<string, RemotePlayer> _remotePlayers = new Dictionary<string, RemotePlayer>();
    private Dictionary<string, ResourceNodeClient> _resourceNodes = new Dictionary<string, ResourceNodeClient>();
    private List<CombatEffect> _activeCombatEffects = new List<CombatEffect>();

    // Object Pooling
    private Queue<GameObject> _playerPool = new Queue<GameObject>();
    private Queue<GameObject> _resourcePool = new Queue<GameObject>();

    private void Start()
    {
        // Subscribe to network events
        NetworkManager.OnPlayerMoved += HandlePlayerMoved;
        NetworkManager.OnWorldUpdate += HandleWorldUpdate;
        NetworkManager.OnPlayerJoined += HandlePlayerJoined;
        NetworkManager.OnCombatAction += HandleCombatAction;
        
        // Phase 1: Subscribe to new projectile collision system events
        NetworkManager.OnProjectileLaunch += HandleProjectileLaunch;
        NetworkManager.OnDamageConfirmation += HandleDamageConfirmation;

        // Pre-populate object pools
        InitializeObjectPools();
    }

    private void InitializeObjectPools()
    {
        // Pre-create player objects for performance
        if (RemotePlayerPrefab != null)
        {
            for (int i = 0; i < 30; i++)
            {
                var playerObj = Instantiate(RemotePlayerPrefab);
                playerObj.SetActive(false);
                _playerPool.Enqueue(playerObj);
            }
        }

        // Pre-create resource objects
        if (ResourceNodePrefab != null)
        {
            for (int i = 0; i < 100; i++)
            {
                var resourceObj = Instantiate(ResourceNodePrefab);
                resourceObj.SetActive(false);
                _resourcePool.Enqueue(resourceObj);
            }
        }
    }

    private void HandlePlayerMoved(PlayerState playerState)
    {
        // Don't update local player position from network
        if (playerState.PlayerId == GameManager.Instance.LocalPlayerId)
            return;

        // Update or create remote player
        if (_remotePlayers.TryGetValue(playerState.PlayerId, out var remotePlayer))
        {
            remotePlayer.UpdateState(playerState);
        }
        else
        {
            CreateRemotePlayer(playerState);
        }
    }

    private void HandleWorldUpdate(NetworkMessages.WorldUpdateMessage worldUpdate)
    {
        // Update all nearby players
        if (worldUpdate.NearbyPlayers != null)
        {
            foreach (var playerState in worldUpdate.NearbyPlayers)
            {
                if (playerState.PlayerId != GameManager.Instance.LocalPlayerId)
                {
                    HandlePlayerMoved(playerState);
                }
            }
        }

        // Update resource nodes
        if (worldUpdate.NearbyResources != null)
        {
            UpdateResourceNodes(worldUpdate.NearbyResources);
        }

        // Update combat effects
        if (worldUpdate.ActiveEffects != null)
        {
            UpdateCombatEffects(worldUpdate.ActiveEffects);
        }

        // Update server time offset
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        GameManager.Instance.ServerTimeOffset = (worldUpdate.ServerTimestamp - currentTime) / 1000f;
    }

    private void HandlePlayerJoined(NetworkMessages.PlayerJoinNotification joinNotification)
    {
        Debug.Log($"Player joined: {joinNotification.PlayerName}");
        
        // Show join notification in UI
        if (GameManager.Instance.UIManager != null)
        {
            GameManager.Instance.UIManager.ShowNotification(
                $"{joinNotification.PlayerName} joined the game!", 
                Color.green
            );
        }
    }

    private void HandleCombatAction(NetworkMessages.CombatActionMessage combatAction)
    {
        Debug.Log($"[WorldManager] CombatAction received - AttackerId: {combatAction.AttackerId}, LocalPlayerId: {GameManager.Instance.LocalPlayerId}");
        
        // Play combat visual effects for confirmed attacks
        if (combatAction.AttackerId == GameManager.Instance.LocalPlayerId)
        {
            Debug.Log("[WorldManager] ✅ Local player's attack confirmed by server - play animation");
            
            // Local player's attack confirmed by server - play animation
            PlayerController localPlayerController = GameManager.Instance.LocalPlayer;
            CombatSystem combatSystem = GameManager.Instance.CombatSystem;
            Debug.Log($"[WorldManager] LocalPlayer: {(localPlayerController != null ? "Found" : "NULL")}, CombatSystem: {(combatSystem != null ? "Found" : "NULL")}");
            
            // Try to get components even if GameManager references are null
            if (localPlayerController == null) 
            {
                localPlayerController = FindObjectOfType<PlayerController>();
                Debug.Log($"[WorldManager] Found LocalPlayer via FindObjectOfType: {(localPlayerController != null ? "Found" : "Still NULL")}");
            }
            
            if (combatSystem == null)
            {
                combatSystem = FindObjectOfType<CombatSystem>();
                Debug.Log($"[WorldManager] Found CombatSystem via FindObjectOfType: {(combatSystem != null ? "Found" : "Still NULL")}");
            }
            
            if (localPlayerController != null && combatSystem != null)
            {
                Vector3 attackerPos = localPlayerController.transform.position;
                Debug.Log($"[WorldManager] Attacker position: {attackerPos}");
                
                // Get the actual equipped weapon from PlayerController
                var equippedWeapon = localPlayerController.GetEquippedWeapon();
                
                if (equippedWeapon != null)
                {
                    Debug.Log($"[WorldManager] 🗡️ Playing attack with weapon: {equippedWeapon.ItemName}, Type: {equippedWeapon.WeaponType}, Range: {equippedWeapon.WeaponRange}");
                    combatSystem.PlayAttackEffectWithWeapon(
                        attackerPos, 
                        combatAction.Position, 
                        equippedWeapon.WeaponType, 
                        equippedWeapon.WeaponRange
                    );
                    Debug.Log("[WorldManager] ✅ Attack animation triggered successfully");
                }
                else
                {
                    Debug.Log("[WorldManager] 👊 Playing unarmed attack");
                    // Unarmed attack
                    combatSystem.PlayAttackEffectWithWeapon(
                        attackerPos, 
                        combatAction.Position, 
                        "Melee", 
                        1.5f
                    );
                    Debug.Log("[WorldManager] ✅ Unarmed attack animation triggered successfully");
                }
            }
            else
            {
                Debug.LogError("[WorldManager] ❌ Cannot play attack animation - missing LocalPlayer or CombatSystem");
            }
        }
        else if (_remotePlayers.TryGetValue(combatAction.AttackerId, out var attacker))
        {
            // Remote player attack
            Vector3 attackerPos = attacker.transform.position;
            if (GameManager.Instance.CombatSystem != null)
            {
                GameManager.Instance.CombatSystem.PlayAttackEffect(attackerPos, combatAction.Position);
            }
        }

        // Handle damage effects on target
        if (!string.IsNullOrEmpty(combatAction.TargetId))
        {
            if (combatAction.TargetId == GameManager.Instance.LocalPlayerId)
            {
                // Local player took damage
                if (GameManager.Instance.CombatSystem != null)
                {
                    GameManager.Instance.CombatSystem.PlayDamageEffect(combatAction.Damage);
                }
            }
            else if (_remotePlayers.TryGetValue(combatAction.TargetId, out var target))
            {
                // Remote player took damage
                target.PlayDamageEffect(combatAction.Damage);
            }
        }
    }

    private void CreateRemotePlayer(PlayerState playerState)
    {
        GameObject playerObj;
        
        // Get from pool or create new
        if (_playerPool.Count > 0)
        {
            playerObj = _playerPool.Dequeue();
            playerObj.SetActive(true);
        }
        else if (RemotePlayerPrefab != null)
        {
            playerObj = Instantiate(RemotePlayerPrefab);
        }
        else
        {
            Debug.LogError("RemotePlayerPrefab is not assigned!");
            return;
        }

        var remotePlayer = playerObj.GetComponent<RemotePlayer>();
        if (remotePlayer != null)
        {
            remotePlayer.Initialize(playerState);
            _remotePlayers[playerState.PlayerId] = remotePlayer;
        }
    }

    private void UpdateResourceNodes(List<ResourceNode> resources)
    {
        // Remove nodes that are no longer present
        var activeResourceIds = resources.Select(r => r.ResourceId).ToHashSet();
        var toRemove = _resourceNodes.Where(kvp => !activeResourceIds.Contains(kvp.Key)).ToList();
        
        foreach (var kvp in toRemove)
        {
            RemoveResourceNode(kvp.Key);
        }

        // Update or create resource nodes
        foreach (var resource in resources)
        {
            if (_resourceNodes.TryGetValue(resource.ResourceId, out var existingNode))
            {
                existingNode.UpdateResource(resource);
            }
            else
            {
                CreateResourceNode(resource);
            }
        }
    }

    private void CreateResourceNode(ResourceNode resource)
    {
        GameObject resourceObj;
        
        if (_resourcePool.Count > 0)
        {
            resourceObj = _resourcePool.Dequeue();
            resourceObj.SetActive(true);
        }
        else if (ResourceNodePrefab != null)
        {
            resourceObj = Instantiate(ResourceNodePrefab);
        }
        else
        {
            Debug.LogError("ResourceNodePrefab is not assigned!");
            return;
        }

        var resourceNodeClient = resourceObj.GetComponent<ResourceNodeClient>();
        if (resourceNodeClient != null)
        {
            resourceNodeClient.Initialize(resource);
            _resourceNodes[resource.ResourceId] = resourceNodeClient;
        }
    }

    private void RemoveResourceNode(string resourceId)
    {
        if (_resourceNodes.TryGetValue(resourceId, out var resourceNode))
        {
            resourceNode.gameObject.SetActive(false);
            _resourcePool.Enqueue(resourceNode.gameObject);
            _resourceNodes.Remove(resourceId);
        }
    }

    private void UpdateCombatEffects(List<CombatEffect> effects)
    {
        _activeCombatEffects = effects;
        
        // Update visual effects based on active combat effects
        foreach (var effect in effects)
        {
            if (GameManager.Instance.CombatSystem != null)
            {
                GameManager.Instance.CombatSystem.UpdateCombatEffect(effect);
            }
        }
    }

    public void RemovePlayer(string playerId)
    {
        if (_remotePlayers.TryGetValue(playerId, out var player))
        {
            player.gameObject.SetActive(false);
            _playerPool.Enqueue(player.gameObject);
            _remotePlayers.Remove(playerId);
        }
    }

    #region Phase 1: New Projectile Collision System Handlers

    /// <summary>
    /// Handle projectile launch messages from server to spawn visual projectiles
    /// </summary>
    private void HandleProjectileLaunch(NetworkMessages.ProjectileLaunchMessage launchMessage)
    {
        Debug.Log($"[WorldManager] ProjectileLaunch received: {launchMessage.ProjectileId} by {launchMessage.ShooterId}");
        
        // Only spawn visual projectiles for ranged attacks
        if (launchMessage.WeaponData.WeaponType == "Ranged")
        {
            CombatSystem combatSystem = GameManager.Instance.CombatSystem;
            if (combatSystem == null)
            {
                combatSystem = FindObjectOfType<CombatSystem>();
            }
            
            if (combatSystem != null)
            {
                Debug.Log($"[WorldManager] 🏹 Spawning projectile: {launchMessage.ProjectileId}");
                
                // Spawn visual projectile through CombatSystem
                combatSystem.SpawnProjectile(
                    launchMessage.ProjectileId,
                    launchMessage.LaunchPosition,
                    launchMessage.TargetPosition,
                    launchMessage.WeaponData
                );
            }
            else
            {
                Debug.LogError("[WorldManager] CombatSystem not found - cannot spawn projectile!");
            }
        }
    }

    /// <summary>
    /// Handle damage confirmation messages for visual effects
    /// </summary>
    private void HandleDamageConfirmation(NetworkMessages.DamageConfirmationMessage damageMessage)
    {
        Debug.Log($"[WorldManager] DamageConfirmation: {damageMessage.ProjectileId} dealt {damageMessage.ActualDamage} to {damageMessage.TargetId}");
        
        // Play damage visual effects
        CombatSystem combatSystem = GameManager.Instance.CombatSystem;
        if (combatSystem == null)
        {
            combatSystem = FindObjectOfType<CombatSystem>();
        }
        
        if (combatSystem != null)
        {
            // Play impact effect at damage position
            combatSystem.PlayDamageEffect(
                damageMessage.DamagePosition,
                damageMessage.ActualDamage,
                damageMessage.IsCritical
            );
        }
    }

    #endregion

    private void OnDestroy()
    {
        // Unsubscribe from events
        NetworkManager.OnPlayerMoved -= HandlePlayerMoved;
        NetworkManager.OnWorldUpdate -= HandleWorldUpdate;
        NetworkManager.OnPlayerJoined -= HandlePlayerJoined;
        NetworkManager.OnCombatAction -= HandleCombatAction;
        
        // Phase 1: Unsubscribe from projectile events
        NetworkManager.OnProjectileLaunch -= HandleProjectileLaunch;
        NetworkManager.OnDamageConfirmation -= HandleDamageConfirmation;
    }
}