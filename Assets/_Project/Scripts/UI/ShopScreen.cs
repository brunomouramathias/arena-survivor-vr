using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ArenaSurvivor.Core;
using ArenaSurvivor.Shop;

namespace ArenaSurvivor.UI
{
    // Tela da loja entre ondas — construída por código, sem prefab.
    //
    // BUG QUE ELA CORRIGE: ao pegar o ícone de poder do fim da onda, o WaveManager chamava
    // GameState.Shop, que zera o Time.timeScale. O painel da loja era o "Shop_Panel" vazio do
    // Canvas_HUD legado, que está DESATIVADO na cena Arena — ou seja: nada aparecia na tela
    // e o jogo ficava congelado para sempre (timeScale 0 sem UI nenhuma).
    public class ShopScreen : MonoBehaviour
    {
        public static ShopScreen Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (SceneManager.GetActiveScene().name != "Arena") return;
            var go = new GameObject("ShopScreen");
            go.AddComponent<ShopScreen>();
        }

        private class Row
        {
            public Button          button;
            public Text            label;
            public System.Func<bool>   buy;      // executa a compra
            public System.Func<string> caption;  // texto do botão
            public System.Func<bool>   enabled;  // pode clicar?
        }

        private readonly List<Row> rows = new List<Row>();

        private Canvas   canvas;
        private GameObject root;
        private GameObject hudCanvas;
        private bool       hudHidden;
        private Text     titleText;
        private Text     pointsText;
        private Text     hintText;
        private bool     built;

        public bool IsVisible => root != null && root.activeInHierarchy;

        // ── ciclo de vida ────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Build();
            Hide();
        }

        private void OnEnable()  { GameManager.OnGameStateChanged += HandleState; }
        private void OnDisable() { GameManager.OnGameStateChanged -= HandleState; }

        private void Start()
        {
            HandleState(GameManager.Instance != null ? GameManager.Instance.CurrentState : GameState.Playing);
        }

        private void HandleState(GameState state)
        {
            if (state == GameState.Shop) Show();
            else Hide();
        }

        private void Show()
        {
            if (!built) Build();
            EnsureCamera();
            root.SetActive(true);
            Refresh();

            Cursor.visible   = true;
            Cursor.lockState = CursorLockMode.None;

            // o HUD de combate sai da frente da loja
            if (!hudHidden)
            {
                var hud = FindAnyObjectByType<GameHUD>();
                var hudT = hud != null ? hud.transform.Find("Canvas_GameHUD") : null;
                if (hudT != null && hudT.gameObject.activeSelf)
                {
                    hudCanvas = hudT.gameObject;
                    hudCanvas.SetActive(false);
                    hudHidden = true;
                }
            }
        }

        private void Hide()
        {
            if (root != null) root.SetActive(false);

            if (hudHidden && hudCanvas != null)
            {
                hudCanvas.SetActive(true);
                hudHidden = false;
            }
        }

        private void Update()
        {
            if (!IsVisible) return;
            EnsureCamera();

            // Sempre há como sair — teclado ou botão
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.N) ||
                Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        public void Close()
        {
            if (ShopManager.Instance != null) ShopManager.Instance.CloseShop();
            else GameManager.Instance?.SetState(GameState.Playing);
        }

        private void EnsureCamera()
        {
            if (canvas == null) return;
            if (canvas.worldCamera == null) canvas.worldCamera = Camera.main;
        }

        // ── conteúdo ─────────────────────────────────────────────────────────
        private void Refresh()
        {
            var shop = ShopManager.Instance;
            var score = ScoreService.Instance;
            var wm = Object.FindAnyObjectByType<Waves.WaveManager>();

            if (titleText != null)
                titleText.text = wm != null
                    ? $"ONDA {Mathf.Max(1, wm.CurrentWave)} CONCLUÍDA  —  LOJA"
                    : "LOJA";

            if (pointsText != null)
                pointsText.text = score != null ? $"PONTOS: {score.Currency}" : "PONTOS: 0";

            if (shop == null) return;

            foreach (var r in rows)
            {
                if (r.label != null) r.label.text = r.caption();
                if (r.button != null) r.button.interactable = r.enabled();
            }
        }

        private void Buy(Row row)
        {
            row.buy();
            Refresh();
        }

        // ── construção da UI ─────────────────────────────────────────────────
        private void Build()
        {
            if (built) return;
            built = true;

            var canvasGO = new GameObject("Canvas_Shop", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);

            canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 0.5f;      // na frente do HUD (planeDistance 1)
            canvas.sortingOrder  = 300;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // fundo escuro que cobre a tela
            root = new GameObject("Root", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvasGO.transform, false);
            var rrt = root.GetComponent<RectTransform>();
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = Vector2.one;
            rrt.offsetMin = rrt.offsetMax = Vector2.zero;
            var bg = root.GetComponent<Image>();
            bg.color = new Color(0.03f, 0.03f, 0.07f, 0.94f);
            bg.raycastTarget = true;

            // moldura central
            var card = MakePanel(root.transform, new Color(0.07f, 0.09f, 0.16f, 0.98f), new Vector2(980f, 860f), Vector2.zero);

            titleText = MakeText(card.transform, font, "LOJA", 52, new Color(1f, 0.88f, 0.55f), new Vector2(0f, 360f), new Vector2(920f, 70f));
            pointsText = MakeText(card.transform, font, "PONTOS: 0", 34, Color.white, new Vector2(0f, 300f), new Vector2(920f, 50f));

            var shop = FindAnyObjectByType<ShopManager>();

            // Linhas de compra (3 upgrades + 2 armas)
            float y = 220f;
            AddRow(card.transform, font, y,
                () => shop != null ? "DANO  +10%   ·   " + Level(shop, UpgradeType.Damage) : "DANO",
                () => shop != null && shop.TryBuyUpgrade(UpgradeType.Damage),
                () => CanBuyUpgrade(shop, UpgradeType.Damage));

            AddRow(card.transform, font, y -= 96f,
                () => shop != null ? "RECARGA  +15%   ·   " + Level(shop, UpgradeType.ReloadSpeed) : "RECARGA",
                () => shop != null && shop.TryBuyUpgrade(UpgradeType.ReloadSpeed),
                () => CanBuyUpgrade(shop, UpgradeType.ReloadSpeed));

            AddRow(card.transform, font, y -= 96f,
                () => shop != null ? "VIDA MÁXIMA  +20   ·   " + Level(shop, UpgradeType.MaxHealth) : "VIDA MÁXIMA",
                () => shop != null && shop.TryBuyUpgrade(UpgradeType.MaxHealth),
                () => CanBuyUpgrade(shop, UpgradeType.MaxHealth));

            AddRow(card.transform, font, y -= 96f,
                () => shop == null ? "ESCOPETA" : (shop.IsShotgunUnlocked
                        ? "ESCOPETA  —  desbloqueada"
                        : $"ESCOPETA  —  {shop.ShotgunCost} pts"),
                () => shop != null && shop.TryUnlockShotgun(),
                () => shop != null && !shop.IsShotgunUnlocked && CanAfford(shop.ShotgunCost));

            AddRow(card.transform, font, y -= 96f,
                () => shop == null ? "ARMA DE ENERGIA" : (shop.IsEnergyGunUnlocked
                        ? "ARMA DE ENERGIA  —  desbloqueada"
                        : $"ARMA DE ENERGIA  —  {shop.EnergyGunCost} pts"),
                () => shop != null && shop.TryUnlockEnergyGun(),
                () => shop != null && !shop.IsEnergyGunUnlocked && CanAfford(shop.EnergyGunCost));

            // Botão continuar
            var next = MakeButton(card.transform, font, "PRÓXIMA ONDA  ▶", new Vector2(0f, -330f),
                                  new Vector2(620f, 92f), new Color(0.85f, 0.45f, 0.12f));
            next.onClick.AddListener(Close);

            hintText = MakeText(card.transform, font, "ESPAÇO / ENTER continua   ·   ESC volta ao combate",
                                26, new Color(0.72f, 0.78f, 0.9f), new Vector2(0f, -400f), new Vector2(900f, 40f));
        }

        private void AddRow(Transform parent, Font font, float y, System.Func<string> caption,
                            System.Func<bool> buy, System.Func<bool> enabled)
        {
            var btn = MakeButton(parent, font, "", new Vector2(0f, y), new Vector2(860f, 76f),
                                 new Color(0.13f, 0.19f, 0.32f));
            var label = btn.GetComponentInChildren<Text>();

            var row = new Row { button = btn, label = label, buy = buy, caption = caption, enabled = enabled };
            btn.onClick.AddListener(() => Buy(row));
            rows.Add(row);
        }

        private static string Level(ShopManager shop, UpgradeType type)
        {
            int lv  = shop.GetUpgradeLevel(type);
            int max = shop.GetUpgradeMaxLevel(type);
            int cost = shop.GetUpgradeCost(type);
            string costTxt = lv >= max ? "máximo" : $"{cost} pts";
            return $"Lv {lv}/{max}  ·  {costTxt}";
        }

        private static bool CanBuyUpgrade(ShopManager shop, UpgradeType type)
        {
            if (shop == null) return false;
            if (shop.GetUpgradeLevel(type) >= shop.GetUpgradeMaxLevel(type)) return false;
            return CanAfford(shop.GetUpgradeCost(type));
        }

        private static bool CanAfford(int cost) => ScoreService.Instance != null && ScoreService.Instance.CanAfford(cost);

        // ── helpers de UI ────────────────────────────────────────────────────
        private static GameObject MakePanel(Transform parent, Color color, Vector2 size, Vector2 pos)
        {
            var go = new GameObject("Card", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static Button MakeButton(Transform parent, Font font, string content, Vector2 pos,
                                         Vector2 size, Color color)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            var img = go.GetComponent<Image>();
            img.color = color;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.normalColor      = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor     = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor    = new Color(0.45f, 0.45f, 0.5f, 0.6f);
            colors.colorMultiplier  = 1f;
            btn.colors = colors;

            var text = MakeText(go.transform, font, content, 32, Color.white, Vector2.zero, size * 0.95f);
            text.raycastTarget = false;
            return btn;
        }

        private static Text MakeText(Transform parent, Font font, string content, int size, Color color,
                                     Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;

            var t = go.GetComponent<Text>();
            t.font = font;
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow   = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
