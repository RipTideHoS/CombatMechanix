# Grenade Mechanics Design Document

## Overview
This document outlines the design and implementation of a grenade system that allows players to throw grenades using the G key, with area damage after a timed delay.

## Core Mechanics

### Grenade Throwing
- **Trigger**: G key press
- **Requirement**: Player must have grenades in inventory
- **Consumption**: Uses 1 grenade per throw
- **Targeting**: Thrown at current mouse cursor/crosshair position
- **Physics**: Arc trajectory with gravity simulation

### Grenade Behavior
1. **Throw Phase**: Grenade follows ballistic trajectory
2. **Landing Phase**: Grenade lands and activates timer
3. **Warning Phase**: Visual indicator shows explosion area
4. **Explosion Phase**: Damage applied to all entities in radius

## Database Schema Changes

### ItemTypes Table Updates
Add new columns for grenade properties:
```sql
ALTER TABLE ItemTypes ADD COLUMN ExplosionRadius FLOAT DEFAULT 0.0;
ALTER TABLE ItemTypes ADD COLUMN ExplosionDelay FLOAT DEFAULT 0.0;
ALTER TABLE ItemTypes ADD COLUMN ThrowRange FLOAT DEFAULT 0.0;
ALTER TABLE ItemTypes ADD COLUMN AreaDamage FLOAT DEFAULT 0.0;
ALTER TABLE ItemTypes ADD COLUMN GrenadeType VARCHAR(50) DEFAULT NULL;
```

### New Grenade Items
```sql
INSERT INTO ItemTypes (
    ItemTypeId, ItemName, Description, ItemRarity, ItemCategory, MaxStackSize,
    AttackPower, AreaDamage, ExplosionRadius, ExplosionDelay, ThrowRange, GrenadeType
) VALUES
('frag_grenade', 'Fragmentation Grenade', 'High-explosive grenade with wide damage radius', 'Uncommon', 'Grenade', 5, 0, 75, 5.0, 3.0, 25.0, 'Explosive'),
('smoke_grenade', 'Smoke Grenade', 'Creates smoke cloud that blocks vision', 'Common', 'Grenade', 10, 0, 0, 8.0, 2.0, 20.0, 'Smoke'),
('flash_grenade', 'Flash Grenade', 'Blinds and disorients enemies in area', 'Rare', 'Grenade', 3, 0, 30, 4.0, 2.5, 18.0, 'Flash');
```

## Network Messages

### Client to Server
```csharp
public class GrenadeThrowMessage
{
    public string PlayerId { get; set; }
    public Vector3Data ThrowPosition { get; set; }
    public Vector3Data TargetPosition { get; set; }
    public string GrenadeType { get; set; }
    public long Timestamp { get; set; }
}

public class GrenadeExplodeMessage
{
    public string GrenadeId { get; set; }
    public Vector3Data ExplosionPosition { get; set; }
    public List<string> AffectedTargets { get; set; }
    public long Timestamp { get; set; }
}
```

### Server to Client
```csharp
public class GrenadeSpawnMessage
{
    public string GrenadeId { get; set; }
    public string PlayerId { get; set; }
    public Vector3Data StartPosition { get; set; }
    public Vector3Data TargetPosition { get; set; }
    public string GrenadeType { get; set; }
    public float ExplosionDelay { get; set; }
    public long Timestamp { get; set; }
}

public class GrenadeWarningMessage
{
    public string GrenadeId { get; set; }
    public Vector3Data ExplosionPosition { get; set; }
    public float ExplosionRadius { get; set; }
    public float TimeToExplosion { get; set; }
    public long Timestamp { get; set; }
}

public class GrenadeExplosionMessage
{
    public string GrenadeId { get; set; }
    public Vector3Data ExplosionPosition { get; set; }
    public float ExplosionRadius { get; set; }
    public float Damage { get; set; }
    public List<DamageTarget> DamagedTargets { get; set; }
    public long Timestamp { get; set; }
}

public class DamageTarget
{
    public string TargetId { get; set; }
    public string TargetType { get; set; } // "Player" or "Enemy"
    public float DamageDealt { get; set; }
    public Vector3Data Position { get; set; }
}
```

## Server-Side Implementation

### GrenadeManager Service
```csharp
public class GrenadeManager
{
    private readonly ConcurrentDictionary<string, ActiveGrenade> _activeGrenades = new();
    private readonly WebSocketConnectionManager _connectionManager;
    private readonly EnemyManager _enemyManager;
    private readonly Timer _updateTimer;

    public async Task ThrowGrenade(string playerId, Vector3Data throwPos, Vector3Data targetPos, string grenadeType);
    public async Task HandleGrenadeExplosion(string grenadeId);
    private async Task UpdateGrenades(object state);
    private async Task CalculateAreaDamage(ActiveGrenade grenade);
    private List<string> GetTargetsInRadius(Vector3Data center, float radius);
}

public class ActiveGrenade
{
    public string GrenadeId { get; set; }
    public string PlayerId { get; set; }
    public string GrenadeType { get; set; }
    public Vector3Data StartPosition { get; set; }
    public Vector3Data TargetPosition { get; set; }
    public Vector3Data CurrentPosition { get; set; }
    public DateTime ThrowTime { get; set; }
    public DateTime ExplosionTime { get; set; }
    public float ExplosionRadius { get; set; }
    public float AreaDamage { get; set; }
    public GrenadeState State { get; set; }
    public bool WarningDisplayed { get; set; }
}

public enum GrenadeState
{
    Flying,
    Landed,
    Warning,
    Exploded
}
```

### WebSocketConnectionManager Updates
```csharp
// Add to message handling
case "GrenadeThrow":
    await HandleGrenadeThrow(connection, wrapper.Data);
    break;

private async Task HandleGrenadeThrow(WebSocketConnection connection, object data)
{
    var grenadeData = JsonSerializer.Deserialize<GrenadeThrowMessage>(data.ToString()!);
    if (grenadeData == null) return;

    // Validate player has grenades in inventory
    if (!await ValidateGrenadeInventory(connection.PlayerId, grenadeData.GrenadeType))
    {
        await SendGrenadeError(connection.ConnectionId, "No grenades available");
        return;
    }

    // Remove grenade from inventory
    await ConsumeGrenadeFromInventory(connection.PlayerId, grenadeData.GrenadeType);

    // Process grenade throw
    await _grenadeManager.ThrowGrenade(
        connection.PlayerId,
        grenadeData.ThrowPosition,
        grenadeData.TargetPosition,
        grenadeData.GrenadeType
    );
}
```

## Client-Side Implementation

### Input Handling
```csharp
// In InputManager or similar
private void Update()
{
    if (Input.GetKeyDown(KeyCode.G))
    {
        TryThrowGrenade();
    }
}

private async void TryThrowGrenade()
{
    var availableGrenade = GetAvailableGrenadeFromInventory();
    if (availableGrenade == null)
    {
        ShowMessage("No grenades available");
        return;
    }

    var throwPosition = transform.position;
    var targetPosition = GetMouseWorldPosition();

    await networkManager.ThrowGrenade(availableGrenade.ItemType, throwPosition, targetPosition);
}
```

### Grenade GameObject
```csharp
public class Grenade : MonoBehaviour
{
    [Header("Grenade Properties")]
    public string GrenadeId;
    public string GrenadeType;
    public float ExplosionRadius;
    public float ExplosionDelay;
    public Vector3 TargetPosition;

    [Header("Visual Effects")]
    public GameObject WarningIndicator;
    public GameObject ExplosionEffect;
    public AudioClip ExplosionSound;
    public Material WarningMaterial;

    private Vector3 _velocity;
    private bool _hasLanded = false;
    private float _landTime;
    private bool _warningActive = false;

    private void Update()
    {
        if (!_hasLanded)
        {
            SimulatePhysics();
        }
        else
        {
            HandleLandedState();
        }
    }

    private void SimulatePhysics()
    {
        // Apply gravity and movement
        _velocity.y -= 9.81f * Time.deltaTime;
        transform.position += _velocity * Time.deltaTime;

        // Check for ground collision
        if (transform.position.y <= 0.1f) // Assuming ground at y=0
        {
            LandGrenade();
        }
    }

    private void LandGrenade()
    {
        _hasLanded = true;
        _landTime = Time.time;
        transform.position = new Vector3(transform.position.x, 0.1f, transform.position.z);

        // Play landing sound/effect
        AudioSource.PlayClipAtPoint(landingSound, transform.position);
    }

    private void HandleLandedState()
    {
        float timeRemaining = ExplosionDelay - (Time.time - _landTime);

        // Show warning indicator 1 second before explosion
        if (timeRemaining <= 1.0f && !_warningActive)
        {
            ShowWarningIndicator();
            _warningActive = true;
        }

        if (timeRemaining <= 0)
        {
            ExplodeGrenade();
        }
    }

    private void ShowWarningIndicator()
    {
        // Create warning circle on ground
        var warningObj = Instantiate(WarningIndicator, transform.position, Quaternion.identity);
        warningObj.transform.localScale = Vector3.one * ExplosionRadius * 2;

        // Animate warning (pulsing red circle)
        StartCoroutine(AnimateWarning(warningObj));
    }

    private void ExplodeGrenade()
    {
        // Visual explosion effect
        var explosion = Instantiate(ExplosionEffect, transform.position, Quaternion.identity);
        AudioSource.PlayClipAtPoint(ExplosionSound, transform.position);

        // Destroy after effects
        Destroy(gameObject);
        Destroy(explosion, 3f);
    }
}
```

### GrenadeManager (Client)
```csharp
public class GrenadeManager : MonoBehaviour
{
    [Header("Grenade Prefabs")]
    public GameObject FragGrenadePrefab;
    public GameObject SmokeGrenadePrefab;
    public GameObject FlashGrenadePrefab;

    private Dictionary<string, GameObject> _activeGrenades = new();

    public void SpawnGrenade(GrenadeSpawnMessage grenadeData)
    {
        GameObject prefab = GetGrenadePrefab(grenadeData.GrenadeType);
        if (prefab == null) return;

        var grenadeObj = Instantiate(prefab, grenadeData.StartPosition.ToVector3(), Quaternion.identity);
        var grenadeScript = grenadeObj.GetComponent<Grenade>();

        grenadeScript.GrenadeId = grenadeData.GrenadeId;
        grenadeScript.GrenadeType = grenadeData.GrenadeType;
        grenadeScript.TargetPosition = grenadeData.TargetPosition.ToVector3();
        grenadeScript.ExplosionDelay = grenadeData.ExplosionDelay;

        // Calculate throw trajectory
        Vector3 direction = (grenadeData.TargetPosition.ToVector3() - grenadeData.StartPosition.ToVector3()).normalized;
        float throwForce = 15f; // Adjustable
        grenadeScript.SetVelocity(direction * throwForce + Vector3.up * 5f);

        _activeGrenades[grenadeData.GrenadeId] = grenadeObj;
    }

    public void HandleGrenadeExplosion(GrenadeExplosionMessage explosionData)
    {
        if (_activeGrenades.TryGetValue(explosionData.GrenadeId, out GameObject grenadeObj))
        {
            var grenadeScript = grenadeObj.GetComponent<Grenade>();
            grenadeScript.ExplodeGrenade();

            _activeGrenades.Remove(explosionData.GrenadeId);
        }
    }
}
```

## Visual Effects

### Warning Indicator
- Red pulsing circle on ground showing explosion radius
- Appears 1 second before explosion
- Scales from 0 to full radius with alpha fade

### Explosion Effect
- Particle system with fire/smoke
- Screen shake for nearby players
- Flash effect for affected targets
- Debris and dust particles

### UI Elements
- Grenade count display in HUD
- Grenade throw trajectory preview
- Damage number display for affected targets

## Damage Calculation

### Area Damage Formula
```csharp
float CalculateAreaDamage(float baseDamage, float distanceFromCenter, float explosionRadius)
{
    if (distanceFromCenter > explosionRadius) return 0f;

    float damageMultiplier = 1f - (distanceFromCenter / explosionRadius);
    return baseDamage * damageMultiplier;
}
```

### Damage Application
1. Find all players and enemies within explosion radius
2. Calculate damage based on distance from center
3. Apply damage through existing damage systems
4. Send damage feedback to all clients

## Implementation Phases

### Phase 1: Basic Grenade System
- [ ] Database schema updates
- [ ] Basic grenade items in ItemTypes
- [ ] Server-side GrenadeManager service
- [ ] Client-side grenade input handling
- [ ] Network message infrastructure

### Phase 2: Physics and Trajectory
- [ ] Grenade throwing physics
- [ ] Trajectory calculation and simulation
- [ ] Landing detection and positioning
- [ ] Timer-based explosion system

### Phase 3: Visual Effects
- [ ] Warning indicator system
- [ ] Explosion particle effects
- [ ] UI elements and feedback
- [ ] Sound effects integration

### Phase 4: Damage Integration
- [ ] Area damage calculation
- [ ] Integration with existing combat system
- [ ] Player and enemy damage application
- [ ] Damage feedback and UI

### Phase 5: Advanced Features
- [ ] Multiple grenade types (smoke, flash)
- [ ] Grenade cooking (hold to delay)
- [ ] Environmental interactions
- [ ] Balance testing and tuning

## Configuration Values

### Default Grenade Stats
```csharp
public static class GrenadeDefaults
{
    public const float FragGrenadeRadius = 5.0f;
    public const float FragGrenadeDelay = 3.0f;
    public const float FragGrenadeDamage = 75f;
    public const float ThrowRange = 25f;
    public const float GrenadeSpeed = 15f;
    public const float GravityMultiplier = 1.0f;
}
```

## Security Considerations

### Anti-Cheat Measures
- Server validates grenade inventory before allowing throw
- Server calculates all trajectories and damage
- Rate limiting on grenade throws (prevent spam)
- Validation of throw positions and targets

### Performance Optimization
- Limit maximum active grenades per player
- Efficient area damage calculations
- Grenade cleanup after explosion
- Network message batching for multiple grenades

## Testing Strategy

### Unit Tests
- Damage calculation formulas
- Trajectory physics calculations
- Inventory validation logic
- Timer and delay mechanisms

### Integration Tests
- Full grenade throw workflow
- Multi-player area damage scenarios
- Network message synchronization
- Edge cases (invalid positions, etc.)

### Performance Tests
- Multiple simultaneous grenades
- Large area damage calculations
- Network bandwidth usage
- Memory usage with active grenades

This design provides a comprehensive foundation for implementing the grenade system while maintaining compatibility with existing game mechanics and ensuring robust multiplayer functionality.