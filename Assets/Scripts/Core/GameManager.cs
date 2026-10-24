using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Core Game Manager responsible for scene initialization, Time.timeScale safety,
/// pause and game-over state reset, and overall run lifecycle.
/// </summary>
[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public bool isGameOver = false;
    public bool isPaused = false;

    public bool IsGameOver { get => isGameOver; set => isGameOver = value; }
    public bool IsPaused { get => isPaused; set => isPaused = value; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        Time.timeScale = 1f;
        if (Instance == null && FindFirstObjectByType<GameManager>() == null)
        {
            GameObject gm = new GameObject("GameManager");
            gm.AddComponent<GameManager>();
            DontDestroyOnLoad(gm);
        }
    }

    private void Awake()
    {
        // 1. Force reset timeScale immediately as the very first operation
        Time.timeScale = 1f;
        isGameOver = false;
        isPaused = false;

        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Time.timeScale = 1f;
        isGameOver = false;
        isPaused = false;
    }

    private void Start()
    {
        // Ensure timeScale is always running when the game starts
        Time.timeScale = 1f;
        isGameOver = false;
        isPaused = false;
    }

    private void Update()
    {
        // If PauseManager is present in the scene, PauseManager handles ESC directly to avoid double-toggling
        if (FindFirstObjectByType<PauseManager>() != null) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePauseMenu();
        }
    }

    public void ResetRunState()
    {
        isGameOver = false;
        isPaused = false;
        Time.timeScale = 1f;
    }

    public void TogglePauseMenu()
    {
        PauseManager pm = FindFirstObjectByType<PauseManager>();
        if (pm != null)
        {
            pm.TogglePause();
        }
        else
        {
            SetPaused(!isPaused);
        }
    }

    public void SetPaused(bool paused)
    {
        isPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
    }

    public void SetGameOver(bool gameOver)
    {
        isGameOver = gameOver;
        if (gameOver)
        {
            Time.timeScale = 0f;
        }
    }
}
