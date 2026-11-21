#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using ArenaSurvivor.Core;
using ArenaSurvivor.Enemies;
using ArenaSurvivor.UI;

namespace ArenaSurvivor.Utils
{
    // Teste da tela de abertura: layout, seleção de modo, "Como jogar", clique em INICIAR
    // e aplicação real da dificuldade na partida.
    public class MainMenuProbeRunner : MonoBehaviour
    {
        public static bool AutoRun => SessionState.GetBool("MainMenuProbe.AutoRun", false);

        readonly StringBuilder sb = new StringBuilder();
        int errors;
        string stage = "inicio";
        bool passed;
        bool finished;

        private static MainMenuProbeRunner current;

        void Awake()
        {
            // a sonda precisa sobreviver à troca de cena (Menu -> Arena) — e ser única:
            // recarregar o menu no meio do teste criaria uma segunda instância (cena de backup)
            if (current != null && current != this) { Destroy(gameObject); return; }
            current = this;

            DontDestroyOnLoad(gameObject);
            Application.logMessageReceived += OnLog;
        }
        void OnDestroy() { Application.logMessageReceived -= OnLog; }
        void OnLog(string c, string s, LogType t)
        {
            if (t == LogType.Error || t == LogType.Exception)
            {
                if (s.Contains("UnityEditor.Search")) return;
                errors++;
                if (errors <= 8) sb.AppendLine($"  [ERRO] {s.Replace("\n", " ")}");
            }
        }

        private IEnumerator Start()
        {
            if (!AutoRun) { enabled = false; yield break; }

            Directory.CreateDirectory("docs/screens/menu");
            StartCoroutine(Watchdog(180f));   // nunca deixa o batch pendurado

            sb.AppendLine("=== TESTE DA TELA DE ABERTURA ===");
            sb.AppendLine($"Data: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            stage = "inicio"; Debug.Log("[MENU-TESTE] iniciou");

            yield return new WaitForSecondsRealtime(1.5f);

            stage = "1"; Debug.Log("[MENU-TESTE] etapa 1");
            // ── 1) estrutura ─────────────────────────────────────────────────
            var screen = FindAnyObjectByType<MainMenuScreen>();
            bool menuOk = false;
            if (screen == null) sb.AppendLine("[1] MainMenuScreen AUSENTE => FAIL");
            else
            {
                var buttons = screen.GetComponentsInChildren<Button>(true);
                var texts   = screen.GetComponentsInChildren<TextMeshProUGUI>(true);
                var images  = screen.GetComponentsInChildren<Image>(true);

                var labels = new List<string>();
                foreach (var b in buttons)
                {
                    var t = b.GetComponentInChildren<TextMeshProUGUI>(true);
                    labels.Add(t != null ? t.text : b.name);
                }

                bool hasDifficulties = labels.Exists(l => l == "FÁCIL") && labels.Exists(l => l == "MÉDIO") && labels.Exists(l => l == "DIFÍCIL");
                bool hasStart        = labels.Exists(l => l.Contains("INICIAR PARTIDA"));
                bool hasHowTo        = labels.Exists(l => l.Contains("COMO JOGAR"));
                bool hasQuit         = labels.Exists(l => l == "SAIR");
                bool hasBackdrop     = System.Array.Exists(images, i => i.name == "Backdrop" && i.sprite != null);

                menuOk = hasDifficulties && hasStart && hasHowTo && hasQuit && hasBackdrop && buttons.Length >= 6;

                sb.AppendLine($"[1] canvas='{screen.name}' botões={buttons.Length} textos={texts.Length} imagens={images.Length}");
                sb.AppendLine($"     modo(fácil/médio/difícil)={hasDifficulties} iniciar={hasStart} comoJogar={hasHowTo} sair={hasQuit} arte={hasBackdrop} => {(menuOk ? "PASS" : "FAIL")}");
                foreach (var l in labels) sb.AppendLine($"       · {l}");

                screen.SelectDifficulty(1);          // estado padrão (MÉDIO) para a foto
                yield return Capture("01_menu.png");
            }

            stage = "2"; Debug.Log("[MENU-TESTE] etapa 2");
            // ── 2) trocar dificuldade muda a ficha do modo ───────────────────
            bool diffOk = false;
            if (screen != null)
            {
                bool hardOk   = CheckMode(screen, 2);
                yield return Capture("02_modo_dificil.png");

                bool easyOk   = CheckMode(screen, 0);
                yield return Capture("03_modo_facil.png");

                bool mediumOk = CheckMode(screen, 1);

                diffOk = hardOk && easyOk && mediumOk;
                sb.AppendLine($"[2] troca de modo: fácil={easyOk} médio={mediumOk} difícil={hardOk}");
                sb.AppendLine($"     => {(diffOk ? "PASS" : "FAIL")}");
            }

            stage = "3"; Debug.Log("[MENU-TESTE] etapa 3");
            // ── 3) "COMO JOGAR" abre com controles e estratégia ──────────────
            bool howToOk = false;
            if (screen != null)
            {
                var howToBtn = FindButton(screen, "COMO JOGAR");
                bool clicked = ClickByRaycast(howToBtn);
                yield return null;

                var panel = FindDeep(screen.transform, "HowToPanel");
                bool open = panel != null && panel.gameObject.activeInHierarchy;

                int chips = 0, tips = 0;
                string[] expectedKeys = { "W A S D", "MOUSE", "CLIQUE ESQ.", "R", "SHIFT / ESPAÇO", "1   2   3", "ESPAÇO / ENTER" };
                if (open)
                {
                    foreach (var t in panel.GetComponentsInChildren<TextMeshProUGUI>(true))
                    {
                        if (System.Array.IndexOf(expectedKeys, t.text) >= 0) chips++;
                        if (t.name.StartsWith("Tip_")) tips++;
                    }
                }

                yield return Capture("04_como_jogar.png");

                howToOk = clicked && open && chips >= 7 && tips == 6;
                sb.AppendLine($"[3] COMO JOGAR: clique={clicked} painelAberto={open} teclas={chips}/7 dicas={tips}/6 => {(howToOk ? "PASS" : "FAIL")}");

                // fecha com o botão FECHAR e confirma que some
                var closeBtn = FindButton(panel, "FECHAR  (ESC)");
                ClickByRaycast(closeBtn);
                yield return null;
                bool closed = !panel.gameObject.activeInHierarchy;
                sb.AppendLine($"[3b] fechar pelo botão: {closed} => {(closed ? "PASS" : "FAIL")}");
                howToOk &= closed;
            }

            stage = "4"; Debug.Log("[MENU-TESTE] etapa 4");
            // ── 4) INICIAR PARTIDA (clique real) abre a Arena com o modo ─────
            bool startOk = false;
            if (screen != null)
            {
                screen.SelectDifficulty(2);           // DIFÍCIL
                yield return null;
                sb.AppendLine($"[4] dificuldade selecionada = {Difficulty.Current} ({Difficulty.Profile.Label})");

                var startBtn = FindButton(screen, "INICIAR PARTIDA");
                bool clicked = ClickByRaycast(startBtn);
                sb.AppendLine($"    clique em INICIAR PARTIDA: {clicked}");

                float t0 = Time.realtimeSinceStartup;
                while (SceneManager.GetActiveScene().name != "Arena" && Time.realtimeSinceStartup - t0 < 15f)
                    yield return null;
                sb.AppendLine($"    troca de cena em {Time.realtimeSinceStartup - t0:F2}s");

                yield return new WaitForSecondsRealtime(0.35f);
                var scoreEarly = ScoreService.Instance;
                int startPoints = scoreEarly != null ? scoreEarly.Currency : -1;
                sb.AppendLine($"    pontos iniciais medidos={startPoints} (esperado 0 no difícil)");

                yield return new WaitForSecondsRealtime(4f);   // espera a onda 1

                var gm = GameManager.Instance;
                var score = ScoreService.Instance;
                var enemies = FindObjectsByType<EnemyBase>(FindObjectsInactive.Exclude);

                int hp = enemies.Length > 0 ? enemies[0].MaxHealth : -1;
                int expectedHp = Mathf.RoundToInt(Waves.WaveTuning.HealthForWave(1) * 1.30f);

                int scoreBefore = score != null ? score.CurrentScore : -1;
                score?.AddScore(100);
                int gained = score != null ? score.CurrentScore - scoreBefore : -1;

                startOk = gm != null && gm.CurrentState == GameState.Playing &&
                          Difficulty.Current == GameDifficulty.Hard &&
                          hp == expectedHp &&
                          startPoints <= 8 &&          // difícil: 0 pontos iniciais (só o ganho por segundo)
                          gained >= 130 && gained <= 134;   // pontuação ×1,30

                sb.AppendLine($"[4] Arena: estado={gm?.CurrentState} modo={Difficulty.Current}");
                sb.AppendLine($"    inimigo: vida={hp} (esperado {expectedHp} = 36 × 1,30)");
                sb.AppendLine($"    +100 pontos virou +{gained} pontuação (esperado 130)");
                sb.AppendLine($"    => {(startOk ? "PASS" : "FAIL")}");

                // HUD mostra o modo
                var hud = FindAnyObjectByType<GameHUD>();
                string hudInfo = "";
                if (hud != null)
                    foreach (var t in hud.GetComponentsInChildren<Text>(true))
                        if (t.text != null && t.text.StartsWith("TEMPO")) hudInfo = t.text;
                sb.AppendLine($"    HUD: '{hudInfo}' => {(hudInfo.Contains("DIFÍCIL") ? "PASS" : "FAIL")}");
            }

            stage = "5"; Debug.Log("[MENU-TESTE] etapa 5");
            // ── 5) partida em FÁCIL começa com pontos e inimigos mais fracos ─
            bool easyMatchOk = false;
            {
                SceneManager.LoadScene("MainMenu");
                yield return new WaitForSecondsRealtime(2.5f);

                var screen2 = FindAnyObjectByType<MainMenuScreen>();
                sb.AppendLine($"[5] menu recarregado: tela={(screen2 != null)} modo atual={Difficulty.Current} pref={PlayerPrefs.GetString("arena.difficulty", "<vazio>")}");

                if (screen2 != null)
                {
                    screen2.SelectDifficulty(0);
                    yield return null;
                    sb.AppendLine($"    após escolher FÁCIL: modo={Difficulty.Current} pref={PlayerPrefs.GetString("arena.difficulty", "<vazio>")}");

                    var startBtn = FindButton(screen2, "INICIAR PARTIDA");
                    bool clicked = ClickByRaycast(startBtn);
                    sb.AppendLine($"    clique em INICIAR PARTIDA: {clicked}");

                    float t0 = Time.realtimeSinceStartup;
                    while (SceneManager.GetActiveScene().name != "Arena" && Time.realtimeSinceStartup - t0 < 15f)
                        yield return null;

                    yield return new WaitForSecondsRealtime(0.35f);
                    var scoreEarly = ScoreService.Instance;
                    int startPoints = scoreEarly != null ? scoreEarly.Currency : -1;

                    yield return new WaitForSecondsRealtime(4f);

                    var score = ScoreService.Instance;
                    var enemies = FindObjectsByType<EnemyBase>(FindObjectsInactive.Exclude);
                    int hp = enemies.Length > 0 ? enemies[0].MaxHealth : -1;
                    int expectedHp = Mathf.RoundToInt(Waves.WaveTuning.HealthForWave(1) * 0.80f);

                    easyMatchOk = Difficulty.Current == GameDifficulty.Easy &&
                                  startPoints >= 150 && startPoints <= 160 &&
                                  hp == expectedHp;

                    sb.AppendLine($"[5] FÁCIL: modo={Difficulty.Current} pontos iniciais={startPoints} (esperado 150) " +
                                  $"vida={hp} (esperado {expectedHp}) => {(easyMatchOk ? "PASS" : "FAIL")}");
                }
            }

            passed = menuOk && diffOk && howToOk && startOk && easyMatchOk && errors == 0;

            sb.AppendLine($"\nErros/exceções: {errors}");
            sb.AppendLine($"RESULTADO: {(passed ? "PASS" : "FAIL")}");

            Finish();
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;

            Directory.CreateDirectory("docs/screens/menu");
            File.WriteAllText("docs/screens/menu/menu_probe.txt", sb.ToString(), Encoding.UTF8);

            // o teste não deve deixar o modo salvo "vazando" para a próxima sessão do jogador
            PlayerPrefs.DeleteKey("arena.difficulty");
            PlayerPrefs.Save();
            Debug.Log("[MENU-TESTE] fim " + sb);
            SessionState.SetBool("MainMenuProbe.AutoRun", false);
            EditorApplication.Exit(0);
        }

        // ── helpers ─────────────────────────────────────────────────────────
        // Confere se a tela mostra exatamente o perfil do modo pedido
        private bool CheckMode(MainMenuScreen screen, int index)
        {
            screen.SelectDifficulty(index);

            var mode    = Difficulty.AtIndex(index);
            var profile = Difficulty.Get(mode);

            string headline = TextOf(screen, "ModeHeadline");
            string desc     = TextOf(screen, "ModeDescription");
            string stats    = StatsOf(screen);

            bool ok = Difficulty.Current == mode &&
                      headline == profile.Headline &&
                      desc == profile.Description &&
                      stats.Contains(profile.HealthText) &&
                      stats.Contains(profile.ScoreText);

            sb.AppendLine($"     {profile.Label}: '{headline}' | ficha = {stats} => {(ok ? "PASS" : "FAIL")}");
            return ok;
        }
        private static string TextOf(MainMenuScreen screen, string name)
        {
            if (screen == null) return "";
            var t = FindDeep(screen.transform, name);
            var tmp = t != null ? t.GetComponent<TextMeshProUGUI>() : null;
            return tmp != null ? tmp.text : "";
        }

        // concatena os valores da ficha do modo (linhas "Value_*")
        private static string StatsOf(MainMenuScreen screen)
        {
            if (screen == null) return "";
            var parts = new List<string>();
            foreach (var t in screen.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (t.name.StartsWith("Value_")) parts.Add(t.text);
            return string.Join(" | ", parts);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static Button FindButton(Component root, string label)
        {
            if (root == null) return null;
            foreach (var b in root.GetComponentsInChildren<Button>(true))
            {
                var t = b.GetComponentInChildren<TextMeshProUGUI>(true);
                if (t != null && t.text.Contains(label)) return b;
            }
            return null;
        }

        // Simula um clique de mouse real: raycast do GraphicRaycaster + pointerClick
        private bool ClickByRaycast(Button btn)
        {
            var es = EventSystem.current;
            if (btn == null || es == null) return false;

            var rt = (RectTransform)btn.transform;
            var canvas = btn.GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector3 screen = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));
            var data = new PointerEventData(es) { position = screen };

            var hits = new List<RaycastResult>();
            es.RaycastAll(data, hits);
            bool hit = hits.Exists(h => h.gameObject == btn.gameObject || h.gameObject.transform.IsChildOf(btn.transform));
            if (!hit) return false;

            ExecuteEvents.Execute(btn.gameObject, data, ExecuteEvents.pointerClickHandler);
            return true;
        }

        private IEnumerator Watchdog(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (finished) yield break;
            sb.AppendLine($"[TIMEOUT] teste não terminou em {seconds}s");
            Finish();
        }

        private IEnumerator Capture(string file)
        {
            var cam = Camera.main;
            int w = 1600, h = 900;

            // canvas em ScreenSpaceOverlay não passa pela câmera: renderiza a tela do jogo
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            if (cam != null)
            {
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
            }

            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            if (cam != null)
            {
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
            }
            else
            {
                var src = ScreenCapture.CaptureScreenshotAsTexture();
                tex = src;
            }

            File.WriteAllBytes("docs/screens/menu/" + file, tex.EncodeToPNG());
            Destroy(tex);
            Destroy(rt);
            yield return null;
        }
    }
}
#endif
