using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// เพลง + เสียงเอฟเฟกต์ของทั้งเกม
///  - เพลงเมนู (Resources/Music/MenuBGM) เล่นในซีนที่ชื่อมีคำว่า "Menu", ซีนอื่นเล่นเพลงในเกม (Resources/Music/GameBGM)
///  - SFX อยู่ที่ Resources/SFX (Shoot, Slash, Hit, Hurt, Pickup, Door, Click)
///  - ถ้าไม่มี AudioManager ใน Scene จะสร้างเองอัตโนมัติ
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;

    [Header("Audio Clips")]
    public AudioClip bgmClip;          // (เดิม) ถ้าไม่มี GameBGM ใน Resources จะใช้ตัวนี้เป็นเพลงในเกม
    public AudioClip shootSFX;
    public AudioClip hitSFX;           // ศัตรูโดนตี
    public AudioClip expSFX;
    public AudioClip doorSFX;
    public AudioClip resonanceSFX;
    public AudioClip hurtSFX;          // ผู้เล่นเจ็บ
    public AudioClip slashSFX;         // ฟันดาบ
    public AudioClip monsterDeadSFX;   // ศัตรูตาย
    public AudioClip playerDeadSFX;    // ผู้เล่นตาย (HP หมด)
    public AudioClip clickSFX;         // กดปุ่ม UI
    public AudioClip menuBgmClip;
    public AudioClip gameBgmClip;

    private const float MenuGain = 0.6f;   // เพลงเมนูดังกว่าเพลงเกม จึงลดให้เท่ากัน
    private const float GameGain = 1.0f;

    private float bgmVolume = 0.75f;
    private float sfxVolume = 0.75f;
    private float bgmGain = 1f;

    private AudioClip proceduralResonanceClip;
    private AudioSource[][] pools;          // 0 = อาวุธ, 1 = โดนตี/เจ็บ (ไม่โดนเสียงอาวุธแย่งช่อง), 2 = อื่นๆ
    private int[] poolIndex = new int[3];
    private Coroutine musicFade;
    private readonly Dictionary<AudioClip, float> lastPlayed = new Dictionary<AudioClip, float>();
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("AudioManager");
            go.AddComponent<AudioManager>();
        }
    }

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
        bgmSource.playOnAwake = false;

        pools = new AudioSource[3][];
        for (int c = 0; c < pools.Length; c++)
        {
            pools[c] = new AudioSource[c == 2 ? 4 : 5];
            for (int i = 0; i < pools[c].Length; i++)
            {
                pools[c][i] = gameObject.AddComponent<AudioSource>();
                pools[c][i].playOnAwake = false;
            }
        }

        if (shootSFX == null) shootSFX = Resources.Load<AudioClip>("SFX/Shoot");
        if (hitSFX == null) hitSFX = Resources.Load<AudioClip>("SFX/Hit");
        if (expSFX == null) expSFX = Resources.Load<AudioClip>("SFX/Pickup");
        if (doorSFX == null) doorSFX = Resources.Load<AudioClip>("SFX/Door");
        if (resonanceSFX == null) resonanceSFX = Resources.Load<AudioClip>("SFX/Resonance");
        if (hurtSFX == null) hurtSFX = Resources.Load<AudioClip>("SFX/Hurt");
        if (slashSFX == null) slashSFX = Resources.Load<AudioClip>("SFX/Slash");
        if (clickSFX == null) clickSFX = Resources.Load<AudioClip>("SFX/Click");
        if (playerDeadSFX == null) playerDeadSFX = Resources.Load<AudioClip>("SFX/PlayerDead");
        if (monsterDeadSFX == null) monsterDeadSFX = Resources.Load<AudioClip>("SFX/MonsterDead");
        if (menuBgmClip == null) menuBgmClip = Resources.Load<AudioClip>("Music/MenuBGM");
        if (gameBgmClip == null) gameBgmClip = Resources.Load<AudioClip>("Music/GameBGM");
        if (gameBgmClip == null) gameBgmClip = bgmClip;
        if (resonanceSFX == null) proceduralResonanceClip = CreateProceduralResonanceClip();

        LoadVolumeSettings();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        PlayMusicForScene(SceneManager.GetActiveScene().name);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayMusicForScene(scene.name);
    }

    private void PlayMusicForScene(string sceneName)
    {
        bool isMenu = sceneName != null && sceneName.IndexOf("Menu", System.StringComparison.OrdinalIgnoreCase) >= 0;
        AudioClip clip = isMenu ? menuBgmClip : gameBgmClip;
        bgmGain = isMenu ? MenuGain : GameGain;
        if (bgmSource == null) return;
        if (musicFade != null) { StopCoroutine(musicFade); musicFade = null; }
        bgmSource.volume = bgmVolume * bgmGain;
        if (clip == null) return;
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;
        bgmSource.clip = clip;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    private void Update()
    {
        // เสียงคลิกปุ่ม UI ทุกปุ่มในเกม (ไม่ต้องแก้ทีละปุ่ม)
        if (clickSFX != null && Input.GetMouseButtonDown(0) && EventSystem.current != null)
        {
            PointerEventData ped = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            raycastResults.Clear();
            EventSystem.current.RaycastAll(ped, raycastResults);
            if (raycastResults.Count > 0)
            {
                Button b = raycastResults[0].gameObject.GetComponentInParent<Button>();
                if (b != null && b.interactable) PlayPooled(clickSFX, 0.8f, 0.03f, 2);
            }
        }
    }

    // ------------------------------------------------------------ SFX
    private void PlayPooled(AudioClip clip, float volume, float pitchJitter, int channel)
    {
        if (clip == null || pools == null) return;

        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(clip, out float last) && now - last < 0.03f) return;   // กันเสียงซ้อนรัวเกินไป
        lastPlayed[clip] = now;

        AudioSource[] pool = pools[channel];
        AudioSource src = pool[poolIndex[channel]];
        poolIndex[channel] = (poolIndex[channel] + 1) % pool.Length;
        src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        src.volume = Mathf.Clamp01(sfxVolume * volume);
        src.clip = clip;
        src.Play();
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        if (clip == shootSFX) PlayPooled(clip, WeaponVolume, 0.08f, 0);
        else if (clip == hitSFX) PlayPooled(clip, 1f, 0.06f, 1);
        else PlayPooled(clip, 1f, 0f, 2);
    }

    private const float WeaponVolume = 0.3f;   // เสียงอาวุธทุกชนิดเบาลง ให้ได้ยินเสียงศัตรูโดนตีชัด

    public void PlayHurtSFX() { PlayPooled(hurtSFX != null ? hurtSFX : hitSFX, 1f, 0.05f, 1); }
    public void PlayMonsterDeadSFX() { PlayPooled(monsterDeadSFX, 0.8f, 0f, 2); }
    public void PlayPlayerDeadSFX() { FadeOutMusic(0.5f); PlayPooled(playerDeadSFX, 1f, 0f, 2); }
    public void PlayEnemyHitSFX(float volume = 1f) { PlayPooled(hitSFX, volume, 0.06f, 1); }
    public void PlaySlashSFX() { PlayPooled(slashSFX != null ? slashSFX : shootSFX, WeaponVolume * 1.2f, 0.08f, 0); }

    /// <summary>เฟดเพลงออกแล้วหยุด (เพลงของซีนถัดไปจะเริ่มเองตอนโหลดซีน)</summary>
    public void FadeOutMusic(float seconds)
    {
        if (bgmSource == null || !bgmSource.isPlaying) return;
        if (musicFade != null) StopCoroutine(musicFade);
        musicFade = StartCoroutine(FadeRoutine(seconds));
    }

    private System.Collections.IEnumerator FadeRoutine(float seconds)
    {
        float start = bgmSource.volume;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            bgmSource.volume = Mathf.Lerp(start, 0f, t / seconds);
            yield return null;
        }
        bgmSource.volume = 0f;
        bgmSource.Stop();
        musicFade = null;
    }

    public void PlayResonanceSFX()
    {
        if (resonanceSFX != null)
        {
            PlaySFX(resonanceSFX);
        }
        else
        {
            if (proceduralResonanceClip == null) proceduralResonanceClip = CreateProceduralResonanceClip();
            PlaySFX(proceduralResonanceClip);
        }
    }

    private AudioClip CreateProceduralResonanceClip()
    {
        int sampleRate = 44100;
        float duration = 1.2f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

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

    // ------------------------------------------------------------ volume
    public void SetBGMVolume(float volume)
    {
        bgmVolume = volume;
        if (bgmSource != null) bgmSource.volume = volume * bgmGain;
        PlayerPrefs.SetFloat("BGMVolume", volume);
        PlayerPrefs.Save();
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = volume;
        PlayerPrefs.SetFloat("SFXVolume", volume);
        PlayerPrefs.Save();
    }

    private void LoadVolumeSettings()
    {
        bgmVolume = PlayerPrefs.GetFloat("BGMVolume", 0.75f);
        sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 0.75f);
        if (bgmSource != null) bgmSource.volume = bgmVolume * bgmGain;
    }
}
