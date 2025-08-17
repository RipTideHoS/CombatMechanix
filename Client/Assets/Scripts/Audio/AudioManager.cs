using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Simple audio manager for Combat Mechanix
/// Handles sound effects and music playback
/// </summary>
public class AudioManager : MonoBehaviour
{
    [Header("Audio Sources")]
    public AudioSource SfxAudioSource;
    public AudioSource MusicAudioSource;
    
    [Header("Volume Settings")]
    [Range(0f, 1f)]
    public float MasterVolume = 1f;
    [Range(0f, 1f)]
    public float SfxVolume = 0.8f;
    [Range(0f, 1f)]
    public float MusicVolume = 0.6f;
    
    [Header("Level Up Audio")]
    public AudioClip LevelUpSound;
    public float LevelUpVolume = 0.9f;
    
    [Header("Combat Audio")]
    public AudioClip AttackSound;
    public AudioClip HitSound;
    public AudioClip EnemyDeathSound;
    public float CombatVolume = 0.7f;
    
    // Singleton instance
    public static AudioManager Instance { get; private set; }
    
    // Audio clip cache
    private Dictionary<string, AudioClip> _audioClips = new Dictionary<string, AudioClip>();
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioSources();
            LoadAudioClips();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void InitializeAudioSources()
    {
        // Create SFX audio source if not assigned
        if (SfxAudioSource == null)
        {
            GameObject sfxObj = new GameObject("SFX AudioSource");
            sfxObj.transform.SetParent(transform);
            SfxAudioSource = sfxObj.AddComponent<AudioSource>();
        }
        
        // Create Music audio source if not assigned
        if (MusicAudioSource == null)
        {
            GameObject musicObj = new GameObject("Music AudioSource");
            musicObj.transform.SetParent(transform);
            MusicAudioSource = musicObj.AddComponent<AudioSource>();
        }
        
        // Configure audio sources
        SfxAudioSource.playOnAwake = false;
        SfxAudioSource.loop = false;
        SfxAudioSource.volume = SfxVolume * MasterVolume;
        
        MusicAudioSource.playOnAwake = false;
        MusicAudioSource.loop = true;
        MusicAudioSource.volume = MusicVolume * MasterVolume;
        
        Debug.Log("[AudioManager] Audio sources initialized");
    }
    
    private void LoadAudioClips()
    {
        // Cache audio clips for quick access
        if (LevelUpSound != null)
            _audioClips["levelup"] = LevelUpSound;
        if (AttackSound != null)
            _audioClips["attack"] = AttackSound;
        if (HitSound != null)
            _audioClips["hit"] = HitSound;
        if (EnemyDeathSound != null)
            _audioClips["enemy_death"] = EnemyDeathSound;
        
        Debug.Log($"[AudioManager] Loaded {_audioClips.Count} audio clips");
    }
    
    /// <summary>
    /// Play a sound effect
    /// </summary>
    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        if (clip != null && SfxAudioSource != null)
        {
            SfxAudioSource.PlayOneShot(clip, volume * SfxVolume * MasterVolume);
        }
    }
    
    /// <summary>
    /// Play a sound effect by name
    /// </summary>
    public void PlaySfx(string clipName, float volume = 1f)
    {
        if (_audioClips.TryGetValue(clipName.ToLower(), out AudioClip clip))
        {
            PlaySfx(clip, volume);
        }
        else
        {
            Debug.LogWarning($"[AudioManager] Audio clip '{clipName}' not found");
        }
    }
    
    /// <summary>
    /// Play level up sound effect
    /// </summary>
    public void PlayLevelUpSound()
    {
        if (LevelUpSound != null)
        {
            PlaySfx(LevelUpSound, LevelUpVolume);
            Debug.Log("[AudioManager] Playing level up sound");
        }
        else
        {
            Debug.LogWarning("[AudioManager] Level up sound not assigned");
        }
    }
    
    /// <summary>
    /// Play combat sound effect
    /// </summary>
    public void PlayCombatSound(string soundType)
    {
        switch (soundType.ToLower())
        {
            case "attack":
                if (AttackSound != null)
                    PlaySfx(AttackSound, CombatVolume);
                break;
            case "hit":
                if (HitSound != null)
                    PlaySfx(HitSound, CombatVolume);
                break;
            case "enemy_death":
                if (EnemyDeathSound != null)
                    PlaySfx(EnemyDeathSound, CombatVolume);
                break;
            default:
                Debug.LogWarning($"[AudioManager] Unknown combat sound type: {soundType}");
                break;
        }
    }
    
    /// <summary>
    /// Play background music
    /// </summary>
    public void PlayMusic(AudioClip musicClip, bool loop = true)
    {
        if (musicClip != null && MusicAudioSource != null)
        {
            MusicAudioSource.clip = musicClip;
            MusicAudioSource.loop = loop;
            MusicAudioSource.Play();
            Debug.Log($"[AudioManager] Playing music: {musicClip.name}");
        }
    }
    
    /// <summary>
    /// Stop background music
    /// </summary>
    public void StopMusic()
    {
        if (MusicAudioSource != null)
        {
            MusicAudioSource.Stop();
            Debug.Log("[AudioManager] Stopped music");
        }
    }
    
    /// <summary>
    /// Update master volume
    /// </summary>
    public void SetMasterVolume(float volume)
    {
        MasterVolume = Mathf.Clamp01(volume);
        UpdateAudioSourceVolumes();
    }
    
    /// <summary>
    /// Update SFX volume
    /// </summary>
    public void SetSfxVolume(float volume)
    {
        SfxVolume = Mathf.Clamp01(volume);
        UpdateAudioSourceVolumes();
    }
    
    /// <summary>
    /// Update music volume
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        MusicVolume = Mathf.Clamp01(volume);
        UpdateAudioSourceVolumes();
    }
    
    private void UpdateAudioSourceVolumes()
    {
        if (SfxAudioSource != null)
            SfxAudioSource.volume = SfxVolume * MasterVolume;
        if (MusicAudioSource != null)
            MusicAudioSource.volume = MusicVolume * MasterVolume;
    }
    
    /// <summary>
    /// Create a placeholder audio clip for testing
    /// </summary>
    public static AudioClip CreatePlaceholderAudioClip(string name = "Placeholder")
    {
        // Create a simple beep sound for testing
        int sampleRate = 44100;
        float duration = 0.5f;
        int samples = Mathf.RoundToInt(sampleRate * duration);
        
        AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
        float[] data = new float[samples];
        
        // Generate a simple sine wave beep
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            data[i] = Mathf.Sin(2 * Mathf.PI * 440 * t) * 0.3f; // 440Hz tone
        }
        
        clip.SetData(data, 0);
        return clip;
    }
    
    /// <summary>
    /// Initialize with placeholder sounds for testing
    /// </summary>
    public void InitializePlaceholderSounds()
    {
        if (LevelUpSound == null)
        {
            LevelUpSound = CreatePlaceholderAudioClip("LevelUpBeep");
            _audioClips["levelup"] = LevelUpSound;
            Debug.Log("[AudioManager] Created placeholder level up sound");
        }
        
        if (AttackSound == null)
        {
            AttackSound = CreatePlaceholderAudioClip("AttackBeep");
            _audioClips["attack"] = AttackSound;
        }
        
        if (HitSound == null)
        {
            HitSound = CreatePlaceholderAudioClip("HitBeep");
            _audioClips["hit"] = HitSound;
        }
        
        if (EnemyDeathSound == null)
        {
            EnemyDeathSound = CreatePlaceholderAudioClip("DeathBeep");
            _audioClips["enemy_death"] = EnemyDeathSound;
        }
    }
}