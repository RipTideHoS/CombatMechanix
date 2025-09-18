using UnityEngine;
using System.Collections;
using System;

namespace CombatMechanix.Unity
{
    /// <summary>
    /// Handles grenade input and throws requests for Unity client
    /// </summary>
    public class GrenadeInputHandler : MonoBehaviour
    {
        [Header("Grenade Settings")]
        public KeyCode grenadeKey = KeyCode.G;
        public LayerMask groundLayer = 1; // Default layer
        public float maxThrowRange = 25f;
        public Camera playerCamera;

        [Header("UI References")]
        public GrenadeUI grenadeUI;

        [Header("Visual Feedback")]
        public GameObject trajectoryIndicator;
        public LineRenderer trajectoryLine;
        public GameObject targetIndicator;

        private NetworkManager networkManager;
        private string currentGrenadeType = "frag_grenade";
        private bool isAiming = false;
        private Vector3 aimPosition;

        // Grenade counts (updated from server)
        private int fragGrenades = 3;
        private int smokeGrenades = 3;
        private int flashGrenades = 3;

        void Start()
        {
            Debug.Log("[GrenadeInputHandler] Starting initialization...");

            // Find NetworkManager using Unity's standard approach
            networkManager = FindObjectOfType<NetworkManager>();
            if (networkManager == null)
            {
                Debug.LogError("NetworkManager not found! Grenade system will not work.");
            }
            else
            {
                Debug.Log("[GrenadeInputHandler] NetworkManager found successfully");
            }

            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            // Initialize trajectory line
            if (trajectoryLine == null)
            {
                trajectoryLine = gameObject.AddComponent<LineRenderer>();
                trajectoryLine.material = new Material(Shader.Find("Sprites/Default"));
                trajectoryLine.startColor = Color.red;
                trajectoryLine.endColor = Color.red;
                trajectoryLine.startWidth = 0.1f;
                trajectoryLine.endWidth = 0.1f;
                trajectoryLine.positionCount = 0;
                trajectoryLine.enabled = false;
            }

            // Initialize UI
            if (grenadeUI != null)
            {
                grenadeUI.UpdateGrenadeCounts(fragGrenades, smokeGrenades, flashGrenades);
            }

            // Subscribe to grenade count updates
            Debug.Log("[GrenadeInputHandler] Subscribing to NetworkManager.OnGrenadeCountUpdate event");
            NetworkManager.OnGrenadeCountUpdate += OnGrenadeCountUpdate;
        }

        void OnDestroy()
        {
            // Unsubscribe from events
            NetworkManager.OnGrenadeCountUpdate -= OnGrenadeCountUpdate;
        }

        void Update()
        {
            HandleGrenadeInput();
            UpdateAiming();
        }

        /// <summary>
        /// Public method to toggle aiming mode - called from PlayerController
        /// </summary>
        public void ToggleAiming()
        {
            Debug.Log($"[GrenadeInputHandler] ToggleAiming called! Current grenade count: {GetCurrentGrenadeCount()} for type: {currentGrenadeType}");
            Debug.Log($"[GrenadeInputHandler] Grenade counts - Frag:{fragGrenades}, Smoke:{smokeGrenades}, Flash:{flashGrenades}");

            if (GetCurrentGrenadeCount() > 0)
            {
                Debug.Log("[GrenadeInputHandler] Toggling aiming mode");
                isAiming = !isAiming;
                SetAimingMode(isAiming);
            }
            else
            {
                Debug.Log($"[GrenadeInputHandler] No {currentGrenadeType} grenades available!");
                if (grenadeUI != null)
                {
                    grenadeUI.ShowMessage($"No {GetGrenadeDisplayName()} available!");
                }
            }
        }

        private void HandleGrenadeInput()
        {
            // Note: G key is now handled by PlayerController, this method handles other inputs

            // Grenade type switching (1, 2, 3 keys)
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                SwitchGrenadeType("frag_grenade");
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                SwitchGrenadeType("smoke_grenade");
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                SwitchGrenadeType("flash_grenade");
            }

            // Throw grenade
            if (isAiming && Input.GetMouseButtonDown(0))
            {
                ThrowGrenade();
            }

            // Cancel aiming
            if (isAiming && (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)))
            {
                SetAimingMode(false);
            }
        }

        private void UpdateAiming()
        {
            if (!isAiming) return;

            // Get the actual player position
            Vector3 playerPosition = GetPlayerPosition();

            // Cast ray from camera to determine throw target
            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, maxThrowRange, groundLayer))
            {
                aimPosition = hit.point;

                // Update trajectory visualization from player's current position
                UpdateTrajectoryVisualization(playerPosition, aimPosition);

                // Update target indicator
                if (targetIndicator != null)
                {
                    targetIndicator.transform.position = aimPosition;
                    targetIndicator.SetActive(true);
                }
            }
        }

        private void UpdateTrajectoryVisualization(Vector3 startPos, Vector3 endPos)
        {
            if (trajectoryLine == null) return;

            trajectoryLine.enabled = true;
            trajectoryLine.positionCount = 20;

            // Calculate arc trajectory
            Vector3 velocity = CalculateThrowVelocity(startPos, endPos);

            for (int i = 0; i < trajectoryLine.positionCount; i++)
            {
                float time = i * 0.1f;
                Vector3 point = startPos + velocity * time + 0.5f * Physics.gravity * time * time;
                trajectoryLine.SetPosition(i, point);
            }
        }

        private Vector3 CalculateThrowVelocity(Vector3 startPos, Vector3 endPos)
        {
            // Simple ballistic trajectory calculation
            Vector3 displacement = endPos - startPos;
            Vector3 horizontalDisplacement = new Vector3(displacement.x, 0, displacement.z);

            float horizontalDistance = horizontalDisplacement.magnitude;
            float height = displacement.y;

            float throwAngle = 45f * Mathf.Deg2Rad; // 45-degree throw angle
            float gravity = Mathf.Abs(Physics.gravity.y);

            float velocity = Mathf.Sqrt((horizontalDistance * gravity) / Mathf.Sin(2 * throwAngle));

            Vector3 horizontalDirection = horizontalDisplacement.normalized;
            Vector3 throwDirection = horizontalDirection * Mathf.Cos(throwAngle) + Vector3.up * Mathf.Sin(throwAngle);

            return throwDirection * velocity;
        }

        private void SetAimingMode(bool aiming)
        {
            isAiming = aiming;

            if (trajectoryLine != null)
            {
                trajectoryLine.enabled = aiming;
            }

            if (targetIndicator != null)
            {
                targetIndicator.SetActive(aiming);
            }

            if (grenadeUI != null)
            {
                grenadeUI.SetAimingMode(aiming, currentGrenadeType);
            }
        }

        private async void ThrowGrenade()
        {
            if (networkManager == null)
            {
                Debug.LogError("Cannot throw grenade: NetworkManager not found");
                return;
            }

            if (GetCurrentGrenadeCount() <= 0)
            {
                Debug.Log($"No {currentGrenadeType} grenades available!");
                return;
            }

            // Get the actual player position for throwing
            Vector3 playerPosition = GetPlayerPosition();

            // Send grenade throw message to server via NetworkManager
            await networkManager.SendGrenadeThrow(currentGrenadeType, playerPosition, aimPosition);

            Debug.Log($"Threw {currentGrenadeType} grenade from {playerPosition} to {aimPosition}");

            // Exit aiming mode
            SetAimingMode(false);
        }

        private void SwitchGrenadeType(string grenadeType)
        {
            currentGrenadeType = grenadeType;

            if (grenadeUI != null)
            {
                grenadeUI.SetSelectedGrenadeType(grenadeType);
            }

            Debug.Log($"Switched to {GetGrenadeDisplayName()}");
        }

        private int GetCurrentGrenadeCount()
        {
            return currentGrenadeType switch
            {
                "frag_grenade" => fragGrenades,
                "smoke_grenade" => smokeGrenades,
                "flash_grenade" => flashGrenades,
                _ => 0
            };
        }

        private string GetGrenadeDisplayName()
        {
            return currentGrenadeType switch
            {
                "frag_grenade" => "Frag Grenade",
                "smoke_grenade" => "Smoke Grenade",
                "flash_grenade" => "Flash Grenade",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Called by NetworkManager when grenade count update is received
        /// </summary>
        private void OnGrenadeCountUpdate(NetworkMessages.GrenadeCountUpdateMessage countUpdate)
        {
            Debug.Log($"[GrenadeInputHandler] OnGrenadeCountUpdate called with Frag={countUpdate.FragGrenades}, Smoke={countUpdate.SmokeGrenades}, Flash={countUpdate.FlashGrenades}");

            fragGrenades = countUpdate.FragGrenades;
            smokeGrenades = countUpdate.SmokeGrenades;
            flashGrenades = countUpdate.FlashGrenades;

            if (grenadeUI != null)
            {
                grenadeUI.UpdateGrenadeCounts(fragGrenades, smokeGrenades, flashGrenades);
            }

            Debug.Log($"[GrenadeInputHandler] Grenade counts updated: Frag={fragGrenades}, Smoke={smokeGrenades}, Flash={flashGrenades}");
        }

        /// <summary>
        /// Get the current player position from PlayerController
        /// </summary>
        private Vector3 GetPlayerPosition()
        {
            // Try to get position from PlayerController
            if (PlayerController.Instance != null)
            {
                return PlayerController.Instance.transform.position;
            }

            // Fallback to finding the player GameObject
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                return player.transform.position;
            }

            // Last resort - use the camera position if available
            if (playerCamera != null)
            {
                return playerCamera.transform.position;
            }

            // Absolute fallback - use origin
            Debug.LogWarning("[GrenadeInputHandler] Could not find player position, using origin");
            return Vector3.zero;
        }
    }
}