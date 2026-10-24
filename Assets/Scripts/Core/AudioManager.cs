using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Audio Clips")]
    public AudioClip bgmClip;
    public AudioClip shootSFX;
    public AudioClip hitSFX;
    public AudioClip expSFX;
    public AudioClip doorSFX;
    public AudioClip resonanceSFX;

    private AudioClip proceduralResonanceClip;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        if (shootSFX == null) shootSFX = Resources.Load<AudioClip>("SFX/Shoot");
        if (hitSFX == null) hitSFX = Resources.Load<AudioClip>("SFX/Hit");
        if (expSFX == null) expSFX = Resources.Load<AudioClip>("SFX/Pickup");
        if (doorSFX == null) doorSFX = Resources.Load<AudioClip>("SFX/Door");
        if (resonanceSFX == null) resonanceSFX = Resources.Load<AudioClip>("SFX/Resonance");
        if (resonanceSFX == null) proceduralResonanceClip = CreateProceduralResonanceClip();
        LoadVolumeSettings();
    }

    private void Start()
    {
        if (bgmClip != null && bgmSource != null)
        {
            bgmSource.clip = bgmClip;
            bgmSource.loop = true;
            bgmSource.Play();
        }
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    public void PlayResonanceSFX()
    {
        if (resonanceSFX != null)
        {
            PlaySFX(resonanceSFX);
        }
        else
        {
            if (proceduralResonanceClip == null)
            {
                proceduralResonanceClip = CreateProceduralResonanceClip();
            }
            PlaySFX(proceduralResonanceClip);
        }
    }

    private AudioClip CreateProceduralResonanceClip()
    {
        int sampleRate = 44100;
        float duration = 1.2f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        // Harmonically rich resonant chord (D5 - A5 - D6 - A6 shimmer chime with exponential decay)
        float f1 = 587.33f;
        float f2 = 880.00f;
        float f3 = 1174.66f;
        float f4 = 1760.00f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Exp(-3.2f * t);
            float shimmer = Mathf.Sin(2f * Mathf.PI * 6.5f * t) * 0.15f;

            float s = (Mathf.Sin(2f * Mathf.PI * f1 * t) * 0.35f)
                    + (Mathf.Sin(2f * Mathf.PI * f2 * t) * 0.30f)
                    + (Mathf.Sin(2f * Mathf.PI * f3 * t) * 0.20f)
                    + (Mathf.Sin(2f * Mathf.PI * f4 * t) * 0.15f);

            samples[i] = (s * (1f + shimmer)) * envelope * 0.6f;
        }

        AudioClip clip = AudioClip.Create("SFX_Resonance_Synth", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    public void SetBGMVolume(float volume)
    {
        bgmSource.volume = volume;
        PlayerPrefs.SetFloat("BGMVolume", volume);
        PlayerPrefs.Save();
    }

    public void SetSFXVolume(float volume)
    {
        sfxSource.volume = volume;
        PlayerPrefs.SetFloat("SFXVolume", volume);
        PlayerPrefs.Save();
    }

    private void LoadVolumeSettings()
    {
        float bgmVol = PlayerPrefs.GetFloat("BGMVolume", 0.75f);
        float sfxVol = PlayerPrefs.GetFloat("SFXVolume", 0.75f);

        if (bgmSource != null) bgmSource.volume = bgmVol;
        if (sfxSource != null) sfxSource.volume = sfxVol;
    }
}
