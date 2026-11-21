#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using ArenaSurvivor.UI;
using ArenaSurvivor.World;

namespace ArenaSurvivor.Utils
{
    // Verifica o aviso pequeno que aparece no HUD quando uma torre cai.
    public class TowerToastProbeRunner : MonoBehaviour
    {
        public static bool AutoRun => SessionState.GetBool("TowerToastProbe.AutoRun", false);

        readonly StringBuilder sb = new StringBuilder();
        bool finished;

        private IEnumerator Start()
        {
            if (!AutoRun) { enabled = false; yield break; }

            StartCoroutine(Watchdog(60f));
            yield return new WaitForSecondsRealtime(1.0f);

            sb.AppendLine("=== AVISO DE TORRE CAINDO (HUD) ===");
            sb.AppendLine($"Data: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

            var hud = FindAnyObjectByType<GameHUD>();
            var toast = FindToast(hud);
            sb.AppendLine($"[1] HUD presente={hud != null} aviso presente={toast != null} " +
                          $"=> {((hud != null && toast != null) ? "PASS" : "FAIL")}");
            if (toast == null) { Finish(); yield break; }

            sb.AppendLine($"[2] antes de cair: texto='{toast.text}' alfa={toast.color.a:F2} " +
                          $"=> {((toast.text.Length == 0 && toast.color.a < 0.01f) ? "PASS" : "FAIL")}");

            // derruba a primeira torre
            var tower = TowerHealth.All.Find(t => t != null && !t.Destroyed);
            if (tower == null) { sb.AppendLine("[ERRO] nenhuma torre encontrada"); Finish(); yield break; }

            int hp = tower.Max;
            tower.TakeDamage(hp + 10);
            yield return null;

            bool destroyed = tower.Destroyed;
            string txt = toast.text;
            float alfa = toast.color.a;
            yield return Capture("torre_caindo.png");

            bool avisoOk = destroyed && txt.Contains("TORRE") && txt.Contains("DE PÉ") && alfa > 0.5f;
            sb.AppendLine($"[3] torre derrubada={destroyed} aviso='{txt}' alfa={alfa:F2} " +
                          $"=> {(avisoOk ? "PASS" : "FAIL")}");

            // o aviso some sozinho depois de alguns segundos
            yield return new WaitForSecondsRealtime(3.8f);
            bool sumiu = toast.color.a < 0.05f;
            sb.AppendLine($"[4] aviso desapareceu sozinho: alfa={toast.color.a:F2} => {(sumiu ? "PASS" : "FAIL")}");

            bool pass = hud != null && toast != null && destroyed && avisoOk && sumiu;
            sb.AppendLine($"\n=== RESULTADO: {(pass ? "PASS" : "FAIL")} ===");
            Finish();
        }

        private IEnumerator Capture(string file)
        {
            var cam = Camera.main;
            int w = 1600, h = 900;

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

            File.WriteAllBytes("docs/screens/hud/" + file, tex.EncodeToPNG());
            Destroy(tex);
            Destroy(rt);
            yield return null;
        }

        private static Text FindToast(GameHUD hud)
        {
            if (hud == null) return null;
            foreach (var t in hud.GetComponentsInChildren<Text>(true))
                if (t.gameObject.name == "TowerToast") return t;
            return null;
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;

            Directory.CreateDirectory("docs/screens/hud");
            File.WriteAllText("docs/screens/hud/torre.txt", sb.ToString(), Encoding.UTF8);
            Debug.Log("[TORRE] " + sb);
            SessionState.SetBool("TowerToastProbe.AutoRun", false);
            EditorApplication.Exit(0);
        }

        private IEnumerator Watchdog(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (!finished)
            {
                sb.AppendLine("[TIMEOUT] a sonda não terminou");
                sb.AppendLine("\n=== RESULTADO: FAIL ===");
                Finish();
            }
        }
    }
}
#endif
