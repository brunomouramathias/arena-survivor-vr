#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using ArenaSurvivor.World;

namespace ArenaSurvivor.Utils
{
    // Play test do cenário: confere torres, dano dos inimigos, spawn nas paredes e erros.
    public class ScenarioPlayRunner : MonoBehaviour
    {
        public static bool AutoRun => SessionState.GetBool("Scenario.AutoRun", false);
        readonly StringBuilder sb = new StringBuilder();
        int errors;

        void Awake() { Application.logMessageReceived += OnLog; }
        void OnDestroy() { Application.logMessageReceived -= OnLog; }
        void OnLog(string c, string s, LogType t) { if (t == LogType.Error || t == LogType.Exception) errors++; }

        private IEnumerator Start()
        {
            if (!AutoRun) { enabled = false; yield break; }

            sb.AppendLine("=== [SCENARIO PLAY] " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");
            var towers = Object.FindObjectsByType<TowerHealth>(FindObjectsSortMode.None);
            sb.AppendLine($"Torres encontradas: {towers.Length}");
            foreach (var t in towers) sb.AppendLine($"  {t.name} em {t.transform.position} HP={t.Current}/{t.Max}");

            for (int i = 0; i < 7; i++)
            {
                yield return new WaitForSecondsRealtime(3f);
                var em = Object.FindObjectsByType<ArenaSurvivor.Enemies.EnemyBase>(FindObjectsSortMode.None);
                var gm = ArenaSurvivor.Core.GameManager.Instance;
                var wm = Object.FindAnyObjectByType<ArenaSurvivor.Waves.WaveManager>();
                sb.AppendLine($"t={3*(i+1)}s: inimigos={em.Length} state={(gm != null ? gm.CurrentState.ToString() : "null")} timeScale={Time.timeScale} wave={(wm != null ? wm.CurrentWave.ToString() + "/" + wm.State : "null")}");
            }

            float total = 0f;
            foreach (var t in towers)
            {
                if (t == null) continue;
                total += t.Current;
                sb.AppendLine($"  após 22s: {t.name} HP={t.Current}/{t.Max} destruida={t.Destroyed}");
            }
            sb.AppendLine($"Soma HP das torres: {total:F0}");
            var enemies = Object.FindObjectsByType<ArenaSurvivor.Enemies.EnemyBase>(FindObjectsSortMode.None);
            sb.AppendLine($"Inimigos vivos no fim: {enemies.Length}");
            sb.AppendLine($"Erros no console: {errors}");

            // captura do jogo em andamento (com HUD)
            CaptureGameView();

            // HUD de corações + teste de morte / game over
            var hud = Object.FindAnyObjectByType<ArenaSurvivor.UI.GameHUD>();
            sb.AppendLine($"HUD de corações: {(hud != null ? "presente" : "AUSENTE")}");
            if (hud != null)
            {
                var canvas = hud.GetComponentInChildren<Canvas>(true);
                sb.AppendLine($"  canvas: {(canvas != null)} mode={(canvas != null ? canvas.renderMode.ToString() : "-")} order={(canvas != null ? canvas.sortingOrder : -1)} enabled={(canvas != null ? canvas.enabled : false)}");
                var imgs = hud.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                foreach (var img in imgs)
                {
                    var rt = img.rectTransform;
                    var wc = new Vector3[4]; rt.GetWorldCorners(wc);
                    sb.AppendLine($"  img '{img.name}' sprite={(img.sprite != null)} color={img.color} size={rt.rect.size} world={wc[0]}..{wc[2]}");
                }
                var texts = hud.GetComponentsInChildren<UnityEngine.UI.Text>(true);
                foreach (var tt in texts)
                    sb.AppendLine($"  text '{tt.name}' = '{tt.text}' color={tt.color} active={tt.gameObject.activeInHierarchy}");
            }
            yield return null;
            ScreenCapture.CaptureScreenshot("docs/screens/cenario/24_hud_overlay.png");
            yield return new WaitForSecondsRealtime(0.5f);

            var ph = Object.FindAnyObjectByType<ArenaSurvivor.Player.PlayerHealth>();
            if (ph != null)
            {
                sb.AppendLine($"Vida inicial: {ph.CurrentHealth}/{ph.MaxHealth}");
                for (int i = 0; i < 6 && !ph.IsDead; i++)
                {
                    ph.TakeDamage(1);
                    yield return new WaitForSecondsRealtime(1.0f);
                }
                yield return new WaitForSecondsRealtime(0.8f);
                var gm2 = ArenaSurvivor.Core.GameManager.Instance;
                sb.AppendLine($"Após levar dano: HP={ph.CurrentHealth}/{ph.MaxHealth} morto={ph.IsDead} state={(gm2 != null ? gm2.CurrentState.ToString() : "null")}");
            }

            CaptureGameView("24_game_over.png");

            File.WriteAllText("docs/scenario_play.txt", sb.ToString(), Encoding.UTF8);
            Debug.Log(sb.ToString());
            SessionState.SetBool("Scenario.AutoRun", false);
            EditorApplication.Exit(0);
        }

        private void CaptureGameView(string file = "23_torres_em_jogo.png")
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGO = new GameObject("_ScenarioCam");
                cam = camGO.AddComponent<Camera>();
                cam.transform.position = new Vector3(0f, 14f, -20f);
                cam.transform.rotation = Quaternion.Euler(32f, 0f, 0f);
            }
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
            System.IO.Directory.CreateDirectory("docs/screens/cenario");
            System.IO.File.WriteAllBytes("docs/screens/cenario/" + file, tex.EncodeToPNG());
            Object.Destroy(tex); Object.Destroy(rt);
        }
    }
}
#endif
