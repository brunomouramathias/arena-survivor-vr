#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ArenaSurvivor.Enemies;

namespace ArenaSurvivor.Editor
{
    // Joga a onda 1 do jogo real (sem pular nada) e registra quais inimigos nasceram,
    // capturando imagens quando os bruxos estiverem em cena.
    public static class WizardWaveProof
    {
        [MenuItem("Tools/Arena/Wizards: Prova Onda 1 (jogo normal)")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Arena.unity", OpenSceneMode.Single);
            var go = new GameObject("WizardWaveProofRunner");
            go.AddComponent<WizardWaveProofRunner>();
            EditorApplication.EnterPlaymode();
        }
    }

    public class WizardWaveProofRunner : MonoBehaviour
    {
        const string DIR = "docs/screens/wizards";
        string logPath;
        Camera cam;
        float t0, nextBeat;
        int shots;
        bool done;

        void Awake()
        {
            logPath = DIR + "/onda1.txt";
            Directory.CreateDirectory(DIR);
            File.WriteAllText(logPath, "=== PROVA ONDA 1 " + System.DateTime.Now.ToString("HH:mm:ss") + " ===\n");
        }

        void Log(string s) { try { File.AppendAllText(logPath, s + "\n"); } catch { } }

        void Start()
        {
            t0 = Time.time;
            nextBeat = 1f;
            var go = new GameObject("_WaveCam");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.11f, 0.14f);
            cam.fieldOfView = 55f;
            Log("[INIT] cena=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        void Update()
        {
            if (done) return;
            float t = Time.time - t0;

            if (t >= nextBeat)
            {
                nextBeat += 2f;
                var all = Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
                int chefes = all.Count(e => e is BossController);
                int magos = all.Count(e => e is EnemyShooter);
                Log($"t={t:0.0} inimigos={all.Length} (chefe/bruxo vermelho={chefes}, bruxo azul={magos}, outros={all.Length - chefes - magos})");
                foreach (var e in all.Where(e => e is BossController || e is EnemyShooter))
                {
                    var wa = e.GetComponent<WizardAnim>();
                    Log($"    - {(e is BossController ? "CHEFE vermelho" : "BRUXO azul")} {e.name} pos=({e.transform.position.x:0.0},{e.transform.position.z:0.0})"
                        + (wa != null ? $" wizardAnim=sim cast={wa.cast:0.00}" : " wizardAnim=NAO"));
                }

                // captura quando houver mago em cena
                if (shots < 3 && (chefes > 0 || magos > 0))
                {
                    Snap();
                }
            }

            if (t > 26f)
            {
                Log("[FIM]");
                done = true;
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += () => EditorApplication.Exit(0);
            }
        }

        void Snap()
        {
            var all = Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
            var focus = all.FirstOrDefault(e => e is BossController) ?? all.FirstOrDefault(e => e is EnemyShooter);
            if (focus == null) return;

            var f = focus.transform;
            float h = 2.0f;
            var renderers = focus.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                h = Mathf.Max(0.5f, b.max.y - f.position.y);
            }

            cam.transform.position = f.position + new Vector3(2.6f, h * 1.5f, -4.2f);
            cam.transform.LookAt(f.position + Vector3.up * (h * 0.55f));

            string name = shots == 0 ? "30_onda1_chefe.png" : shots == 1 ? "31_onda1_bruxo_azul.png" : "32_onda1_grupo.png";
            int w = 1600, hh = 900;
            var rt = new RenderTexture(w, hh, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, hh, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, hh), 0, 0); tex.Apply();
            cam.targetTexture = null; RenderTexture.active = null;
            File.WriteAllBytes(DIR + "/" + name, tex.EncodeToPNG());
            Destroy(tex); Destroy(rt);
            shots++;
            Log("[SHOT] " + name + " foco=" + focus.name);
        }
    }
}
#endif
