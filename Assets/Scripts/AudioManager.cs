using UnityEngine;
using System.Collections;

public class AudioManager : MonoBehaviour
{
    // ----------------------------------------------------------------
    //  Singleton
    // ----------------------------------------------------------------
    public static AudioManager Instance { get; private set; }

    // ----------------------------------------------------------------
    //  Audio Sources
    // ----------------------------------------------------------------
    [Header("Audio Sources")]
    [Tooltip("AudioSource khusus BGM (loop). Attach komponen AudioSource di child object 'BGM'.")]
    public AudioSource bgmSource;

    [Tooltip("AudioSource khusus SFX (one-shot). Attach komponen AudioSource di child object 'SFX'.")]
    public AudioSource sfxSource;

    // ----------------------------------------------------------------
    //  BGM Clips
    // ----------------------------------------------------------------
    [Header("BGM Clips")]
    [Tooltip("Musik latar gameplay utama")]
    public AudioClip gameplayBGM;

    [Tooltip("Musik latar menu utama")]
    public AudioClip menuBGM;

    // ----------------------------------------------------------------
    //  SFX Clips
    // ----------------------------------------------------------------
    [Header("SFX - Bomb")]
    [Tooltip("Suara saat bom diletakkan")]
    public AudioClip bombDropSFX;

    [Tooltip("Suara ledakan bom")]
    public AudioClip explosionSFX;

    [Header("SFX - Player")]
    [Tooltip("Suara saat pemain mati")]
    public AudioClip deathSFX;

    [Tooltip("Suara saat mengambil item pickup")]
    public AudioClip powerUpSFX;

    [Header("SFX - UI")]
    [Tooltip("Suara saat klik tombol di menu")]
    public AudioClip buttonClickSFX;

    [Tooltip("Suara menang / round over")]
    public AudioClip victorySFX;

    // ----------------------------------------------------------------
    //  Volume Settings (disimpan via PlayerPrefs)
    // ----------------------------------------------------------------
    [Header("Default Volume (0-1)")]
    [Range(0f, 1f)] public float defaultBGMVolume = 0.6f;
    [Range(0f, 1f)] public float defaultSFXVolume = 1.0f;

    private const string PrefBGM = "Vol_BGM";
    private const string PrefSFX = "Vol_SFX";

    // ----------------------------------------------------------------
    //  Properties
    // ----------------------------------------------------------------
    public float BGMVolume
    {
        get => bgmSource != null ? bgmSource.volume : 0f;
        set
        {
            if (bgmSource != null) bgmSource.volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(PrefBGM, Mathf.Clamp01(value));
        }
    }

    public float SFXVolume
    {
        get => sfxSource != null ? sfxSource.volume : 0f;
        set
        {
            if (sfxSource != null) sfxSource.volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(PrefSFX, Mathf.Clamp01(value));
        }
    }

    // ----------------------------------------------------------------
    //  Lifecycle
    // ----------------------------------------------------------------
    private void Awake()
    {
        // Singleton — satu instance selama game berjalan
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // Instance kedua (dari scene reload): sync clips lalu hancurkan
            if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        ValidateSources();
        LoadVolumes();
    }

    private void ValidateSources()
    {
        // Auto-create AudioSources jika belum di-assign di Inspector
        if (bgmSource == null)
        {
            GameObject bgmObj = new GameObject("BGM");
            bgmObj.transform.SetParent(transform);
            bgmSource = bgmObj.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
        }

        if (sfxSource == null)
        {
            GameObject sfxObj = new GameObject("SFX");
            sfxObj.transform.SetParent(transform);
            sfxSource = sfxObj.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
        }
    }

    private void LoadVolumes()
    {
        bgmSource.volume = PlayerPrefs.GetFloat(PrefBGM, defaultBGMVolume);
        sfxSource.volume = PlayerPrefs.GetFloat(PrefSFX, defaultSFXVolume);
    }

    // ----------------------------------------------------------------
    //  BGM
    // ----------------------------------------------------------------

    /// <summary>Play BGM dari clip yang diberi. Jika same clip & sudah play, skip.</summary>
    public void PlayBGM(AudioClip clip)
    {
        if (clip == null || bgmSource == null) return;

        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        bgmSource.clip = clip;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    /// <summary>Play BGM gameplay.</summary>
    public void PlayGameplayBGM() => PlayBGM(gameplayBGM);

    /// <summary>Play BGM menu.</summary>
    public void PlayMenuBGM() => PlayBGM(menuBGM);

    /// <summary>Stop BGM.</summary>
    public void StopBGM()
    {
        if (bgmSource != null) bgmSource.Stop();
    }

    /// <summary>Pause BGM.</summary>
    public void PauseBGM()
    {
        if (bgmSource != null) bgmSource.Pause();
    }

    /// <summary>Resume BGM.</summary>
    public void ResumeBGM()
    {
        if (bgmSource != null) bgmSource.UnPause();
    }

    /// <summary>Fade BGM ke volume target dalam durasi tertentu.</summary>
    public void FadeBGM(float targetVolume, float duration)
    {
        if (bgmSource == null) return;
        StopAllCoroutines();
        StartCoroutine(FadeBGMCoroutine(targetVolume, duration));
    }

    private IEnumerator FadeBGMCoroutine(float target, float duration)
    {
        float start = bgmSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }

        bgmSource.volume = target;
        if (target <= 0f) bgmSource.Stop();
    }

    // ----------------------------------------------------------------
    //  SFX — Generic
    // ----------------------------------------------------------------

    /// <summary>Play sembarang AudioClip sebagai SFX.</summary>
    public void PlaySFX(AudioClip clip)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, sfxSource.volume);
    }

    /// <summary>Play SFX dengan volume modifier (0-1 relatif terhadap SFXVolume).</summary>
    public void PlaySFX(AudioClip clip, float volumeScale)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, sfxSource.volume * Mathf.Clamp01(volumeScale));
    }

    // ----------------------------------------------------------------
    //  SFX — Game Events (shortcut methods)
    // ----------------------------------------------------------------

    /// <summary>Dipanggil saat bom diletakkan.</summary>
    public void PlayBombDrop() => PlaySFX(bombDropSFX);

    /// <summary>Dipanggil saat bom meledak.</summary>
    public void PlayExplosion() => PlaySFX(explosionSFX);

    /// <summary>Dipanggil saat pemain mati.</summary>
    public void PlayDeath() => PlaySFX(deathSFX);

    /// <summary>Dipanggil saat item pickup diambil.</summary>
    public void PlayPowerUp() => PlaySFX(powerUpSFX);

    /// <summary>Dipanggil saat klik tombol UI.</summary>
    public void PlayButtonClick() => PlaySFX(buttonClickSFX);

    /// <summary>Dipanggil saat ronde selesai / menang.</summary>
    public void PlayVictory() => PlaySFX(victorySFX);

    // ----------------------------------------------------------------
    //  Mute
    // ----------------------------------------------------------------

    public bool IsBGMMuted => bgmSource != null && bgmSource.mute;
    public bool IsSFXMuted => sfxSource != null && sfxSource.mute;

    public void SetBGMMute(bool mute)
    {
        if (bgmSource != null) bgmSource.mute = mute;
    }

    public void SetSFXMute(bool mute)
    {
        if (sfxSource != null) sfxSource.mute = mute;
    }

    public void ToggleBGMMute()  => SetBGMMute(!IsBGMMuted);
    public void ToggleSFXMute()  => SetSFXMute(!IsSFXMuted);
}