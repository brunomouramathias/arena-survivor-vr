#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using ArenaSurvivor.Core;
using ArenaSurvivor.Enemies;
using ArenaSurvivor.Items;
using ArenaSurvivor.UI;
using ArenaSurvivor.Waves;

namespace ArenaSurvivor.Utils
{
    // Prova do fluxo completo: onda limpa -> ícone de poder -> jogador encosta -> LOJA aparece
    // -> "PRÓXIMA ONDA" -> combate recomeça. Sem travamento (timeScale volta a 1).
    public class ShopFlowRunner : MonoBehaviour
    {
        public static bool AutoRun => SessionState.GetBool("ShopFlow.AutoRun", false);

        readonly StringBuilder sb = new StringBuilder();
        int errors;

        void Awake() { Application.logMessageReceived += OnLog; }
        void OnDestroy() { Application.logMessageReceived -= OnLog; }
        void OnLog(string c, string s, LogType t)
        {
            if (t == LogType.Error || t == LogType.Exception)
            {
                // exceção interna do editor (índice de busca do Unity) não é erro do jogo
                if (s.Contains("UnityEditor.Search")) return;
                errors++;
                if (errors <= 10) sb.AppendLine($"  [ERRO] {s.Replace("\n", " ")}");
            }
        }

        private IEnumerator Start()
        {
            if (!AutoRun) { enabled = false; yield break; }

            sb.AppendLine("=== TESTE: ícone de poder -> loja -> próxima onda ===");
            sb.AppendLine($"Data: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            yield return new WaitForSecondsRealtime(4f);

            var gm = GameManager.Instance;
            var wm = FindAnyObjectByType<WaveManager>();
            var player = GameObject.FindGameObjectWithTag("Player");
            sb.AppendLine($"ShopScreen na cena: {(ShopScreen.Instance != null)} | estado={gm?.CurrentState} timeScale={Time.timeScale} onda={wm?.CurrentWave}/{wm?.State}");

            // ── 1) mata a onda 1 inteira (caminho real do fim de onda) ───────
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 90f)
            {
                var enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
                foreach (var e in enemies) if (!e.IsDead) e.TakeDamage(99999);
                if (wm != null && wm.State == WaveState.Cleared) break;
                yield return new WaitForSecondsRealtime(0.5f);
            }
            sb.AppendLine($"[1] onda limpa? state={wm?.State} inimigos={FindObjectsByType<EnemyBase>(FindObjectsSortMode.None).Length}");

            // ── 2) espera o ícone brotar e o jogador encostar nele ───────────
            PowerUpPickup pu = null;
            t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 20f)
            {
                pu = FindAnyObjectByType<PowerUpPickup>();
                if (pu != null) break;
                yield return null;
            }
            sb.AppendLine($"[2] ícone de poder brotou: {(pu != null)} pos={(pu != null ? pu.transform.position.ToString() : "-")} ativos={PowerUpPickup.Active}");

            if (pu != null && player != null)
            {
                // "jogador encosta no ícone": traz o ícone até o jogador (teleportar o jogador
                // briga com o NavMesh/CharacterController)
                pu.transform.position = player.transform.position + Vector3.up * 1.1f;
                t0 = Time.realtimeSinceStartup;
                while (pu != null && !pu.Collected && Time.realtimeSinceStartup - t0 < 6f) yield return null;
                sb.AppendLine($"[2] ícone coletado: {(pu == null || pu.Collected)}");
            }

            yield return new WaitForSecondsRealtime(1.5f);

            // ── 3) a loja apareceu? ─────────────────────────────────────────
            var shopScreen = ShopScreen.Instance;
            var btns = shopScreen != null ? shopScreen.GetComponentsInChildren<Button>(true) : new Button[0];
            int activeBtns = 0;
            foreach (var b in btns) if (b.gameObject.activeInHierarchy) activeBtns++;

            bool shopOk = gm != null && gm.CurrentState == GameState.Shop &&
                          shopScreen != null && shopScreen.IsVisible && activeBtns >= 6;

            sb.AppendLine($"[3] estado={gm?.CurrentState} timeScale={Time.timeScale} telaVisivel={shopScreen?.IsVisible} botões={btns.Length} visíveis={activeBtns} => {(shopOk ? "PASS" : "FAIL")}");

            if (shopScreen != null && shopScreen.IsVisible)
            {
                foreach (var t in shopScreen.GetComponentsInChildren<Text>(true))
                    if (t.gameObject.activeInHierarchy)
                        sb.AppendLine($"      '{t.text}'");
                Capture("01_loja_onda.png");
            }

            // ── 3b) o clique do mouse realmente compra? ─────────────────────
            if (shopScreen != null && shopScreen.IsVisible)
            {
                var shopMgr = FindAnyObjectByType<ArenaSurvivor.Shop.ShopManager>();
                var score = ScoreService.Instance;
                int before = shopMgr != null ? shopMgr.GetUpgradeLevel(ArenaSurvivor.Shop.UpgradeType.Damage) : -1;
                int moneyBefore = score != null ? score.Currency : 0;

                bool clicked = ClickByRaycast(shopScreen.GetComponentInChildren<Button>(false));
                yield return new WaitForSecondsRealtime(0.3f);

                int after = shopMgr != null ? shopMgr.GetUpgradeLevel(ArenaSurvivor.Shop.UpgradeType.Damage) : -2;
                int moneyAfter = score != null ? score.Currency : 0;
                bool buyOk = clicked && after == before + 1 && moneyAfter < moneyBefore;
                sb.AppendLine($"[3b] clique no 1º botão: raycast={clicked} nível DANO {before}->{after} pontos {moneyBefore}->{moneyAfter} => {(buyOk ? "PASS" : "FAIL")}");
            }

            // ── 4) "PRÓXIMA ONDA" volta ao combate ──────────────────────────
            shopScreen?.Close();
            yield return new WaitForSecondsRealtime(1.0f);
            bool backOk = gm != null && gm.CurrentState == GameState.Playing && Mathf.Approximately(Time.timeScale, 1f);
            sb.AppendLine($"[4] após continuar: estado={gm?.CurrentState} timeScale={Time.timeScale} telaVisivel={shopScreen?.IsVisible} => {(backOk ? "PASS" : "FAIL")}");

            // ── 5) a onda seguinte começa (jogo não ficou parado) ───────────
            yield return new WaitForSecondsRealtime(6f);
            bool nextOk = wm != null && wm.CurrentWave >= 2 &&
                          Mathf.Approximately(Time.timeScale, 1f) &&
                          FindObjectsByType<EnemyBase>(FindObjectsSortMode.None).Length > 0;
            sb.AppendLine($"[5] onda={wm?.CurrentWave}/{wm?.State} inimigos={FindObjectsByType<EnemyBase>(FindObjectsSortMode.None).Length} timeScale={Time.timeScale} => {(nextOk ? "PASS" : "FAIL")}");

            // ── 6) trava de segurança: loja sem tela não congela ────────────
            wm?.StopAllCoroutines();   // isola o teste da máquina de ondas
            yield return null;
            if (shopScreen != null) shopScreen.gameObject.SetActive(false);
            gm?.SetState(GameState.Shop);
            yield return new WaitForSecondsRealtime(2.6f);
            bool guardOk = gm != null && gm.CurrentState == GameState.Playing && Mathf.Approximately(Time.timeScale, 1f);
            sb.AppendLine($"[6] loja sem tela: estado={gm?.CurrentState} timeScale={Time.timeScale} => {(guardOk ? "PASS" : "FAIL")}");

            // ── 7) vitória também tem saída (não fica em timeScale 0 sem UI) ─
            gm?.SetState(GameState.Victory);
            yield return new WaitForSecondsRealtime(0.5f);
            bool victoryOk = false;
            var hud = FindAnyObjectByType<GameHUD>();
            var panel = hud != null ? hud.transform.Find("Canvas_GameHUD/GameOverPanel") : null;
            if (panel != null && panel.gameObject.activeInHierarchy)
                foreach (var t in panel.GetComponentsInChildren<Text>(true))
                    if (t.text == "VITÓRIA!") victoryOk = true;
            sb.AppendLine($"[7] estado={gm?.CurrentState} timeScale={Time.timeScale} painel de vitória visível={victoryOk} => {(victoryOk ? "PASS" : "FAIL")}");

            sb.AppendLine($"\nErros/exceções: {errors}");
            sb.AppendLine($"RESULTADO: {(shopOk && backOk && nextOk && guardOk && victoryOk && errors == 0 ? "PASS" : "FAIL")}");
            Finish();
        }

        // Simula um clique de mouse de verdade: raycast do GraphicRaycaster + pointerClick
        private bool ClickByRaycast(Button btn)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (btn == null || es == null) return false;

            var rt = (RectTransform)btn.transform;
            Vector3 screen = RectTransformUtility.WorldToScreenPoint(Camera.main, rt.TransformPoint(rt.rect.center));
            var data = new UnityEngine.EventSystems.PointerEventData(es) { position = screen };

            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            es.RaycastAll(data, hits);
            bool hitButton = hits.Exists(h => h.gameObject == btn.gameObject) || hits.Exists(h => h.gameObject.transform.IsChildOf(btn.transform));
            if (!hitButton) return false;

            UnityEngine.EventSystems.ExecuteEvents.Execute(btn.gameObject, data,
                UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            return true;
        }

        private void Capture(string file)
        {
            var cam = Camera.main;
            if (cam == null) return;
            int w = 1600, h = 900;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            Directory.CreateDirectory("docs/screens/loja");
            File.WriteAllBytes("docs/screens/loja/" + file, tex.EncodeToPNG());
            Destroy(tex);
            Destroy(rt);
        }

        private void Finish()
        {
            Directory.CreateDirectory("docs/screens/loja");
            File.WriteAllText("docs/screens/loja/fluxo.txt", sb.ToString(), Encoding.UTF8);
            Debug.Log(sb.ToString());
            SessionState.SetBool("ShopFlow.AutoRun", false);
            EditorApplication.Exit(0);
        }
    }
}
#endif
