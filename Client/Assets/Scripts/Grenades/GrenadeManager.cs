using UnityEngine;
using System.Collections.Generic;
using System.Collections;

namespace CombatMechanix.Unity
{
    /// <summary>
    /// Client-side grenade manager that handles grenade spawning, physics, and effects
    /// </summary>
    public class GrenadeManager : MonoBehaviour
    {
        [Header("Grenade Prefabs")]
        public GameObject fragGrenadePrefab;
        public GameObject smokeGrenadePrefab;
        public GameObject flashGrenadePrefab;

        [Header("Effect Prefabs")]
        public GameObject explosionEffectPrefab;
        public GameObject smokeEffectPrefab;
        public GameObject flashEffectPrefab;
        public GameObject warningIndicatorPrefab;

        [Header("Audio")]
        public AudioClip grenadeThrowSound;
        public AudioClip explosionSound;
        public AudioClip warningSound;

        private Dictionary<string, GameObject> activeGrenades = new Dictionary<string, GameObject>();
        private AudioSource audioSource;

        void Start()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            // Subscribe to grenade network events
            NetworkManager.OnGrenadeSpawn += OnGrenadeSpawn;
            NetworkManager.OnGrenadeWarning += OnGrenadeWarning;
            NetworkManager.OnGrenadeExplosion += OnGrenadeExplosion;
            NetworkManager.OnGrenadeError += OnGrenadeError;
        }

        void OnDestroy()
        {
            // Unsubscribe from events
            NetworkManager.OnGrenadeSpawn -= OnGrenadeSpawn;
            NetworkManager.OnGrenadeWarning -= OnGrenadeWarning;
            NetworkManager.OnGrenadeExplosion -= OnGrenadeExplosion;
            NetworkManager.OnGrenadeError -= OnGrenadeError;
        }

        /// <summary>
        /// Handle grenade spawn message from server
        /// </summary>
        private void OnGrenadeSpawn(NetworkMessages.GrenadeSpawnMessage grenadeData)
        {
            GameObject prefab = GetGrenadePrefab(grenadeData.GrenadeType);
            if (prefab == null)
            {
                Debug.LogError($"No prefab found for grenade type: {grenadeData.GrenadeType}");
                return;
            }

            Vector3 startPos = new Vector3(grenadeData.StartPosition.X, grenadeData.StartPosition.Y, grenadeData.StartPosition.Z);
            Vector3 targetPos = new Vector3(grenadeData.TargetPosition.X, grenadeData.TargetPosition.Y, grenadeData.TargetPosition.Z);

            GameObject grenadeObj = Instantiate(prefab, startPos, Quaternion.identity);
            var grenadeScript = grenadeObj.GetComponent<Grenade>();

            if (grenadeScript == null)
            {
                grenadeScript = grenadeObj.AddComponent<Grenade>();
            }

            grenadeScript.Initialize(grenadeData.GrenadeId, grenadeData.GrenadeType, targetPos, grenadeData.ExplosionDelay);
            grenadeScript.OnLanded += OnGrenadeLanded;
            grenadeScript.OnExploded += OnGrenadeExploded;

            activeGrenades[grenadeData.GrenadeId] = grenadeObj;

            // Play throw sound
            if (grenadeThrowSound != null)
            {
                audioSource.PlayOneShot(grenadeThrowSound);
            }

            Debug.Log($"Spawned grenade {grenadeData.GrenadeId} of type {grenadeData.GrenadeType}");
        }

        /// <summary>
        /// Handle grenade warning message from server
        /// </summary>
        private void OnGrenadeWarning(NetworkMessages.GrenadeWarningMessage warningData)
        {
            Vector3 explosionPos = new Vector3(warningData.ExplosionPosition.X, warningData.ExplosionPosition.Y, warningData.ExplosionPosition.Z);

            if (warningIndicatorPrefab != null)
            {
                GameObject warningObj = Instantiate(warningIndicatorPrefab, explosionPos, Quaternion.identity);
                warningObj.transform.localScale = Vector3.one * warningData.ExplosionRadius * 2;

                // Animate warning indicator
                StartCoroutine(AnimateWarning(warningObj, warningData.TimeToExplosion));
            }

            // Play warning sound
            if (warningSound != null)
            {
                audioSource.PlayOneShot(warningSound);
            }

            Debug.Log($"Warning: Grenade {warningData.GrenadeId} exploding in {warningData.TimeToExplosion}s");
        }

        /// <summary>
        /// Handle grenade explosion message from server
        /// </summary>
        private void OnGrenadeExplosion(NetworkMessages.GrenadeExplosionMessage explosionData)
        {
            Vector3 explosionPos = new Vector3(explosionData.ExplosionPosition.X, explosionData.ExplosionPosition.Y, explosionData.ExplosionPosition.Z);

            // Create explosion effect
            CreateExplosionEffect(explosionData.GrenadeId, explosionPos, explosionData.ExplosionRadius);

            // Remove grenade from active list
            if (activeGrenades.TryGetValue(explosionData.GrenadeId, out GameObject grenadeObj))
            {
                if (grenadeObj != null)
                {
                    Destroy(grenadeObj);
                }
                activeGrenades.Remove(explosionData.GrenadeId);
            }

            // Apply screen shake for nearby explosions
            ApplyScreenShake(explosionPos, explosionData.ExplosionRadius);

            Debug.Log($"Grenade {explosionData.GrenadeId} exploded, {explosionData.DamagedTargets.Count} targets affected");
        }

        /// <summary>
        /// Handle grenade error message from server
        /// </summary>
        private void OnGrenadeError(NetworkMessages.GrenadeErrorMessage errorData)
        {
            Debug.LogWarning($"Grenade Error: {errorData.ErrorMessage}");

            // Show error message to player
            var uiManager = FindObjectOfType<UIManager>();
            if (uiManager != null)
            {
                // You might want to add a method to show error messages in UIManager
                Debug.LogWarning($"Show error to player: {errorData.ErrorMessage}");
            }
        }

        private GameObject GetGrenadePrefab(string grenadeType)
        {
            return grenadeType switch
            {
                "frag_grenade" => fragGrenadePrefab,
                "smoke_grenade" => smokeGrenadePrefab,
                "flash_grenade" => flashGrenadePrefab,
                _ => fragGrenadePrefab // Default to frag grenade
            };
        }

        private void CreateExplosionEffect(string grenadeId, Vector3 position, float radius)
        {
            // Determine effect type based on grenade ID or type
            GameObject effectPrefab = explosionEffectPrefab; // Default to explosion

            // You might want to track grenade types to show appropriate effects
            if (activeGrenades.ContainsKey(grenadeId))
            {
                var grenade = activeGrenades[grenadeId].GetComponent<Grenade>();
                if (grenade != null)
                {
                    effectPrefab = grenade.GrenadeType switch
                    {
                        "smoke_grenade" => smokeEffectPrefab ?? explosionEffectPrefab,
                        "flash_grenade" => flashEffectPrefab ?? explosionEffectPrefab,
                        _ => explosionEffectPrefab
                    };
                }
            }

            if (effectPrefab != null)
            {
                GameObject effect = Instantiate(effectPrefab, position, Quaternion.identity);

                // Scale effect based on explosion radius
                effect.transform.localScale = Vector3.one * (radius / 5f); // Normalize to default radius of 5

                // Auto-destroy effect after 5 seconds
                Destroy(effect, 5f);
            }

            // Play explosion sound
            if (explosionSound != null)
            {
                audioSource.PlayOneShot(explosionSound);
            }
        }

        private void ApplyScreenShake(Vector3 explosionPos, float explosionRadius)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null) return;

            float distance = Vector3.Distance(mainCamera.transform.position, explosionPos);
            if (distance > explosionRadius * 2) return; // Screen shake range is 2x explosion radius

            float intensity = Mathf.Clamp01(1f - (distance / (explosionRadius * 2)));
            StartCoroutine(ScreenShake(intensity * 0.5f, 0.3f));
        }

        private IEnumerator ScreenShake(float intensity, float duration)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null) yield break;

            Vector3 originalPosition = mainCamera.transform.position;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float x = Random.Range(-1f, 1f) * intensity;
                float y = Random.Range(-1f, 1f) * intensity;

                mainCamera.transform.position = originalPosition + new Vector3(x, y, 0);

                elapsed += Time.deltaTime;
                yield return null;
            }

            mainCamera.transform.position = originalPosition;
        }

        private IEnumerator AnimateWarning(GameObject warningObj, float duration)
        {
            float elapsed = 0f;
            Renderer renderer = warningObj.GetComponent<Renderer>();
            Color originalColor = renderer.material.color;

            while (elapsed < duration && warningObj != null)
            {
                float alpha = Mathf.PingPong(elapsed * 4f, 1f); // Pulsing effect
                renderer.material.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (warningObj != null)
            {
                Destroy(warningObj);
            }
        }

        private void OnGrenadeLanded(string grenadeId, Vector3 position)
        {
            Debug.Log($"Grenade {grenadeId} landed at {position}");
        }

        private void OnGrenadeExploded(string grenadeId)
        {
            Debug.Log($"Grenade {grenadeId} exploded locally");
        }
    }
}