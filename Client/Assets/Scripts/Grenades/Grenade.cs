using UnityEngine;
using System;
using System.Collections;

namespace CombatMechanix.Unity
{
    /// <summary>
    /// Individual grenade behavior for Unity client
    /// </summary>
    public class Grenade : MonoBehaviour
    {
        public string GrenadeId { get; private set; }
        public string GrenadeType { get; private set; }
        public Vector3 TargetPosition { get; private set; }
        public float ExplosionDelay { get; private set; }

        [Header("Physics")]
        public float throwForce = 15f;
        public float gravityMultiplier = 1f;
        public LayerMask groundLayer = 1;

        [Header("Visual Effects")]
        public GameObject warningIndicator;
        public ParticleSystem trailEffect;
        public AudioClip landingSound;

        // Events
        public event Action<string, Vector3> OnLanded;
        public event Action<string> OnExploded;

        private Vector3 velocity;
        private bool hasLanded = false;
        private float landTime;
        private bool warningActive = false;
        private Rigidbody rb;
        private AudioSource audioSource;

        void Start()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            // Disable rigidbody initially for custom physics
            rb.isKinematic = true;

            // Start trail effect if available
            if (trailEffect != null && !trailEffect.isPlaying)
            {
                trailEffect.Play();
            }
        }

        void Update()
        {
            if (!hasLanded)
            {
                SimulatePhysics();
            }
            else
            {
                HandleLandedState();
            }
        }

        /// <summary>
        /// Initialize the grenade with server data
        /// </summary>
        public void Initialize(string grenadeId, string grenadeType, Vector3 targetPos, float explosionDelay)
        {
            GrenadeId = grenadeId;
            GrenadeType = grenadeType;
            TargetPosition = targetPos;
            ExplosionDelay = explosionDelay;

            // Calculate initial velocity for arc trajectory
            CalculateThrowVelocity();

            Debug.Log($"Initialized grenade {grenadeId} targeting {targetPos} with {explosionDelay}s delay");
        }

        private void CalculateThrowVelocity()
        {
            Vector3 displacement = TargetPosition - transform.position;
            Vector3 horizontalDisplacement = new Vector3(displacement.x, 0, displacement.z);

            float horizontalDistance = horizontalDisplacement.magnitude;
            float height = displacement.y;

            // Calculate velocity for ballistic trajectory
            float throwAngle = 45f * Mathf.Deg2Rad; // 45-degree throw angle
            float gravity = Mathf.Abs(Physics.gravity.y) * gravityMultiplier;

            float velocityMagnitude;
            if (horizontalDistance > 0.1f)
            {
                velocityMagnitude = Mathf.Sqrt((horizontalDistance * gravity) / Mathf.Sin(2 * throwAngle));
            }
            else
            {
                velocityMagnitude = throwForce; // Fallback for very short throws
            }

            Vector3 horizontalDirection = horizontalDisplacement.normalized;
            Vector3 throwDirection = horizontalDirection * Mathf.Cos(throwAngle) + Vector3.up * Mathf.Sin(throwAngle);

            velocity = throwDirection * velocityMagnitude;

            // Apply some randomness for realism
            velocity += new Vector3(
                UnityEngine.Random.Range(-1f, 1f),
                UnityEngine.Random.Range(-0.5f, 0.5f),
                UnityEngine.Random.Range(-1f, 1f)
            );
        }

        private void SimulatePhysics()
        {
            // Apply gravity
            velocity += Physics.gravity * gravityMultiplier * Time.deltaTime;

            // Move grenade
            Vector3 newPosition = transform.position + velocity * Time.deltaTime;

            // Check for ground collision
            if (CheckGroundCollision(newPosition))
            {
                LandGrenade();
                return;
            }

            // Update position
            transform.position = newPosition;

            // Rotate grenade for visual effect
            transform.Rotate(velocity.normalized * 360f * Time.deltaTime, Space.World);
        }

        private bool CheckGroundCollision(Vector3 position)
        {
            // Check if grenade hits ground or target position
            if (position.y <= TargetPosition.y + 0.1f)
            {
                return true;
            }

            // Raycast downward to check for ground collision
            if (Physics.Raycast(transform.position, Vector3.down, 0.5f, groundLayer))
            {
                return true;
            }

            return false;
        }

        private void LandGrenade()
        {
            hasLanded = true;
            landTime = Time.time;

            // Snap to target position or ground
            Vector3 landPosition = TargetPosition;
            if (Physics.Raycast(TargetPosition + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f, groundLayer))
            {
                landPosition = hit.point + Vector3.up * 0.1f;
            }
            transform.position = landPosition;

            // Stop trail effect
            if (trailEffect != null && trailEffect.isPlaying)
            {
                trailEffect.Stop();
            }

            // Play landing sound
            if (landingSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(landingSound);
            }

            // Switch to rigidbody physics for bouncing
            rb.isKinematic = false;
            rb.velocity = Vector3.zero;

            // Trigger landed event
            OnLanded?.Invoke(GrenadeId, transform.position);

            Debug.Log($"Grenade {GrenadeId} landed at {transform.position}");
        }

        private void HandleLandedState()
        {
            float timeRemaining = ExplosionDelay - (Time.time - landTime);

            // Show warning indicator 1 second before explosion
            if (timeRemaining <= 1.0f && !warningActive)
            {
                ShowWarningIndicator();
                warningActive = true;
            }

            // Note: Explosion is handled by server, not client
            // Client just waits for server explosion message
        }

        private void ShowWarningIndicator()
        {
            if (warningIndicator != null)
            {
                var warningObj = Instantiate(warningIndicator, transform.position, Quaternion.identity);
                warningObj.transform.localScale = Vector3.one * 10f; // Approximate explosion radius

                // Animate warning (pulsing red circle)
                StartCoroutine(AnimateWarning(warningObj));
            }
        }

        private IEnumerator AnimateWarning(GameObject warningObj)
        {
            float duration = 1f; // Warning duration
            float elapsed = 0f;
            Renderer renderer = warningObj.GetComponent<Renderer>();

            if (renderer != null)
            {
                Color originalColor = renderer.material.color;

                while (elapsed < duration && warningObj != null)
                {
                    float alpha = Mathf.PingPong(elapsed * 6f, 1f); // Fast pulsing
                    renderer.material.color = new Color(1f, 0f, 0f, alpha); // Red pulsing

                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            if (warningObj != null)
            {
                Destroy(warningObj);
            }
        }

        /// <summary>
        /// Called by server when grenade explodes
        /// </summary>
        public void ExplodeGrenade()
        {
            OnExploded?.Invoke(GrenadeId);

            // Create local explosion effect if needed
            // (Main explosion effects are handled by GrenadeManager)

            Debug.Log($"Grenade {GrenadeId} exploded");

            // Destroy the grenade object
            Destroy(gameObject);
        }

        void OnDrawGizmosSelected()
        {
            // Visualize trajectory in editor
            if (Application.isPlaying && !hasLanded)
            {
                Gizmos.color = Color.yellow;
                Vector3 pos = transform.position;
                Vector3 vel = velocity;

                for (int i = 0; i < 50; i++)
                {
                    float time = i * 0.1f;
                    Vector3 point = pos + vel * time + 0.5f * Physics.gravity * gravityMultiplier * time * time;
                    Gizmos.DrawWireSphere(point, 0.1f);
                }
            }

            // Visualize target position
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(TargetPosition, 0.5f);
        }
    }
}