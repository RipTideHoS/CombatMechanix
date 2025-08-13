using System;
using UnityEngine;

public class RemotePlayer : MonoBehaviour
{
    [Header("Interpolation")]
    public float InterpolationRate = 15f;
    public float ExtrapolationLimit = 0.5f;

    [Header("Visual Components")]
    public TextMesh PlayerNameText;
    public GameObject HealthBar;
    public ParticleSystem DamageEffect;

    // Player Data
    public string PlayerId { get; private set; }
    public string PlayerName { get; private set; }
    public float Health { get; private set; }
    public float MaxHealth { get; private set; }
    public int Level { get; private set; }

    // Movement Interpolation
    private Vector3 _targetPosition;
    private Vector3 _targetVelocity;
    private float _targetRotation;
    private float _lastUpdateTime;
    private Vector3 _previousPosition;

    // Visual Components
    private Renderer _renderer;
    private Animator _animator;

    private void Awake()
    {
        _renderer = GetComponentInChildren<Renderer>();
        _animator = GetComponent<Animator>();
        
        SetupVisualComponents();
    }

    private void Update()
    {
        InterpolateMovement();
        UpdateVisuals();
    }

    private void SetupVisualComponents()
    {
        // Create player name text if it doesn't exist
        if (PlayerNameText == null)
        {
            var nameObj = new GameObject("PlayerNameText");
            nameObj.transform.SetParent(transform);
            nameObj.transform.localPosition = new Vector3(0, 2.5f, 0);
            
            PlayerNameText = nameObj.AddComponent<TextMesh>();
            PlayerNameText.text = "Player";
            PlayerNameText.fontSize = 20;
            PlayerNameText.color = Color.white;
            PlayerNameText.anchor = TextAnchor.MiddleCenter;
            PlayerNameText.alignment = TextAlignment.Center;
        }

        // Create health bar if it doesn't exist
        if (HealthBar == null)
        {
            var healthBarObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            healthBarObj.name = "HealthBar";
            healthBarObj.transform.SetParent(transform);
            healthBarObj.transform.localPosition = new Vector3(0, 2.2f, 0);
            healthBarObj.transform.localScale = new Vector3(1f, 0.1f, 0.1f);
            
            var healthRenderer = healthBarObj.GetComponent<Renderer>();
            if (healthRenderer != null)
            {
                healthRenderer.material.color = Color.green;
            }
            
            // Remove collider from health bar
            var collider = healthBarObj.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyImmediate(collider);
            }
            
            HealthBar = healthBarObj;
        }

        // Create damage effect if it doesn't exist
        if (DamageEffect == null)
        {
            var effectObj = new GameObject("DamageEffect");
            effectObj.transform.SetParent(transform);
            effectObj.transform.localPosition = Vector3.zero;
            
            DamageEffect = effectObj.AddComponent<ParticleSystem>();
            var main = DamageEffect.main;
            main.startColor = Color.red;
            main.startSize = 0.1f;
            main.startLifetime = 1f;
            main.maxParticles = 20;
            
            var emission = DamageEffect.emission;
            emission.enabled = false; // Only play on demand
        }
    }

    public void Initialize(PlayerState playerState)
    {
        PlayerId = playerState.PlayerId;
        PlayerName = playerState.PlayerName;
        Health = playerState.Health;
        MaxHealth = playerState.MaxHealth;
        Level = playerState.Level;

        transform.position = playerState.Position;
        _targetPosition = playerState.Position;
        _previousPosition = playerState.Position;
        _targetRotation = playerState.Rotation;
        _lastUpdateTime = Time.time;

        // Update visual elements
        if (PlayerNameText != null)
        {
            PlayerNameText.text = $"{PlayerName} (Lvl {Level})";
        }

        UpdateHealthBar();
        
        Debug.Log($"RemotePlayer initialized: {PlayerName} at {transform.position}");
    }

    public void UpdateState(PlayerState playerState)
    {
        _previousPosition = transform.position;
        _targetPosition = playerState.Position;
        _targetVelocity = playerState.Velocity;
        _targetRotation = playerState.Rotation;
        _lastUpdateTime = Time.time;

        // Update health if changed
        if (Math.Abs(Health - playerState.Health) > 0.1f)
        {
            Health = playerState.Health;
            UpdateHealthBar();
        }

        // Update other properties
        if (Level != playerState.Level)
        {
            Level = playerState.Level;
            if (PlayerNameText != null)
            {
                PlayerNameText.text = $"{PlayerName} (Lvl {Level})";
            }
        }
    }

    private void InterpolateMovement()
    {
        float timeSinceUpdate = Time.time - _lastUpdateTime;
        
        // Extrapolate position based on velocity for smooth movement
        Vector3 extrapolatedPosition = _targetPosition;
        if (timeSinceUpdate < ExtrapolationLimit && _targetVelocity.magnitude > 0.1f)
        {
            extrapolatedPosition += _targetVelocity * timeSinceUpdate;
        }

        // Smoothly interpolate to target position
        transform.position = Vector3.Lerp(transform.position, extrapolatedPosition, InterpolationRate * Time.deltaTime);
        
        // Interpolate rotation
        float currentY = transform.eulerAngles.y;
        float targetY = Mathf.LerpAngle(currentY, _targetRotation, InterpolationRate * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0, targetY, 0);
    }

    private void UpdateVisuals()
    {
        // Update animator based on movement
        bool isMoving = _targetVelocity.magnitude > 0.1f;
        if (_animator != null)
        {
            _animator.SetBool("IsWalking", isMoving);
            _animator.SetFloat("Speed", _targetVelocity.magnitude);
        }

        // Update name tag to face camera
        if (PlayerNameText != null && Camera.main != null)
        {
            PlayerNameText.transform.LookAt(Camera.main.transform);
            PlayerNameText.transform.Rotate(0, 180, 0); // Flip to face camera correctly
        }

        // Update health bar orientation
        if (HealthBar != null && Camera.main != null)
        {
            HealthBar.transform.LookAt(Camera.main.transform);
            HealthBar.transform.Rotate(0, 180, 0);
        }
    }

    private void UpdateHealthBar()
    {
        if (HealthBar != null)
        {
            float healthPercent = MaxHealth > 0 ? Health / MaxHealth : 0;
            HealthBar.transform.localScale = new Vector3(healthPercent, 0.1f, 0.1f);
            
            // Change color based on health
            var renderer = HealthBar.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (healthPercent > 0.6f)
                    renderer.material.color = Color.green;
                else if (healthPercent > 0.3f)
                    renderer.material.color = Color.yellow;
                else
                    renderer.material.color = Color.red;
            }
        }
    }

    public void PlayDamageEffect(float damage)
    {
        // Play particle effect
        if (DamageEffect != null)
        {
            var emission = DamageEffect.emission;
            emission.enabled = true;
            DamageEffect.Play();
            
            // Disable emission after a short time
            StartCoroutine(DisableDamageEffectAfterDelay(1f));
        }

        // Show floating damage text
        var uiManager = GameManager.Instance?.UIManager;
        if (uiManager != null)
        {
            uiManager.ShowFloatingDamage(transform.position + Vector3.up * 1.5f, damage);
        }

        Debug.Log($"RemotePlayer {PlayerName} took {damage} damage!");
    }

    private System.Collections.IEnumerator DisableDamageEffectAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (DamageEffect != null)
        {
            var emission = DamageEffect.emission;
            emission.enabled = false;
        }
    }

    public void SetVisibility(bool visible)
    {
        gameObject.SetActive(visible);
    }

    public float GetDistanceToLocalPlayer()
    {
        if (PlayerController.Instance != null)
        {
            return Vector3.Distance(transform.position, PlayerController.Instance.transform.position);
        }
        return float.MaxValue;
    }

    private void OnDestroy()
    {
        Debug.Log($"RemotePlayer {PlayerName} destroyed");
    }
}