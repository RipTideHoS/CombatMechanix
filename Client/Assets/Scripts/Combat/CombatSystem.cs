using System.Collections;
using UnityEngine;

public class CombatSystem : MonoBehaviour
{
    [Header("Combat Effects")]
    public ParticleSystem AttackEffect;
    public ParticleSystem HitEffect;
    public AudioClip AttackSound;
    public AudioClip HitSound;

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
        Debug.Log($"Attack effect: {attackerPos} -> {targetPos}");
        
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
}