using UnityEngine;
using UnityEngine.UI;
using ArenaSurvivor.Core;
using ArenaSurvivor.Player;

namespace ArenaSurvivor.UI
{
    // HUD de 3 corações + tela de Game Over com reinício (UI legada, sem depender de TMP).
    public class GameHUD : MonoBehaviour
    {
        private PlayerHealth player;
        private Image[] hearts = new Image[3];
        private GameObject gameOverPanel;
        private Text titleText;
        private Text scoreText;
        private Text scoreBreakdownText;
        private Text waveText;
        private Text hudScoreText;
        private Text hudInfoText;
        private Text towerToast;             // aviso pequeno quando uma torre cai
        private float towerToastTimer;
        private const float TowerToastTime = 3.2f;
        private static Sprite heartSprite;

        private void Start()
        {
            Build();
            player = Object.FindAnyObjectByType<PlayerHealth>();
            GameManager.OnGameStateChanged += OnStateChanged;

            // aviso de cada torre que cai
            foreach (var tower in ArenaSurvivor.World.TowerHealth.All)
                if (tower != null) tower.DestroyedEvent += OnTowerDestroyed;
            OnStateChanged(GameManager.Instance != null ? GameManager.Instance.CurrentState : GameState.Playing);
        }

        private void OnDestroy()
        {
            GameManager.OnGameStateChanged -= OnStateChanged;

            foreach (var tower in ArenaSurvivor.World.TowerHealth.All)
                if (tower != null) tower.DestroyedEvent -= OnTowerDestroyed;
        }

        // ── aviso de torre destruída ─────────────────────────────────────────
        private void OnTowerDestroyed(ArenaSurvivor.World.TowerHealth tower)
        {
            if (towerToast == null) return;

            int alive = 0, total = 0;
            foreach (var t in ArenaSurvivor.World.TowerHealth.All)
            {
                if (t == null) continue;
                total++;
                if (!t.Destroyed) alive++;
            }

            string where = TowerSide(tower);
            towerToast.text = alive > 0
                ? $"TORRE {where} DESTRUÍDA   ·   {alive}/{total} DE PÉ"
                : "ÚLTIMA TORRE DESTRUÍDA";
            towerToast.color = new Color(1f, 0.55f, 0.34f, 1f);
            towerToastTimer = TowerToastTime;
        }

        // nome pelo lado da arena (as quatro torres ficam nos cantos)
        private static string TowerSide(ArenaSurvivor.World.TowerHealth tower)
        {
            Vector3 center = Vector3.zero;
            int n = 0;
            foreach (var t in ArenaSurvivor.World.TowerHealth.All)
                if (t != null) { center += t.transform.position; n++; }
            if (n == 0) return "";
            center /= n;

            Vector3 d = tower.transform.position - center;
            float ax = Mathf.Abs(d.x), az = Mathf.Abs(d.z);
            if (ax < 0.5f && az < 0.5f) return "CENTRAL";

            bool leste = d.x > 0f, norte = d.z > 0f;
            if (ax > az * 1.6f) return leste ? "LESTE" : "OESTE";
            if (az > ax * 1.6f) return norte ? "NORTE" : "SUL";
            return norte ? (leste ? "NORDESTE" : "NOROESTE")
                         : (leste ? "SUDESTE"  : "SUDOESTE");
        }

        private void Update()
        {
            if (player == null) player = Object.FindAnyObjectByType<PlayerHealth>();
            UpdateHearts();

            if (waveText != null)
            {
                var wm = Object.FindAnyObjectByType<ArenaSurvivor.Waves.WaveManager>();
                waveText.text = wm != null ? $"ONDA {Mathf.Max(1, wm.CurrentWave)}" : "ONDA 1";
            }
            if (hudScoreText != null && ScoreService.Instance != null)
                hudScoreText.text = $"PONTOS: {ScoreService.Instance.CurrentScore}";

            // desvanece o aviso de torre
            if (towerToast != null)
            {
                if (towerToastTimer > 0f)
                {
                    towerToastTimer -= Time.unscaledDeltaTime;
                    var c = towerToast.color;
                    c.a = Mathf.Clamp01(towerToastTimer / 0.7f);
                    towerToast.color = c;
                }
                else if (towerToast.color.a > 0f)
                {
                    var c = towerToast.color;
                    c.a = 0f;
                    towerToast.color = c;
                }
            }

            if (hudInfoText != null)
            {
                float secs = ScoreService.Instance != null ? ScoreService.Instance.PlaySeconds : 0f;
                int mins = Mathf.FloorToInt(secs / 60f);
                int rest = Mathf.FloorToInt(secs % 60f);

                int alive = 0, total = 0;
                float hp = 0f;
                foreach (var t in ArenaSurvivor.World.TowerHealth.All)
                {
                    if (t == null) continue;
                    total++;
                    if (!t.Destroyed) { alive++; hp += t.Normalized; }
                }
                // media da vida das torres vivas (0-100%)
                int pct = alive > 0 ? Mathf.RoundToInt(hp / alive * 100f) : 0;
                hudInfoText.text = $"TEMPO {mins:00}:{rest:00}    TORRES {alive}/{total} ({pct}%)    {Difficulty.Profile.Label}";
            }

            var gm = GameManager.Instance;
            // Vitória também precisa de saída: senão o timeScale fica em 0 e o jogo trava.
            if (gm != null && (gm.CurrentState == GameState.GameOver || gm.CurrentState == GameState.Victory))
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
                    gm.RestartGame();
            }
        }

        private void OnStateChanged(GameState state)
        {
            if (gameOverPanel == null) return;
            bool victory = state == GameState.Victory;
            bool over = state == GameState.GameOver || victory;
            gameOverPanel.SetActive(over);

            if (titleText != null)
            {
                titleText.text  = victory ? "VITÓRIA!" : "GAME OVER";
                titleText.color = victory ? new Color(1f, 0.85f, 0.3f) : new Color(0.95f, 0.2f, 0.22f);
            }

            if (over && scoreText != null)
            {
                int score = ScoreService.Instance != null ? ScoreService.Instance.CurrentScore : 0;
                scoreText.text = $"PONTUAÇÃO: {score}";

                if (scoreBreakdownText != null && ScoreService.Instance != null)
                {
                    var s = ScoreService.Instance;
                    scoreBreakdownText.text =
                        $"Sobreviveu {FormatDuration(s.PlaySeconds)} (+{s.TimeBonus})   " +
                        $"Torres de pé (+{s.TowerBonus})   Vida das torres (+{s.TowerHealthBonus})";
                }
            }
        }

        // Tempo de sobrevivência legível: até 59 s mostra segundos, acima disso mostra minutos
        // ("48 s", "3 min 47 s", "1 min"). Sempre com dois dígitos quando tem minutos e segundos.
        public static string FormatDuration(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));

            if (total < 60) return $"{total} s";

            int minutes = total / 60;
            int rest    = total % 60;

            if (rest == 0) return $"{minutes} min";
            return $"{minutes} min {rest:00} s";
        }

        private void UpdateHearts()
        {
            if (player == null) return;
            int hp = Mathf.Clamp(player.CurrentHealth, 0, hearts.Length);
            for (int i = 0; i < hearts.Length; i++)
            {
                if (hearts[i] == null) continue;
                hearts[i].color = i < hp ? new Color(0.95f, 0.18f, 0.22f) : new Color(0.22f, 0.22f, 0.26f);
            }
        }

        // ------------------------------------------------------------------
        private void Build()
        {
            var canvasGO = new GameObject("Canvas_GameHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            // ScreenSpaceCamera: renderiza na câmera do jogo (aparece no jogo e em capturas)
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 100;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // ----- corações (canto superior direito) -----
            var heartsRoot = new GameObject("Hearts", typeof(RectTransform));
            heartsRoot.transform.SetParent(canvasGO.transform, false);
            var hrt = heartsRoot.GetComponent<RectTransform>();
            hrt.anchorMin = hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot = new Vector2(1f, 1f);
            hrt.anchoredPosition = new Vector2(-28f, -22f);
            hrt.sizeDelta = new Vector2(240f, 80f);

            var sprite = HeartSprite();
            for (int i = 0; i < 3; i++)
            {
                var imgGO = new GameObject("Heart" + (i + 1), typeof(RectTransform), typeof(Image));
                imgGO.transform.SetParent(heartsRoot.transform, false);
                var rt = imgGO.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.sizeDelta = new Vector2(64f, 64f);
                rt.anchoredPosition = new Vector2(-(2 - i) * 74f, 0f);

                var img = imgGO.GetComponent<Image>();
                img.sprite = sprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.color = new Color(0.95f, 0.18f, 0.22f);
                hearts[i] = img;
            }

            // ----- onda (topo centro) e pontos (topo esquerdo) -----
            waveText = MakeText(canvasGO.transform, font, "ONDA 1", 44, new Color(1f, 0.95f, 0.85f), Vector2.zero, new Vector2(600f, 70f));
            var wrt = waveText.rectTransform;
            wrt.anchorMin = wrt.anchorMax = new Vector2(0.5f, 1f);
            wrt.pivot = new Vector2(0.5f, 1f);
            wrt.anchoredPosition = new Vector2(0f, -18f);

            hudScoreText = MakeText(canvasGO.transform, font, "PONTOS: 0", 34, Color.white, Vector2.zero, new Vector2(500f, 60f));
            var srt = hudScoreText.rectTransform;
            srt.anchorMin = srt.anchorMax = new Vector2(0f, 1f);
            srt.pivot = new Vector2(0f, 1f);
            srt.anchoredPosition = new Vector2(24f, -18f);
            hudScoreText.alignment = TextAnchor.MiddleLeft;

            hudInfoText = MakeText(canvasGO.transform, font, "TEMPO 00:00    TORRES 4/4 (100%)", 26,
                                   new Color(0.8f, 0.88f, 1f), Vector2.zero, new Vector2(900f, 50f));
            var irt = hudInfoText.rectTransform;
            irt.anchorMin = irt.anchorMax = new Vector2(0f, 1f);
            irt.pivot = new Vector2(0f, 1f);
            irt.anchoredPosition = new Vector2(24f, -74f);
            hudInfoText.alignment = TextAnchor.MiddleLeft;

            // aviso curto de torre caindo (aparece embaixo do número da onda)
            towerToast = MakeText(canvasGO.transform, font, "", 26, new Color(1f, 0.55f, 0.34f, 0f),
                                  Vector2.zero, new Vector2(1100f, 34f));
            var trt = towerToast.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -134f);
            towerToast.alignment = TextAnchor.MiddleCenter;
            towerToast.gameObject.name = "TowerToast";

            // contorno escuro: o aviso aparece sobre torre clara, chão escuro e fogo
            var outline = towerToast.gameObject.AddComponent<Outline>();
            outline.effectColor    = new Color(0.06f, 0.03f, 0.05f, 0.95f);
            outline.effectDistance = new Vector2(1.6f, -1.6f);
            outline.useGraphicAlpha = false;

            // ----- tela de game over -----
            gameOverPanel = new GameObject("GameOverPanel", typeof(RectTransform));
            gameOverPanel.transform.SetParent(canvasGO.transform, false);
            var gort = gameOverPanel.GetComponent<RectTransform>();
            gort.anchorMin = Vector2.zero;
            gort.anchorMax = Vector2.one;
            gort.offsetMin = gort.offsetMax = Vector2.zero;

            var bg = new GameObject("BG", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(gameOverPanel.transform, false);
            var bgrt = bg.GetComponent<RectTransform>();
            bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one;
            bgrt.offsetMin = bgrt.offsetMax = Vector2.zero;
            var bgImg = bg.GetComponent<Image>();
            bgImg.color = new Color(0.03f, 0.02f, 0.05f, 0.86f);
            bgImg.raycastTarget = true;

            titleText = MakeText(gameOverPanel.transform, font, "GAME OVER", 96, new Color(0.95f, 0.2f, 0.22f), new Vector2(0f, 150f), new Vector2(1200f, 160f));
            scoreText = MakeText(gameOverPanel.transform, font, "PONTUAÇÃO: 0", 44, Color.white, new Vector2(0f, 20f), new Vector2(900f, 80f));
            scoreBreakdownText = MakeText(gameOverPanel.transform, font, "", 30, new Color(0.8f, 0.88f, 1f), new Vector2(0f, -55f), new Vector2(1400f, 60f));
            MakeText(gameOverPanel.transform, font, "Pressione ENTER (ou clique) para reiniciar", 34, new Color(0.85f, 0.85f, 0.9f), new Vector2(0f, -120f), new Vector2(1200f, 70f));

            // Botão reiniciar (mouse)
            var btnGO = new GameObject("RestartButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(gameOverPanel.transform, false);
            var brt = btnGO.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(360f, 84f);
            brt.anchoredPosition = new Vector2(0f, -240f);
            var bimg = btnGO.GetComponent<Image>();
            bimg.color = new Color(0.75f, 0.2f, 0.2f, 1f);
            var btn = btnGO.GetComponent<Button>();
            btn.onClick.AddListener(() => GameManager.Instance?.RestartGame());

            var btnLabel = MakeText(btnGO.transform, font, "REINICIAR", 36, Color.white, Vector2.zero, new Vector2(360f, 84f));
            btnLabel.raycastTarget = false;

            gameOverPanel.SetActive(false);
        }

        private static Text MakeText(Transform parent, Font font, string content, int size, Color color, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;

            var t = go.GetComponent<Text>();
            t.font = font;
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static Sprite HeartSprite()
        {
            if (heartSprite != null) return heartSprite;

            int size = 96;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (float)(size - 1) * 2f - 1f) * 1.35f;
                    float ny = (y / (float)(size - 1) * 2f - 1f) * 1.35f;
                    float a = nx * nx + ny * ny - 1f;
                    float f = a * a * a - nx * nx * ny * ny * ny; // curva do coração
                    float alpha = Mathf.Clamp01(-f * 18f);
                    px[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            heartSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return heartSprite;
        }
    }
}
