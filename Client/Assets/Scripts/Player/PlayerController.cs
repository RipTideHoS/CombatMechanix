using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    [Header("Movement Settings")]
    public float MovementSpeed = 5f;
    public float RotationSpeed = 720f;
    public float SendRate = 20f; // 20 updates per second

    [Header("Prediction Settings")]
    public float PredictionTime = 0.1f;
    public float ReconciliationThreshold = 0.5f;

    // Components
    private CharacterController _characterController;
    private Camera _mainCamera;

    // Input
    private Vector3 _inputVector;
    private Vector3 _moveDirection;
    private bool _isMoving;
    
    // Gravity and ground handling
    private float _verticalVelocity = 0f;
    private float _gravity = -9.81f;
    private bool _isGrounded;

    // Networking
    private Vector3 _lastSentPosition;
    private float _lastSendTime;
    private Queue<Vector3> _positionHistory = new Queue<Vector3>();
    private Queue<float> _timeHistory = new Queue<float>();

    // State
    public Vector3 CurrentVelocity { get; private set; }
    public float CurrentRotation { get; private set; }
    public bool IsLocalPlayer { get; private set; } = true;

    private void Awake()
    {
        Instance = this;
        _characterController = GetComponent<CharacterController>();

        // Add CharacterController if it doesn't exist
        if (_characterController == null)
        {
            _characterController = gameObject.AddComponent<CharacterController>();
            _characterController.radius = 0.5f;
            _characterController.height = 2f;
            _characterController.center = new Vector3(0, 0f, 0); // Center at transform origin
            _characterController.skinWidth = 0.08f; // Prevents sticking to surfaces
            _characterController.minMoveDistance = 0.001f; // Allows small movements
        }
    }

    private void Start()
    {
        _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            _mainCamera = FindObjectOfType<Camera>();
        }

        // Set initial position so CharacterController bottom sits on ground
        // CharacterController center is at (0,0,0) relative to transform  
        // CharacterController height is 2, so bottom is at center.y - height/2 = 0 - 1 = -1 relative to transform
        // To put bottom at ground level (world y=0), transform should be at y=1
        transform.position = new Vector3(0, 1f, 0); // Transform at y=1 puts CC bottom at ground level
        CurrentRotation = transform.eulerAngles.y;
        
        // Initialize networking variables
        _lastSentPosition = transform.position;
        _lastSendTime = Time.time;
        
    }

    private void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsInGame) 
        {
            return;
        }

        HandleInput();
        HandleMovement();
        HandleNetworking();
    }

    private void HandleInput()
    {
        // Enhanced WASD movement input with camera-relative movement
        float horizontal = Input.GetAxis("Horizontal"); // A/D keys
        float vertical = Input.GetAxis("Vertical");     // W/S keys

        // Make movement relative to camera direction for 3rd person
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            Vector3 cameraForward = mainCamera.transform.forward;
            Vector3 cameraRight = mainCamera.transform.right;
            
            // Remove Y component to keep movement on ground plane
            cameraForward.y = 0;
            cameraRight.y = 0;
            cameraForward.Normalize();
            cameraRight.Normalize();
            
            // Calculate camera-relative movement direction
            Vector3 moveDirection = (cameraForward * vertical + cameraRight * horizontal).normalized;
            _inputVector = moveDirection;
        }
        else
        {
            // Fallback to world-relative movement if no camera
            _inputVector = new Vector3(horizontal, 0, vertical).normalized;
        }

        _isMoving = _inputVector.magnitude > 0.1f;

        // Rotate player to face movement direction for 3rd person
        if (_isMoving && _inputVector.magnitude > 0.1f)
        {
            float targetAngle = Mathf.Atan2(_inputVector.x, _inputVector.z) * Mathf.Rad2Deg;
            CurrentRotation = Mathf.LerpAngle(CurrentRotation, targetAngle, RotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0, CurrentRotation, 0);
        }

        // Mouse look rotation removed - now handled by CameraController

        // Alternative: Q/E keys for manual rotation
        if (Input.GetKey(KeyCode.Q))
        {
            CurrentRotation -= RotationSpeed * Time.deltaTime;
            transform.rotation = Quaternion.Euler(0, CurrentRotation, 0);
        }
        else if (Input.GetKey(KeyCode.E))
        {
            CurrentRotation += RotationSpeed * Time.deltaTime;
            transform.rotation = Quaternion.Euler(0, CurrentRotation, 0);
        }

        // Combat input
        if (Input.GetMouseButtonDown(0)) // Left click to attack
        {
            HandleAttackInput();
        }

        // Resource gathering
        if (Input.GetKeyDown(KeyCode.E))
        {
            HandleGatherInput();
        }

        // Quick chat (T key handled in UIManager)
        if (Input.GetKeyDown(KeyCode.Return))
        {
            // Focus chat input
            var uiManager = GameManager.Instance.UIManager;
            if (uiManager != null && uiManager.ChatInput != null)
            {
                uiManager.ChatInput.Select();
                uiManager.ChatInput.ActivateInputField();
            }
        }

        // Inventory toggle (I key)
        if (Input.GetKeyDown(KeyCode.I))
        {
            HandleInventoryToggle();
        }

        // Character panel toggle (C key)
        if (Input.GetKeyDown(KeyCode.C))
        {
            HandleCharacterToggle();
        }

        // Note: T key for chat is handled by UIManager.Update() to avoid conflicts
    }

    private void HandleMovement()
    {
        // Check if grounded
        _isGrounded = _characterController != null ? _characterController.isGrounded : true;

        // Handle gravity
        if (_isGrounded && _verticalVelocity < 0)
        {
            _verticalVelocity = -2f; // Small downward force to keep grounded
        }
        else
        {
            _verticalVelocity += _gravity * Time.deltaTime; // Apply gravity
        }

        // Calculate horizontal movement
        Vector3 horizontalMovement = Vector3.zero;
        if (_isMoving)
        {
            // Use camera-relative movement direction directly (already calculated in HandleInput)
            horizontalMovement = _inputVector * MovementSpeed;
            CurrentVelocity = horizontalMovement;
        }
        else
        {
            CurrentVelocity = Vector3.zero;
        }

        // Combine horizontal movement with vertical velocity
        Vector3 finalMovement = horizontalMovement;
        finalMovement.y = _verticalVelocity;

        // Apply movement with collision detection
        if (_characterController != null)
        {
            _characterController.Move(finalMovement * Time.deltaTime);
        }
        else
        {
            // Fallback if no CharacterController (only horizontal movement)
            if (_isMoving)
            {
                transform.Translate(horizontalMovement * Time.deltaTime, Space.World);
            }
        }

        // Store position history for reconciliation
        _positionHistory.Enqueue(transform.position);
        _timeHistory.Enqueue(Time.time);

        // Limit history size
        while (_positionHistory.Count > 60) // 1 second at 60 FPS
        {
            _positionHistory.Dequeue();
            _timeHistory.Dequeue();
        }
    }

    private async void HandleNetworking()
    {
        // Check if we should send an update (either at normal rate or forced interval)
        bool normalRateCheck = Time.time - _lastSendTime > 1f / SendRate;
        bool forcedIntervalCheck = Time.time - _lastSendTime > 1f; // Force update every 1 second
        
        if (normalRateCheck || forcedIntervalCheck)
        {
            // Send if position changed significantly, if moving, or if forced interval reached
            bool shouldSend = Vector3.Distance(transform.position, _lastSentPosition) > 0.01f || 
                             _isMoving || 
                             forcedIntervalCheck;
            
            if (shouldSend)
            {
                var networkManager = GameManager.Instance?.NetworkManager;
                
                // Try to fix NetworkManager if it's null
                if (networkManager == null)
                {
                    Debug.Log("[PlayerController] NetworkManager is NULL! Attempting auto-refresh...");
                    
                    // Try to refresh the GameManager's NetworkManager reference
                    GameManager.Instance?.RefreshNetworkManager();
                    networkManager = GameManager.Instance?.NetworkManager;
                    
                    Debug.Log($"[PlayerController] After refresh - NetworkManager: {(networkManager != null ? "found" : "still null")}");
                    
                    // If still null, try direct scene lookup
                    if (networkManager == null)
                    {
                        networkManager = FindObjectOfType<NetworkManager>();
                        Debug.Log($"[PlayerController] Direct FindObjectOfType result: {(networkManager != null ? "found" : "still null")}");
                    }
                    
                    if (networkManager == null) return;
                }
                
                if (networkManager.IsConnected)
                {
                    try
                    {
                        await networkManager.SendMovement(transform.position, CurrentVelocity, CurrentRotation);
                        _lastSentPosition = transform.position;
                        _lastSendTime = Time.time;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[PlayerController] Failed to send movement: {ex.Message}");
                    }
                }
            }
        }
    }

    public void CorrectPosition(Vector3 serverPosition)
    {
        // Server reconciliation - correct position if too far off
        float distance = Vector3.Distance(transform.position, serverPosition);

        if (distance > ReconciliationThreshold)
        {
            Debug.Log($"Position corrected by server: {distance:F2} units");
            SetPosition(serverPosition);

            // Clear prediction history since we're now synced
            _positionHistory.Clear();
            _timeHistory.Clear();
        }
    }

    private void HandleAttackInput()
    {
        if (_mainCamera == null) return;

        // Raycast to find target
        Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            Vector3 attackPosition = hit.point;
            string targetId = null;
            string targetType = "Ground";

            // Check for enemy targets first
            var targetEnemy = hit.collider.GetComponent<EnemyBase>();
            if (targetEnemy != null)
            {
                targetId = targetEnemy.EnemyId;
                targetType = "Enemy";
            }
            else
            {
                // Check if we hit another player
                var targetPlayer = hit.collider.GetComponent<RemotePlayer>();
                if (targetPlayer != null)
                {
                    targetId = targetPlayer.PlayerId;
                    targetType = "Player";
                }
            }

            // Send attack to server
            var networkManager = GameManager.Instance?.NetworkManager ?? FindObjectOfType<NetworkManager>();
            
            if (networkManager != null)
            {
                _ = networkManager.SendAttack(targetId, "BasicAttack", attackPosition);
            }

            // Play local attack animation/effect
            var combatSystem = GameManager.Instance.CombatSystem;
            if (combatSystem != null)
            {
                combatSystem.PlayAttackEffect(transform.position, attackPosition);
            }

        }
    }

    private void HandleGatherInput()
    {
        // Find nearby resources
        var nearbyColliders = Physics.OverlapSphere(transform.position, 3f);

        foreach (var collider in nearbyColliders)
        {
            var resource = collider.GetComponent<ResourceNodeClient>();
            if (resource != null && resource.CurrentAmount > 0)
            {
                // Send gather request to server
                var networkManager = GameManager.Instance.NetworkManager;
                if (networkManager != null)
                {
                    _ = networkManager.SendResourceGather(
                        resource.ResourceId,
                        resource.ResourceType,
                        resource.transform.position
                    );
                }

                // Play gathering animation
                resource.PlayGatherEffect();
                break; // Only gather from one resource at a time
            }
        }
    }

    private void HandleInventoryToggle()
    {
        // Try UIManager first
        var uiManager = GameManager.Instance?.UIManager;
        if (uiManager != null)
        {
            uiManager.ToggleInventory();
            return;
        }
        
        // Fallback 1: Try to find UIManager directly
        UIManager directUIManager = FindObjectOfType<UIManager>();
        if (directUIManager != null)
        {
            directUIManager.ToggleInventory();
            return;
        }
        
        // Fallback 2: Direct panel toggle
        GameObject inventoryPanel = GameObject.Find("InventoryPanel");
        if (inventoryPanel != null)
        {
            bool currentState = inventoryPanel.activeSelf;
            inventoryPanel.SetActive(!currentState);
        }
    }

    private void HandleCharacterToggle()
    {
        // Try UIManager first
        var uiManager = GameManager.Instance?.UIManager;
        if (uiManager != null)
        {
            uiManager.ToggleCharacterPanel();
            return;
        }
        
        // Fallback 1: Try to find UIManager directly
        UIManager directUIManager = FindObjectOfType<UIManager>();
        if (directUIManager != null)
        {
            directUIManager.ToggleCharacterPanel();
            return;
        }
        
        // Fallback 2: Direct panel toggle
        GameObject characterPanel = GameObject.Find("CharacterPanel");
        if (characterPanel != null)
        {
            bool currentState = characterPanel.activeSelf;
            characterPanel.SetActive(!currentState);
        }
    }


    // ===============================================
    // PUBLIC METHODS FOR EXTERNAL CONTROL
    // ===============================================

    public void SetPosition(Vector3 position)
    {
        if (_characterController != null)
        {
            _characterController.enabled = false;
            transform.position = position;
            _characterController.enabled = true;
        }
        else
        {
            transform.position = position;
        }
    }

    public void SetRotation(float rotation)
    {
        CurrentRotation = rotation;
        transform.rotation = Quaternion.Euler(0, rotation, 0);
    }

    public void TakeDamage(float damage)
    {

        // Update UI
        var uiManager = GameManager.Instance.UIManager;
        if (uiManager != null)
        {
            uiManager.UpdateHealth(damage);
        }

        // Play damage effects
        var combatSystem = GameManager.Instance.CombatSystem;
        if (combatSystem != null)
        {
            combatSystem.PlayDamageEffect(damage);
        }
    }

    public bool IsMoving()
    {
        return _isMoving;
    }

    public Vector3 GetVelocity()
    {
        return CurrentVelocity;
    }
}
