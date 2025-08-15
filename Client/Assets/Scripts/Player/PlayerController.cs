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
        
        Debug.Log($"Player positioned at: {transform.position}, CharacterController center: {_characterController.center}, height: {_characterController.height}");
    }

    private void Update()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("GameManager.Instance is null!");
            return;
        }
        
        if (!GameManager.Instance.IsInGame) 
        {
            // Only log this occasionally to avoid spam
            if (Time.frameCount % 300 == 0) // Log every 5 seconds at 60fps
                Debug.LogWarning("Player movement blocked - GameManager.IsInGame is false");
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
        
        if (horizontal != 0 || vertical != 0)
            Debug.Log($"Input detected: H={horizontal:F2}, V={vertical:F2}");

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
        
        if (_isMoving)
        {
            Debug.Log($"Movement calculated: InputVector={_inputVector}, IsMoving={_isMoving}");
        }

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
            
            if (_isMoving)
            {
                Debug.Log($"Movement applied via CharacterController: H={horizontalMovement}, V={_verticalVelocity:F2}, Grounded={_isGrounded}");
            }
        }
        else
        {
            // Fallback if no CharacterController (only horizontal movement)
            if (_isMoving)
            {
                transform.Translate(horizontalMovement * Time.deltaTime, Space.World);
                Debug.Log($"Movement applied via Transform: {horizontalMovement * Time.deltaTime}");
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

    private void HandleNetworking()
    {
        // Send movement updates at specified rate
        if (Time.time - _lastSendTime > 1f / SendRate)
        {
            // Only send if position changed significantly or if moving
            if (Vector3.Distance(transform.position, _lastSentPosition) > 0.01f || _isMoving)
            {
                var networkManager = GameManager.Instance.NetworkManager;
                if (networkManager != null && networkManager.IsConnected)
                {
                   _= networkManager.SendMovement(transform.position, CurrentVelocity, CurrentRotation);
                }

                _lastSentPosition = transform.position;
                _lastSendTime = Time.time;
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

            // Check if we hit another player
            var targetPlayer = hit.collider.GetComponent<RemotePlayer>();
            if (targetPlayer != null)
            {
                targetId = targetPlayer.PlayerId;
            }

            // Send attack to server
            var networkManager = GameManager.Instance.NetworkManager;
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

            Debug.Log($"Attack sent to position: {attackPosition}" + (targetId != null ? $", target: {targetId}" : ""));
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
                Debug.Log($"Gathering {resource.ResourceType} from {resource.ResourceId}");
                break; // Only gather from one resource at a time
            }
        }
    }

    private void HandleInventoryToggle()
    {
        Debug.Log($"HandleInventoryToggle called - GameManager.Instance: {(GameManager.Instance != null ? "exists" : "null")}");
        
        // Try UIManager first
        var uiManager = GameManager.Instance?.UIManager;
        if (uiManager != null)
        {
            Debug.Log("Using UIManager.ToggleInventory()");
            uiManager.ToggleInventory();
            return;
        }
        
        Debug.LogWarning($"UIManager not available via GameManager - GameManager: {(GameManager.Instance != null ? "exists" : "null")}, UIManager: {(uiManager != null ? "exists" : "null")}");
        
        // Fallback 1: Try to find UIManager directly
        UIManager directUIManager = FindObjectOfType<UIManager>();
        if (directUIManager != null)
        {
            Debug.Log("Found UIManager directly, using ToggleInventory()");
            directUIManager.ToggleInventory();
            return;
        }
        
        Debug.LogWarning("No UIManager found via FindObjectOfType either");
        
        // Fallback 2: Direct panel toggle
        GameObject inventoryPanel = GameObject.Find("InventoryPanel");
        if (inventoryPanel != null)
        {
            bool currentState = inventoryPanel.activeSelf;
            inventoryPanel.SetActive(!currentState);
            Debug.Log($"Inventory panel toggled (direct fallback): {!currentState}");
        }
        else
        {
            Debug.LogError("No inventory panel found with GameObject.Find either - UI system may be broken");
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
        Debug.Log($"Local player took {damage} damage!");

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
