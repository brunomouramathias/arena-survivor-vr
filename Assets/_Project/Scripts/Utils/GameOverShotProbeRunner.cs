#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using ArenaSurvivor.Core;
using ArenaSurvivor.UI;
using ArenaSurvivor.World;

namespace ArenaSurvivor.Utils
{
    // Derruba as quatro torres depois de ~1 minuto e fotografa a tela de fim de jogo,
    // para conferir o tempo de sobrevivência em minutos.
    public class GameOverShotProbeRunner : MonoBehaviour
    {
        public static bool AutoRun => SessionState.GetBool("GameOverShot.AutoRun", false);

        bool finished;

        private IEnumerator Start()
        {
            if (!AutoRun) { enabled = false; yield break; }

            StartCoroutine(Watchdog(150f));

            // deixa o relógio passar de 59 s (o bônus de tempo usa PlaySeconds)
            yield return new WaitForSecondsRealtime(66f);

            var hud = FindAnyObjectByType<GameHUD>();
            var score = ScoreService.Instance;

            foreach (var t in TowerHealth.All)
                if (t != null && !t.Destroyed) t.TakeDamage(t.Max + 50);

            yield return new WaitForSecondsRealtime(1.5f);

            string texto = "";
            if (hud != null)
                foreach (var txt in hud.GetComponentsInChildren<Text>(true))
                    if (txt != null && txt.text != null && txt.text.StartsWith("Sobreviveu")) texto = txt.text;

            Debug.Log($"[FIM] {texto}");

            yield return Capture("gameover_tempo.png");
            EditorApplication.Exit(string.IsNullOrEmpty(texto) ? 1 : 0);
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

            Directory.CreateDirectory("docs/screens/hud");
            File.WriteAllBytes("docs/screens/hud/" + file, tex.EncodeToPNG());
            Destroy(tex);
            Destroy(rt);
            yield return null;
        }

        private IEnumerator Watchdog(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (!finished) { finished = true; EditorApplication.Exit(1); }
        }
    }
}
#endif
