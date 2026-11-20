#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using ArenaSurvivor.Enemies;

namespace ArenaSurvivor.Editor
{
    // Play test dos bruxos na Arena: registra o que a IA faz e captura
    // andando, conjurando (orbe de poder visivel) e o grupo final.
    public static class WizardPlayTest
    {
        [MenuItem("Tools/Arena/Wizards: Play Test (Arena)")]
        public static void Run() => Enter(true);

        public static void RunEmpty() => Enter(false);

        static void Enter(bool arena)
        {
            if (arena) EditorSceneManager.OpenScene("Assets/_Project/Scenes/Arena.unity", OpenSceneMode.Single);
            var runner = new GameObject("WizardPlayRunner");
            runner.AddComponent<WizardPlayRunner>();
            EditorApplication.EnterPlaymode();
        }
    }

    public class WizardPlayRunner : MonoBehaviour
    {
        const string DIR = "docs/screens/wizards";
        string logPath;
        GameObject boss, sorc;
        Camera cam;
        Transform player;
        float t0, nextBeat = 0.5f;
        bool shotWalk, shotBossCast, shotSorcCast, shotGroup, done;

        void Awake()
        {
            logPath = DIR + "/playtest.txt";
            Directory.CreateDirectory(DIR);
            File.WriteAllText(logPath, "=== WIZARD PLAY TEST " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===\n");
        }

        void Beat(string s)
        {
            try { File.AppendAllText(logPath, s + "\n"); } catch { }
        }

        void Start()
        {
            t0 = Time.time;
            Beat("[INIT] cena=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

            var go = new GameObject("_WizCam");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.11f, 0.14f);
            cam.fieldOfView = 55f;

            var bossP = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemy_Boss.prefab");
            var sorcP = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemy_Sorcerer.prefab");
            Beat("[INIT] prefabs boss=" + (bossP != null) + " sorcerer=" + (sorcP != null));

            var p = GameObject.FindGameObjectWithTag("Player");
            player = p != null ? p.transform : null;
            if (player == null)
            {
                var t = new GameObject("Player") { tag = "Player" };
                t.transform.position = new Vector3(0f, 0.1f, -1f);
                player = t.transform;
            }

            if (bossP != null) boss = Instantiate(bossP, new Vector3(-2.5f, 0.1f, 4.5f), Quaternion.Euler(0, 180, 0));
            if (sorcP != null) sorc = Instantiate(sorcP, new Vector3(2.5f, 0.1f, 4.5f), Quaternion.identity);

            if (boss != null) boss.GetComponent<EnemyBase>().Initialize(3);
            if (sorc != null) sorc.GetComponent<EnemyBase>().Initialize(3);
            Beat("[INIT] inimigos inicializados (Initialize(3))");

            foreach (var g in new[] { boss, sorc })
            {
                if (g == null) continue;
                var wa = g.GetComponent<WizardAnim>();
                Beat($"[ANIM] {g.name} WizardAnim={(wa != null)} animator={(g.GetComponent<Animator>() != null)}"
                    + $" renderers={g.GetComponentsInChildren<Renderer>().Length} escala={(wa != null && wa.modelRoot != null ? wa.modelRoot.localScale.x : 0f):0.000}");
            }
        }

        void Update()
        {
            if (done) return;
            float t = Time.time - t0;

            if (t >= nextBeat)
            {
                nextBeat += 1f;
                foreach (var kv in new[] { ("chefe", boss), ("atirador", sorc) })
                {
                    var g = kv.Item2;
                    if (g == null) { Beat($"t={t:0.0} {kv.Item1}: DESTRUIDO"); continue; }
                    var ag = g.GetComponent<NavMeshAgent>();
                    var wa = g.GetComponent<WizardAnim>();
                    string glow = "off";
                    foreach (var r in g.GetComponentsInChildren<Renderer>(true))
                        if (r.gameObject.name == "WizardPowerGlow") glow = r.gameObject.activeInHierarchy ? "ON" : "off";
                    Beat($"t={t:0.0} {kv.Item1}: pos=({g.transform.position.x:0.0},{g.transform.position.z:0.0})"
                        + $" vel={(ag != null ? ag.velocity.magnitude : 0f):0.0}m/s"
                        + $" speed={(wa != null ? wa.speed : -1f):0.00}"
                        + $" cast={(wa != null ? wa.cast : -1f):0.00}"
                        + $" glow={glow}");
                }
            }

            // capturas orientadas a evento
            if (!shotWalk && boss != null && Speed(boss) > 1.0f) { shotWalk = true; Snap(boss, "10_boss_andando.png", "chefe andando"); }
            if (!shotBossCast && boss != null && Cast(boss) > 0.45f) { shotBossCast = true; Snap(boss, "11_chefe_conjurando.png", "chefe conjurando (orbe)"); }
            if (!shotSorcCast && sorc != null && Cast(sorc) > 0.45f) { shotSorcCast = true; Snap(sorc, "12_bruxo_azul_conjurando.png", "bruxo azul conjurando (orbe)"); }
            if (!shotGroup && t > 8f)
            {
                shotGroup = true;
                Snap(sorc != null ? sorc : boss, "13_grupo.png", "grupo final");
            }

            if (t > 11f)
            {
                done = true;
                Beat("[FIM] concluido em " + t.ToString("0.0") + "s");
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += () => EditorApplication.Exit(0);
            }
        }

        static float Speed(GameObject g)
        {
            var wa = g.GetComponent<WizardAnim>();
            return wa != null ? wa.speed : 0f;
        }

        static float Cast(GameObject g)
        {
            var wa = g.GetComponent<WizardAnim>();
            return wa != null ? wa.cast : 0f;
        }

        void Snap(GameObject focus, string fname, string label)
        {
            var dir = (player.position - focus.transform.position); dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            var side = new Vector3(-dir.normalized.z, 0f, dir.normalized.x);
            cam.transform.position = focus.transform.position + Vector3.up * 2.0f + side * 2.6f - dir.normalized * 2.6f;
            cam.transform.LookAt(focus.transform.position + Vector3.up * 1.15f);

            int w = 1600, h = 900;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            cam.targetTexture = null; RenderTexture.active = null;
            File.WriteAllBytes(DIR + "/" + fname, tex.EncodeToPNG());
            Destroy(tex); Destroy(rt);
            Beat("[SHOT] " + fname + " — " + label);
        }
    }
}
#endif
