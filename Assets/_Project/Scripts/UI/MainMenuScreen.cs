using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ArenaSurvivor.Core;

namespace ArenaSurvivor.UI
{
    // Tela de abertura: arte da arena, seleção de modo (Fácil / Médio / Difícil), iniciar partida,
    // "Como jogar" (controles + estratégia) e sair. Construída por código, sem prefab.
    public class MainMenuScreen : MonoBehaviour
    {
        // paleta (mesma família de cores do HUD e da loja)
        static readonly Color Gold      = new Color(0.95f, 0.74f, 0.33f);
        static readonly Color GoldSoft  = new Color(0.80f, 0.60f, 0.27f);
        static readonly Color Ink       = new Color(0.05f, 0.06f, 0.10f);
        static readonly Color Card      = new Color(0.05f, 0.06f, 0.105f, 0.975f);
        static readonly Color Line      = new Color(1f, 0.85f, 0.55f, 0.22f);
        static readonly Color TextMain  = new Color(0.96f, 0.95f, 0.92f);
        static readonly Color TextMuted = new Color(0.80f, 0.83f, 0.88f);
        static readonly Color Chip      = new Color(1f, 1f, 1f, 0.10f);

        private const float PanelW = 620f;

        // backdrop
        private RectTransform artRect;
        private float artTime;

        // painel de modo
        private readonly List<Button> difficultyButtons = new List<Button>();
        private readonly List<TextMeshProUGUI> difficultyLabels = new List<TextMeshProUGUI>();
        private readonly List<Image> difficultyFills = new List<Image>();
        private TextMeshProUGUI modeHeadline;
        private TextMeshProUGUI modeDescription;
        private readonly List<TextMeshProUGUI> statValues = new List<TextMeshProUGUI>();
        private TextMeshProUGUI recordText;

        // modal "como jogar"
        private GameObject howToPanel;
        private Button startButton;
        private TextMeshProUGUI hintText;

        private Sprite roundedPanelSprite;
        private Sprite roundedButtonSprite;
        private Sprite outlinePanelSprite;
        private static Material boldMaterial;
        private Sprite outlineButtonSprite;
        private Sprite gradientLeftSprite;
        private Sprite gradientBottomSprite;
        private Sprite gradientTopSprite;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            Cursor.visible   = true;
            Cursor.lockState = CursorLockMode.None;

            // UI desenhada "crua": bloom/tonemap/vinheta e antialiasing de post processamento
            // passam por cima do canvas e deixam o texto embaçado. Aqui o menu é texto nítido.
            SharpUICamera();

            Difficulty.LoadPreference();
            BuildSprites();
            BuildBackdrop();
            BuildTopBar();
            BuildHero();
            BuildModePanel();
            BuildFooter();
            BuildHowToPanel();

            RefreshMode();
        }

        // O canvas é ScreenSpaceCamera: sem isso o texto passa pelo pós-processamento da câmera
        private static void SharpUICamera()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var data = cam.GetUniversalAdditionalCameraData();
            if (data == null) return;

            data.renderPostProcessing = false;                   // sem bloom/tonemap/vinheta no texto
            data.antialiasing          = AntialiasingMode.None;   // FXAA/SMAA borram letras pequenas
            data.dithering             = false;
        }

        private void Update()
        {
            // Ken Burns bem discreto na arte
            if (artRect != null)
            {
                artTime += Time.unscaledDeltaTime;
                float t = Mathf.PingPong(artTime / 26f, 1f);
                float zoom = Mathf.Lerp(1.02f, 1.07f, t);
                artRect.localScale = new Vector3(zoom, zoom, 1f);
            }

            bool howToOpen = howToPanel != null && howToPanel.activeSelf;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (howToOpen) CloseHowTo();
                return;
            }

            if (howToOpen)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
                    CloseHowTo();
                return;
            }

            // teclado: setas trocam o modo, Enter inicia
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                SelectDifficulty(Difficulty.Index - 1);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                SelectDifficulty(Difficulty.Index + 1);
            else if (Input.GetKeyDown(KeyCode.Alpha1)) SelectDifficulty(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) SelectDifficulty(1);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) SelectDifficulty(2);
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                StartMatch();
        }

        // ── ações ────────────────────────────────────────────────────────────
        public void SelectDifficulty(int index)
        {
            var next = Difficulty.AtIndex(index);
            if (next == Difficulty.Current) return;
            Difficulty.Select(next);
            RefreshMode();
        }

        public void StartMatch()
        {
            Difficulty.Select(Difficulty.Current);   // grava a escolha
            PlayerPrefs.Save();
            GameManager.Instance?.StartGame();
        }

        public void OpenHowTo()  { if (howToPanel != null) howToPanel.SetActive(true); }
        public void CloseHowTo() { if (howToPanel != null) howToPanel.SetActive(false); }
        public void QuitGame()   { GameManager.Instance?.QuitGame(); }

        // ── estado da UI ─────────────────────────────────────────────────────
        private void RefreshMode()
        {
            var profile = Difficulty.Profile;

            for (int i = 0; i < difficultyButtons.Count; i++)
            {
                bool selected = i == Difficulty.Index;

                difficultyFills[i].color  = selected ? Gold : new Color(1f, 1f, 1f, 0.06f);
                difficultyLabels[i].color = selected ? Ink : TextMuted;
                difficultyLabels[i].fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
                difficultyButtons[i].transform.localScale = selected ? Vector3.one : new Vector3(0.98f, 0.98f, 1f);
            }

            if (modeHeadline != null)    modeHeadline.text    = profile.Headline;
            if (modeDescription != null) modeDescription.text = profile.Description;

            string[] values =
            {
                profile.HealthText,
                profile.SpeedText,
                profile.CountText,
                profile.StartPoints > 0 ? profile.StartPoints.ToString() : "0",
                profile.ScoreText
            };
            for (int i = 0; i < statValues.Count && i < values.Length; i++)
            {
                statValues[i].text  = values[i];
                statValues[i].color = values[i] == "padrão" ? TextMuted : Gold;
            }

            if (recordText != null)
            {
                int record = PlayerPrefs.GetInt("HighScore", 0);
                recordText.text = record > 0 ? $"RECORDE PESSOAL  {record}" : "SEM RECORDE AINDA — A ARENA ESTÁ ESPERANDO";
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  CONSTRUÇÃO
        // ─────────────────────────────────────────────────────────────────────

        private void BuildSprites()
        {
            roundedPanelSprite  = RoundedSprite(96, 26, new Vector4(34, 34, 34, 34));
            roundedButtonSprite = RoundedSprite(64, 14, new Vector4(20, 20, 20, 20));
            outlinePanelSprite  = RoundedOutlineSprite(96, 26, 1.6f, new Vector4(34, 34, 34, 34));
            outlineButtonSprite = RoundedOutlineSprite(64, 14, 1.4f, new Vector4(20, 20, 20, 20));

            // negativo "de verdade": dilatação do SDF (mesmo traço em 14px e em 116px)
            var baseMat = TMP_Settings.defaultFontAsset != null ? TMP_Settings.defaultFontAsset.material : null;
            if (baseMat != null)
            {
                boldMaterial = new Material(baseMat) { name = "MenuFont Bold" };
                boldMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0.11f);
                boldMaterial.SetFloat(ShaderUtilities.ID_WeightBold, 0f);
            }
            gradientLeftSprite   = GradientSprite(GradientDirection.LeftToRight);
            gradientBottomSprite = GradientSprite(GradientDirection.BottomToTop);
            gradientTopSprite    = GradientSprite(GradientDirection.BottomToTop);
        }

        private void BuildBackdrop()
        {
            var art = MakeImage("Backdrop", transform, null, Color.white);
            artRect = art.rectTransform;
            artRect.anchorMin = Vector2.zero;
            artRect.anchorMax = Vector2.one;
            artRect.offsetMin = artRect.offsetMax = Vector2.zero;

            var sprite = Resources.Load<Sprite>("Menu_Backdrop");
            if (sprite != null)
            {
                art.sprite = sprite;
                art.preserveAspect = false;
            }
            else
            {
                // sem arte importada: degradê escuro para a tela nunca ficar vazia
                art.color = new Color(0.05f, 0.06f, 0.10f, 1f);
            }
            art.raycastTarget = false;

            // escurece o lado do texto (esquerda) e as bordas
            var left = MakeImage("ScrimLeft", transform, gradientLeftSprite, new Color(0.02f, 0.02f, 0.05f, 0.96f));
            var lrt = left.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(0.72f, 1f);
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            left.raycastTarget = false;

            var bottom = MakeImage("ScrimBottom", transform, gradientBottomSprite, new Color(0.02f, 0.02f, 0.04f, 0.90f));
            var brt = bottom.rectTransform;
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 0.34f);
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            bottom.raycastTarget = false;

            var top = MakeImage("ScrimTop", transform, gradientTopSprite, new Color(0.01f, 0.01f, 0.03f, 0.75f));
            var trt = top.rectTransform;
            trt.localRotation = Quaternion.Euler(0f, 0f, 180f);   // escuro colado no topo da tela
            trt.anchorMin = new Vector2(0f, 0.82f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            top.raycastTarget = false;
        }

        private void BuildTopBar()
        {
            // filete superior
            var line = MakeImage("TopRule", transform, null, new Color(1f, 1f, 1f, 0.10f));
            var lrt = line.rectTransform;
            lrt.anchorMin = new Vector2(0f, 1f);
            lrt.anchorMax = new Vector2(1f, 1f);
            lrt.pivot     = new Vector2(0.5f, 1f);
            lrt.anchoredPosition = new Vector2(0f, -76f);
            lrt.sizeDelta = new Vector2(0f, 1f);
            line.raycastTarget = false;

            // marca
            var mark = MakeImage("Mark", transform, roundedButtonSprite, Gold);
            var mrt = mark.rectTransform;
            mrt.anchorMin = mrt.anchorMax = new Vector2(0f, 1f);
            mrt.pivot = new Vector2(0f, 1f);
            mrt.anchoredPosition = new Vector2(96f, -26f);
            mrt.sizeDelta = new Vector2(24f, 24f);
            mark.raycastTarget = false;

            var brand = MakeText("Brand", transform, "ARENA SURVIVOR", 21f, TextMain, FontStyles.Bold,
                                 new Vector2(0f, 1f), new Vector2(136f, -24f), new Vector2(360f, 30f),
                                 TextAlignmentOptions.Left);
            brand.characterSpacing = 8f;

            var version = MakeText("Version", transform,
                                   "UTFPR  ·  REALIDADE VIRTUAL  ·  v1.0", 15f,
                                   new Color(0.79f, 0.82f, 0.88f), FontStyles.Normal,
                                   new Vector2(1f, 1f), new Vector2(-96f, -24f), new Vector2(700f, 26f),
                                   TextAlignmentOptions.Right);
            version.characterSpacing = 5f;
        }

        private void BuildHero()
        {
            var eyebrow = MakeText("Eyebrow", transform, "DEFENDA AS TORRES  ·  SOBREVIVA ÀS ONDAS", 19f, Gold, FontStyles.Bold,
                                   new Vector2(0f, 1f), new Vector2(96f, -208f), new Vector2(720f, 26f),
                                   TextAlignmentOptions.Left);
            eyebrow.characterSpacing = 10f;

            var title1 = MakeText("TitleLine1", transform, "ARENA", 116f, TextMain, FontStyles.Bold,
                                  new Vector2(0f, 1f), new Vector2(92f, -246f), new Vector2(760f, 130f),
                                  TextAlignmentOptions.Left);
            title1.characterSpacing = 2f;

            var title2 = MakeText("TitleLine2", transform, "SURVIVOR", 116f, TextMain, FontStyles.Bold,
                                  new Vector2(0f, 1f), new Vector2(92f, -364f), new Vector2(760f, 130f),
                                  TextAlignmentOptions.Left);
            title2.characterSpacing = 2f;

            var rule = MakeImage("HeroRule", transform, null, Gold);
            var rrt = rule.rectTransform;
            rrt.anchorMin = rrt.anchorMax = new Vector2(0f, 1f);
            rrt.pivot = new Vector2(0f, 1f);
            rrt.anchoredPosition = new Vector2(96f, -510f);
            rrt.sizeDelta = new Vector2(168f, 4f);
            rule.raycastTarget = false;

            var paragraph = MakeText("HeroText", transform,
                "Quatro torres, ondas sem fim e um bruxo que não perdoa.\n" +
                "Escolha o modo e segure a arena o quanto puder.",
                22f, new Color(0.86f, 0.88f, 0.92f), FontStyles.Normal,
                new Vector2(0f, 1f), new Vector2(96f, -548f), new Vector2(620f, 76f),
                TextAlignmentOptions.TopLeft, wrap: true);
            paragraph.lineSpacing = 10f;

            recordText = MakeText("Record", transform, "", 17f, Gold, FontStyles.Bold,
                                  new Vector2(0f, 1f), new Vector2(96f, -654f), new Vector2(620f, 26f),
                                  TextAlignmentOptions.Left);
            recordText.characterSpacing = 7f;
        }

        private void BuildModePanel()
        {
            var panel = MakeImage("ModePanel", transform, roundedPanelSprite, Card);
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(1f, 0.5f);
            prt.pivot = new Vector2(1f, 0.5f);
            prt.anchoredPosition = new Vector2(-96f, -6f);
            prt.sizeDelta = new Vector2(PanelW, 760f);

            var border = MakeImage("PanelBorder", panel.transform, outlinePanelSprite, new Color(1f, 0.85f, 0.55f, 0.30f));
            var brt2 = border.rectTransform;
            brt2.anchorMin = Vector2.zero;
            brt2.anchorMax = Vector2.one;
            brt2.offsetMin = new Vector2(-1f, -1f);
            brt2.offsetMax = new Vector2(1f, 1f);
            border.raycastTarget = false;
            border.transform.SetAsFirstSibling();

            float pad  = 36f;
            float y    = -34f;
            float innerW = PanelW - pad * 2f;

            // título do painel
            var label = MakeText("PanelLabel", panel.transform, "MODO DE JOGO", 17f, Gold, FontStyles.Bold,
                                 new Vector2(0f, 1f), new Vector2(pad, y), new Vector2(innerW, 22f),
                                 TextAlignmentOptions.Left);
            label.characterSpacing = 12f;
            y -= 34f;

            // seletor segmentado
            float gap = 8f;
            float btnW = (innerW - gap * 2f) / 3f;
            for (int i = 0; i < Difficulty.Count; i++)
            {
                int index = i;
                var mode = Difficulty.AtIndex(i);
                var btn = MakeButton($"Diff_{mode}", panel.transform, roundedButtonSprite,
                                     new Color(1f, 1f, 1f, 0.06f),
                                     new Vector2(pad + i * (btnW + gap), y),
                                     new Vector2(btnW, 58f),
                                     Difficulty.Get(mode).Label, 19f, TextMuted, FontStyles.Bold,
                                     TextAlignmentOptions.Center, out var fill, out var text);
                btn.onClick.AddListener(() => SelectDifficulty(index));

                difficultyButtons.Add(btn);
                difficultyFills.Add(fill);
                difficultyLabels.Add(text);
            }
            y -= 80f;

            modeHeadline = MakeText("ModeHeadline", panel.transform, "", 22f, TextMain, FontStyles.Bold,
                                    new Vector2(0f, 1f), new Vector2(pad, y), new Vector2(innerW, 30f),
                                    TextAlignmentOptions.Left);
            y -= 34f;

            modeDescription = MakeText("ModeDescription", panel.transform, "", 17f, TextMuted, FontStyles.Normal,
                                       new Vector2(0f, 1f), new Vector2(pad, y), new Vector2(innerW, 66f),
                                       TextAlignmentOptions.TopLeft, wrap: true);
            modeDescription.lineSpacing = 6f;
            y -= 82f;

            // ficha técnica do modo
            string[] statLabels = { "VIDA DOS INIMIGOS", "VELOCIDADE", "INIMIGOS POR ONDA", "PONTOS INICIAIS", "PONTUAÇÃO" };
            foreach (var statLabel in statLabels)
            {
                var l = MakeText($"Stat_{statLabel}", panel.transform, statLabel, 16f, new Color(0.76f, 0.79f, 0.85f),
                                 FontStyles.Normal, new Vector2(0f, 1f), new Vector2(pad, y),
                                 new Vector2(innerW - 130f, 26f), TextAlignmentOptions.Left);
                l.characterSpacing = 6f;

                var v = MakeText($"Value_{statLabel}", panel.transform, "", 17f, Gold, FontStyles.Bold,
                                 new Vector2(0f, 1f), new Vector2(pad + innerW - 200f, y),
                                 new Vector2(200f, 26f), TextAlignmentOptions.Right);
                statValues.Add(v);

                var sep = MakeImage($"StatSep_{statLabel}", panel.transform, null, new Color(1f, 1f, 1f, 0.06f));
                var srt = sep.rectTransform;
                srt.anchorMin = srt.anchorMax = new Vector2(0f, 1f);
                srt.pivot = new Vector2(0f, 1f);
                srt.anchoredPosition = new Vector2(pad, y - 26f);
                srt.sizeDelta = new Vector2(innerW, 1f);
                sep.raycastTarget = false;

                y -= 38f;
            }

            y -= 10f;

            // botão principal
            startButton = MakeButton("StartMatch", panel.transform, roundedButtonSprite, Gold,
                                     new Vector2(pad, y), new Vector2(innerW, 76f),
                                     "INICIAR PARTIDA  »", 24f, Ink, FontStyles.Bold,
                                     TextAlignmentOptions.Center, out _, out _);
            startButton.onClick.AddListener(StartMatch);
            y -= 92f;

            // botões secundários
            float halfW = (innerW - 12f) / 2f;
            var howTo = MakeGhostButton("HowTo", panel.transform, "COMO JOGAR", new Vector2(pad, y), new Vector2(halfW, 54f));
            howTo.onClick.AddListener(OpenHowTo);

            var quit = MakeGhostButton("Quit", panel.transform, "SAIR", new Vector2(pad + halfW + 12f, y), new Vector2(halfW, 54f));
            quit.onClick.AddListener(QuitGame);
            y -= 74f;

            hintText = MakeText("Hint", panel.transform, "SETAS  trocam o modo     ·     ENTER  inicia a partida", 15f,
                                new Color(0.72f, 0.75f, 0.82f), FontStyles.Normal,
                                new Vector2(0f, 1f), new Vector2(pad, y), new Vector2(innerW, 24f),
                                TextAlignmentOptions.Center);
            hintText.characterSpacing = 4f;
        }

        private void BuildFooter()
        {
            var footer = MakeText("Footer", transform,
                "WASD mover     ·     MOUSE mirar     ·     CLIQUE atirar     ·     R recarregar     ·     SHIFT / ESPAÇO esquiva     ·     1 2 3 armas",
                16f, new Color(0.79f, 0.82f, 0.88f), FontStyles.Normal,
                new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(1500f, 26f),
                TextAlignmentOptions.Center);
            footer.characterSpacing = 5f;
        }

        // ── modal "COMO JOGAR" ───────────────────────────────────────────────
        private void BuildHowToPanel()
        {
            howToPanel = new GameObject("HowToPanel", typeof(RectTransform));
            howToPanel.transform.SetParent(transform, false);
            var prt = howToPanel.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = prt.offsetMax = Vector2.zero;

            var dim = MakeImage("Dim", howToPanel.transform, null, new Color(0.01f, 0.01f, 0.03f, 0.80f));
            var drt = dim.rectTransform;
            drt.anchorMin = Vector2.zero;
            drt.anchorMax = Vector2.one;
            drt.offsetMin = drt.offsetMax = Vector2.zero;

            var card = MakeImage("Card", howToPanel.transform, roundedPanelSprite, new Color(0.055f, 0.065f, 0.115f, 0.985f));
            var crt = card.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(1330f, 860f);
            crt.anchoredPosition = Vector2.zero;

            var border = MakeImage("CardBorder", card.transform, outlinePanelSprite, new Color(1f, 0.85f, 0.55f, 0.30f));
            var brt = border.rectTransform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(-1f, -1f);
            brt.offsetMax = new Vector2(1f, 1f);
            border.raycastTarget = false;

            var title = MakeText("HowToTitle", card.transform, "COMO JOGAR", 34f, TextMain, FontStyles.Bold,
                                 new Vector2(0f, 1f), new Vector2(44f, -34f), new Vector2(600f, 42f),
                                 TextAlignmentOptions.Left);
            title.characterSpacing = 8f;

            var subtitle = MakeText("HowToSubtitle", card.transform,
                                    "Tudo o que você precisa apertar — e a ordem que costuma dar certo.",
                                    17f, TextMuted, FontStyles.Normal,
                                    new Vector2(0f, 1f), new Vector2(44f, -78f), new Vector2(900f, 26f),
                                    TextAlignmentOptions.Left);

            var close = MakeGhostButton("CloseX", card.transform, "FECHAR  (ESC)",
                                        new Vector2(0f, 1f), new Vector2(190f, 46f));
            var closeRt = (RectTransform)close.transform;
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-44f, -34f);
            close.onClick.AddListener(CloseHowTo);

            // coluna 1 — controles
            float colY = -140f;
            var controlsTitle = MakeText("ControlsTitle", card.transform, "CONTROLES", 16f, Gold, FontStyles.Bold,
                                         new Vector2(0f, 1f), new Vector2(44f, colY), new Vector2(560f, 22f),
                                         TextAlignmentOptions.Left);
            controlsTitle.characterSpacing = 12f;

            var controls = new (string keys, string action)[]
            {
                ("W A S D",          "Mover o personagem"),
                ("MOUSE",            "Mirar (a arma segue o cursor)"),
                ("CLIQUE ESQ.",      "Atirar"),
                ("R",                "Recarregar"),
                ("SHIFT / ESPAÇO",   "Esquiva rápida"),
                ("1   2   3",        "Trocar de arma (ou roda do mouse)"),
                ("CLIQUE",           "Comprar na loja entre ondas"),
                ("ESPAÇO / ENTER",   "Fechar a loja e começar a próxima onda"),
                ("ESC",              "Sair da loja / fechar janelas"),
            };

            float rowY = colY - 44f;
            foreach (var (keys, action) in controls)
            {
                var chipBg = MakeImage($"Chip_{action}", card.transform, roundedButtonSprite, new Color(1f, 1f, 1f, 0.13f));
                var cbrt = chipBg.rectTransform;
                cbrt.anchorMin = cbrt.anchorMax = new Vector2(0f, 1f);
                cbrt.pivot = new Vector2(0f, 1f);
                cbrt.anchoredPosition = new Vector2(44f, rowY);
                cbrt.sizeDelta = new Vector2(196f, 34f);

                var chipText = MakeText($"ChipText_{action}", chipBg.transform, keys, 14f, new Color(0.95f, 0.96f, 0.98f), FontStyles.Bold,
                                        new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190f, 34f),
                                        TextAlignmentOptions.Center);
                chipText.characterSpacing = 4f;

                MakeText($"Action_{action}", card.transform, action, 17f,
                         new Color(0.84f, 0.86f, 0.90f), FontStyles.Normal,
                         new Vector2(0f, 1f), new Vector2(258f, rowY + 4f), new Vector2(380f, 28f),
                         TextAlignmentOptions.Left);

                rowY -= 50f;
            }

            // coluna 2 — estratégia
            float stratX = 690f;
            var stratTitle = MakeText("StrategyTitle", card.transform, "ESTRATÉGIA", 16f, Gold, FontStyles.Bold,
                                      new Vector2(0f, 1f), new Vector2(stratX, colY), new Vector2(580f, 22f),
                                      TextAlignmentOptions.Left);
            stratTitle.characterSpacing = 12f;

            var tips = new[]
            {
                "Fique entre a horda e as torres: elas são o objetivo, não o cenário. Torre no chão é menos vida para você.",
                "Cada onda limpa solta um ícone de poder no chão — pegue antes de entrar na loja, ele evolui a arma.",
                "Na loja, DANO é o primeiro upgrade que compensa; escopeta e arma de energia valem a partir da 2ª onda.",
                "O tiro sai reto da mira: acerte o peito e ande enquanto atira — parado você vira alvo fácil.",
                "A esquiva tem recarga — use para sair do cerco, não para atravessar o mapa.",
                "Sem munição a arma troca sozinha para a pistola (reserva infinita). Recarregue atrás das torres.",
            };

            float tipY = colY - 44f;
            for (int i = 0; i < tips.Length; i++)
            {
                var numberBg = MakeImage($"TipNum_{i}", card.transform, roundedButtonSprite, Gold);
                var nrt = numberBg.rectTransform;
                nrt.anchorMin = nrt.anchorMax = new Vector2(0f, 1f);
                nrt.pivot = new Vector2(0f, 1f);
                nrt.anchoredPosition = new Vector2(stratX, tipY);
                nrt.sizeDelta = new Vector2(34f, 34f);

                MakeText($"TipNumText_{i}", numberBg.transform, (i + 1).ToString(), 16f, Ink, FontStyles.Bold,
                         new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f), TextAlignmentOptions.Center);

                var tip = MakeText($"Tip_{i}", card.transform, tips[i], 17f,
                                   new Color(0.84f, 0.86f, 0.90f), FontStyles.Normal,
                                   new Vector2(0f, 1f), new Vector2(stratX + 46f, tipY + 2f), new Vector2(540f, 66f),
                                   TextAlignmentOptions.TopLeft, wrap: true);
                tip.lineSpacing = 4f;

                tipY -= 74f;
            }

            // ciclo do jogo (rodapé do modal)
            var cycleTitle = MakeText("CycleTitle", card.transform, "O CICLO DE UMA ONDA", 15f, new Color(0.76f, 0.79f, 0.85f), FontStyles.Bold,
                                      new Vector2(0f, 0f), new Vector2(44f, 190f), new Vector2(600f, 22f),
                                      TextAlignmentOptions.Left);
            cycleTitle.characterSpacing = 10f;

            string[] steps = { "LIMPAR A ONDA", "PEGAR O PODER", "GASTAR NA LOJA", "PRÓXIMA ONDA" };
            float stepX = 44f;
            for (int i = 0; i < steps.Length; i++)
            {
                var stepBg = MakeImage($"Step_{i}", card.transform, roundedButtonSprite, new Color(1f, 1f, 1f, 0.07f));
                var srt = stepBg.rectTransform;
                srt.anchorMin = srt.anchorMax = new Vector2(0f, 0f);
                srt.pivot = new Vector2(0f, 0f);
                srt.anchoredPosition = new Vector2(stepX, 142f);
                srt.sizeDelta = new Vector2(210f, 38f);

                MakeText($"StepText_{i}", stepBg.transform, steps[i], 14f, TextMain, FontStyles.Bold,
                         new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(226f, 38f), TextAlignmentOptions.Center)
                    .characterSpacing = 6f;

                stepX += 210f;

                if (i < steps.Length - 1)
                {
                    MakeText($"Arrow_{i}", card.transform, "»", 20f, Gold, FontStyles.Bold,
                             new Vector2(0f, 0f), new Vector2(stepX + 4f, 148f), new Vector2(28f, 26f),
                             TextAlignmentOptions.Center);
                    stepX += 36f;
                }
            }

            var gotIt = MakeButton("GotIt", card.transform, roundedButtonSprite, Gold,
                                   new Vector2(0f, 0f), new Vector2(240f, 54f),
                                   "ENTENDI", 19f, Ink, FontStyles.Bold, TextAlignmentOptions.Center, out _, out _);
            var grt = (RectTransform)gotIt.transform;
            grt.anchorMin = grt.anchorMax = new Vector2(1f, 0f);
            grt.pivot = new Vector2(1f, 0f);
            grt.anchoredPosition = new Vector2(-44f, 64f);
            gotIt.onClick.AddListener(CloseHowTo);

            howToPanel.SetActive(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  HELPERS
        // ─────────────────────────────────────────────────────────────────────
        private Image MakeImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            if (sprite != null)
            {
                img.type = sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            }
            return img;
        }

        private TextMeshProUGUI MakeText(string name, Transform parent, string content, float size, Color color,
                                        FontStyles style, Vector2 anchor, Vector2 pos, Vector2 sizeDelta,
                                        TextAlignmentOptions align, bool wrap = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot     = new Vector2(anchor.x == 1f ? 1f : anchor.x == 0.5f ? 0.5f : 0f,
                                       anchor.y == 1f ? 1f : anchor.y == 0.5f ? 0.5f : 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.fontSize = size;
            tmp.color = color;

            // O "Bold" do TMP em fonte sem face negrito dobra o glifo (parece borrado).
            // Aqui o peso vem do próprio SDF: traço mais grosso e nítido em qualquer tamanho.
            tmp.fontStyle = FontStyles.Normal;
            if (style == FontStyles.Bold && boldMaterial != null) tmp.fontSharedMaterial = boldMaterial;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            if (wrap)
            {
                tmp.enableWordWrapping = true;
                tmp.overflowMode = TextOverflowModes.Overflow;
            }
            return tmp;
        }

        private Button MakeButton(string name, Transform parent, Sprite sprite, Color fillColor,
                                  Vector2 pos, Vector2 size, string label, float fontSize, Color labelColor,
                                  FontStyles style, TextAlignmentOptions align,
                                  out Image fillOut, out TextMeshProUGUI textOut)
        {
            var fill = MakeImage(name, parent, sprite, fillColor);
            var rt = fill.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var btn = fill.gameObject.AddComponent<Button>();
            btn.targetGraphic = fill;

            var colors = btn.colors;
            colors.normalColor      = Color.white;
            colors.highlightedColor = new Color(1.06f, 1.06f, 1.06f, 1f);
            colors.pressedColor     = new Color(0.88f, 0.88f, 0.88f, 1f);
            colors.selectedColor    = Color.white;
            colors.disabledColor    = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            colors.colorMultiplier  = 1f;
            btn.colors = colors;

            var labelText = MakeText(name + "_Label", fill.transform, label, fontSize, labelColor, style,
                                     new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(12f, 4f), align);

            fillOut = fill;
            textOut = labelText;
            return btn;
        }

        private Button MakeGhostButton(string name, Transform parent, string label, Vector2 pos, Vector2 size)
        {
            var btn = MakeButton(name, parent, roundedButtonSprite, new Color(1f, 1f, 1f, 0.05f),
                                 pos, size, label, 17f, TextMain, FontStyles.Bold,
                                 TextAlignmentOptions.Center, out var fill, out var text);
            text.characterSpacing = 6f;

            var border = MakeImage(name + "_Border", fill.transform, outlineButtonSprite, new Color(1f, 1f, 1f, 0.16f));
            var brt = border.rectTransform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(-1f, -1f);
            brt.offsetMax = new Vector2(1f, 1f);
            border.raycastTarget = false;
            border.transform.SetAsFirstSibling();

            return btn;
        }

        // contorno arredondado (9-slice): só o anel, miolo transparente
        private static Sprite RoundedOutlineSprite(int size, int radius, float thickness, Vector4 border)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[size * size];

            float half = (size - 1) * 0.5f;
            float halfRect = half - 0.5f;
            float r = radius - 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float qx = Mathf.Abs(x - half) - (halfRect - r);
                    float qy = Mathf.Abs(y - half) - (halfRect - r);
                    float mx = Mathf.Max(qx, 0f);
                    float my = Mathf.Max(qy, 0f);
                    float d = Mathf.Sqrt(mx * mx + my * my) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;  // < 0 = dentro

                    float alpha = (d <= 0f && d >= -thickness) ? 1f : 0f;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                                 SpriteMeshType.FullRect, border);
        }

        // sprite arredondado (9-slice) gerado em runtime — sem depender de prefab/asset
        private static Sprite RoundedSprite(int size, int radius, Vector4 border)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float a = 1f;
                    int cx = Mathf.Clamp(x, radius, size - 1 - radius);
                    int cy = Mathf.Clamp(y, radius, size - 1 - radius);
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    if (d > radius) a = Mathf.Clamp01(radius - d + 0.5f);

                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                                 SpriteMeshType.FullRect, border);
        }

        private enum GradientDirection { LeftToRight, BottomToTop }

        // gradiente de alpha (1 → 0) para os scrims da arte
        private static Sprite GradientSprite(GradientDirection direction)
        {
            const int w = 256, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float t = direction == GradientDirection.LeftToRight
                        ? x / (w - 1f)      // 0 = esquerda (opaco) → 1 = direita (transparente)
                        : y / (h - 1f);     // 0 = base (opaco) → 1 = topo (transparente)
                    byte a = (byte)Mathf.RoundToInt(Mathf.Lerp(255f, 0f, t));
                    px[y * w + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
