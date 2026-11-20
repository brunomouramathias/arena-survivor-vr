using UnityEngine;
using ArenaSurvivor.Core;

namespace ArenaSurvivor.UI
{
    // Gerencia a visibilidade dos painéis de UI conforme o estado do jogo
    public class UIManager : MonoBehaviour
    {
        [Header("Painéis")]
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject shopPanel;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private GameObject victoryPanel;

        private bool panelsSetupForVR = false;
        private float stateEnterTime;

        private void Awake()
        {
            // Busca referências por código se não estiverem atribuídas via Inspector
            if (hudPanel == null)
            {
                var t = transform.Find("HUD_Panel") ?? transform.Find("HUD");
                if (t != null) hudPanel = t.gameObject;
                else hudPanel = gameObject; // Fallback: o próprio canvas/container
            }

            if (shopPanel == null)
            {
                var t = transform.Find("Shop_Panel") ?? transform.Find("ShopPanel") ?? transform.Find("Canvas_Shop");
                if (t != null) shopPanel = t.gameObject;
            }

            if (pausePanel == null)
            {
                var t = transform.Find("Pause_Panel") ?? transform.Find("PausePanel");
                if (t != null) pausePanel = t.gameObject;
            }

            if (gameOverPanel == null)
            {
                var t = transform.Find("GameOver_Panel") ?? transform.Find("GameOverPanel");
                if (t != null) gameOverPanel = t.gameObject;
            }

            if (victoryPanel == null)
            {
                var t = transform.Find("Victory_Panel") ?? transform.Find("VictoryPanel");
                if (t != null) victoryPanel = t.gameObject;
            }
        }

        private void Start()
        {
            GameManager.OnGameStateChanged += OnStateChanged;
            OnStateChanged(GameManager.Instance?.CurrentState ?? GameState.Playing);
            
            SetupPanelsForVR();
        }

        private void SetupPanelsForVR()
        {
            if (panelsSetupForVR) return;
            
            SetupPanelForVR(hudPanel);
            SetupPanelForVR(shopPanel);
            SetupPanelForVR(pausePanel);
            SetupPanelForVR(gameOverPanel);
            SetupPanelForVR(victoryPanel);
            
            panelsSetupForVR = true;
        }

        private void SetupPanelForVR(GameObject panel)
        {
            if (panel == null) return;
            
            var canvas = panel.GetComponent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.transform.SetParent(null);
                canvas.transform.localScale = Vector3.one * 0.01f;
            }

            var follow = panel.GetComponent<CanvasWorldSpaceFollow>();
            if (follow == null)
            {
                follow = panel.AddComponent<CanvasWorldSpaceFollow>();
            }
            
            follow.targetCamera = Camera.main;
            follow.offset = new Vector3(0f, 0.2f, 1.5f);
            follow.followPosition = true;
            follow.followRotation = true;
            follow.onlyYawRotation = true;
        }

        private void OnDestroy()
        {
            GameManager.OnGameStateChanged -= OnStateChanged;
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            var input = InputService.Instance;

            // Esc sempre tem saída (nunca deixa o jogo travado em timeScale 0)
            if (input != null && input.PauseDown)
            {
                switch (gm.CurrentState)
                {
                    case GameState.Playing: gm.SetState(GameState.Paused); break;
                    case GameState.Paused:  gm.SetState(GameState.Playing); break;
                    case GameState.Shop:    gm.SetState(GameState.Playing); break;
                    case GameState.GameOver:
                    case GameState.Victory: gm.GoToMenu(); break;
                }
            }

            switch (gm.CurrentState)
            {
                case GameState.Shop:
                {
                    // Atalhos de teclado para comprar e seguir
                    if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.N))
                        gm.SetState(GameState.Playing);
                    else if (Time.unscaledTime - stateEnterTime > 1.5f && !HasUsableButton(shopPanel))
                        ResumeFromBrokenPanel("Loja");
                    break;
                }

                case GameState.Paused:
                    if (Time.unscaledTime - stateEnterTime > 1.5f && !HasUsableButton(pausePanel))
                        ResumeFromBrokenPanel("Pausa");
                    break;

                case GameState.GameOver:
                case GameState.Victory:
                {
                    if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                        gm.RestartGame();
                    else if (Time.unscaledTime - stateEnterTime > 8f &&
                             Object.FindAnyObjectByType<GameHUD>() == null &&
                             !HasUsableButton(gameOverPanel) && !HasUsableButton(victoryPanel))
                    {
                        Debug.LogWarning($"[UI] {gm.CurrentState}: painel sem botão utilizável — reiniciando para evitar travamento.");
                        gm.RestartGame();
                    }
                    break;
                }
            }
        }

        private void ResumeFromBrokenPanel(string label)
        {
            Debug.LogWarning($"[UI] {label}: painel sem botão utilizável — retomando o jogo (evita travamento em timeScale 0).");
            GameManager.Instance?.SetState(GameState.Playing);
        }

        private static bool HasUsableButton(GameObject panel)
        {
            if (panel == null || !panel.activeInHierarchy) return false;
            foreach (var b in panel.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                if (b != null && b.interactable) return true;
            foreach (var s in panel.GetComponentsInChildren<UnityEngine.UI.Selectable>(true))
                if (s != null && s.interactable) return true;
            return false;
        }

        private void OnStateChanged(GameState state)
        {
            stateEnterTime = Time.unscaledTime;
            // Checagens com '!= null' explícito evitam UnassignedReferenceException nativa do Unity
            if (hudPanel != null && hudPanel != gameObject)
                hudPanel.SetActive(state == GameState.Playing || state == GameState.Shop);

            if (shopPanel != null)
                shopPanel.SetActive(state == GameState.Shop);

            if (pausePanel != null)
                pausePanel.SetActive(state == GameState.Paused);

            if (gameOverPanel != null)
                gameOverPanel.SetActive(state == GameState.GameOver);

            if (victoryPanel != null)
                victoryPanel.SetActive(state == GameState.Victory);
        }

        public void BindPanels(GameObject hud, GameObject shop, GameObject pause, GameObject gameOver, GameObject victory)
        {
            hudPanel = hud;
            shopPanel = shop;
            pausePanel = pause;
            gameOverPanel = gameOver;
            victoryPanel = victory;
            
            SetupPanelsForVR();
        }

        public void OnResumeClicked()  => GameManager.Instance?.SetState(GameState.Playing);
        public void OnRestartClicked() => GameManager.Instance?.RestartGame();
        public void OnMenuClicked()    => GameManager.Instance?.GoToMenu();
    }
}

