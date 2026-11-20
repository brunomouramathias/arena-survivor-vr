using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArenaSurvivor.Core
{
    // Estados do jogo
    public enum GameState { Menu, Playing, Shop, Paused, GameOver, Victory }

    // Gerenciador principal do jogo — singleton
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Estado Atual")]
        [SerializeField] private GameState currentState = GameState.Menu;

        public GameState CurrentState => currentState;

        // Eventos para notificar outros sistemas
        public static event Action<GameState> OnGameStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
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
            if (scene.name == "Arena")
                SetState(GameState.Playing);
            else if (scene.name == "MainMenu")
                SetState(GameState.Menu);
        }

        private void Start()
        {
            if (SceneManager.GetActiveScene().name == "Arena")
                SetState(GameState.Playing);
            else
                SetState(GameState.Menu);
        }

        public void SetState(GameState newState)
        {
            currentState = newState;
            OnGameStateChanged?.Invoke(newState);

            // Controla o tempo conforme o estado
            switch (newState)
            {
                case GameState.Playing:
                    Time.timeScale = 1f;
                    break;
                case GameState.Paused:
                case GameState.Shop:
                case GameState.GameOver:
                case GameState.Victory:
                    Time.timeScale = 0f;
                    break;
                case GameState.Menu:
                    Time.timeScale = 1f;
                    break;
            }
        }

        public void StartGame()
        {
            SceneManager.LoadScene("Arena");
        }

        public void RestartGame()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void GoToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
        }

        public void QuitGame()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        public bool IsPlaying() => currentState == GameState.Playing;

        private float uiMissingSince = -1f;

        private void Update()
        {
            // Trava de segurança: em combate o tempo NUNCA pode ficar parado
            if (currentState == GameState.Playing && Time.timeScale <= 0f)
                Time.timeScale = 1f;

            // Travas de segurança: loja/pausa com timeScale 0 e SEM tela visível travariam o jogo
            // para sempre (foi o que acontecia ao pegar o ícone de poder do fim da onda).
            if (currentState == GameState.Shop || currentState == GameState.Paused)
            {
                if (HasVisibleScreenFor(currentState))
                {
                    uiMissingSince = -1f;
                }
                else
                {
                    if (uiMissingSince < 0f) uiMissingSince = Time.unscaledTime;
                    else if (Time.unscaledTime - uiMissingSince > 1.5f)
                    {
                        Debug.LogWarning($"[GameManager] {currentState} sem tela utilizável — voltando ao combate para não travar.");
                        uiMissingSince = -1f;
                        SetState(GameState.Playing);
                    }
                }
            }
            else uiMissingSince = -1f;
        }

        private static UI.UIManager cachedUIManager;
        private static bool uiManagerSearched;

        private static bool HasVisibleScreenFor(GameState state)
        {
            if (state == GameState.Shop)
                return UI.ShopScreen.Instance != null && UI.ShopScreen.Instance.IsVisible;

            // Pausa: sem UIManager ativo ninguém consegue retomar pela tecla Esc
            if (!uiManagerSearched)
            {
                cachedUIManager = FindAnyObjectByType<UI.UIManager>();
                uiManagerSearched = true;
            }
            return cachedUIManager != null;
        }
    }
}
