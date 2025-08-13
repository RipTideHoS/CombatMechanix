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
            _characterController.center = new Vector3(0, 1, 0);
        }
    }

    private void Start()
    {
        _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            _mainCamera = FindObjectOfType<Camera>();
        }

        // Set initial position from spawn point
        transform.position = Vector3.zero; // Will be set by server
        CurrentRotation = transform.eulerAngles.y;
    }

    private void Update()
    {
        if (!GameManager.Instance.IsInGame) return;

        HandleInput();
        HandleMovement();
        HandleNetworking();
    }

    private void HandleInput()
    {
        // WASD movement input
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        _inputVector = new Vector3(horizontal, 0, vertical).normalized;
        _isMoving = _inputVector.magnitude > 0.1f;

        // Mouse look rotation (optional - you can also use arrow keys)
        if (Input.GetMouseButton(1)) // Right click to rotate
        {
            Vector3 mousePos = Input.mousePosition;
            if (_mainCamera != null)
            {
                Vector3 worldPos = _mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 10f));
                Vector3 direction = (worldPos - transform.position).normalized;

                if (direction.magnitude > 0.1f)
                {
                    float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                    CurrentRotation = Mathf.LerpAngle(CurrentRotation, targetAngle, RotationSpeed * Time.deltaTime);
                    transform.rotation = Quaternion.Euler(0, CurrentRotation, 0);
                }
            }
        }

        // Arrow key rotation
        float rotationInput = Input.GetAxis("Mouse X") * RotationSpeed * Time.deltaTime;
        if (Mathf.Abs(rotationInput) > 0.1f)
        {
            CurrentRotation += rotationInput;
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
    }

    private void HandleMovement()
    {
        if (_isMoving)
        {
            // Calculate movement direction relative to current rotation
            _moveDirection = transform.TransformDirection(_inputVector) * MovementSpeed;
            CurrentVelocity = _moveDirection;

            // Apply movement with collision detection
            if (_characterController != null)
            {
                _characterController.Move(_moveDirection * Time.deltaTime);
            }
            else
            {
                // Fallback if no CharacterController
                transform.Translate(_moveDirection * Time.deltaTime, Space.World);
            }
        }
        else
        {
            CurrentVelocity = Vector3.zero;
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
