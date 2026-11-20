#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ArenaSurvivor.Core;
using ArenaSurvivor.Enemies;
using ArenaSurvivor.Player;
using ArenaSurvivor.Waves;
using ArenaSurvivor.World;

namespace ArenaSurvivor.Editor
{
    // Valida em jogo as melhorias pedidas:
    // 1) barra de vida em cima dos inimigos
    // 2) onda 1 morre em no maximo 3 tiros
    // 3) poucos inimigos na onda 1, crescendo depois
    // 4) jogo so acaba quando a ultima torre cai
    // 5) pontos por tempo, torres e vida das torres
    public static class ArenaImprovementsTest
    {
        [MenuItem("Tools/Arena/Testar Melhorias (checklist)")]
        public static void Run()
        {
            // Evita o diálogo de "recuperar cenas" quando uma execução anterior foi interrompida
            try { if (Directory.Exists("Temp/__Backupscenes")) Directory.Delete("Temp/__Backupscenes", true); } catch { }

            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Arena.unity", OpenSceneMode.Single);

            // Cria o runner já e pede o playmode na hora (padrão que funciona no batchmode)
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;

            SpawnRunner();
            EditorApplication.EnterPlaymode();
            EditorApplication.delayCall += RetryPlaymode;
        }

        static double retryAt;

        static void RetryPlaymode()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (retryAt == 0) retryAt = EditorApplication.timeSinceStartup + 5.0;
            if (EditorApplication.timeSinceStartup < retryAt) { EditorApplication.delayCall += RetryPlaymode; return; }

            retryAt = EditorApplication.timeSinceStartup + 5.0;
            Debug.Log("[TESTE] playmode não iniciou — tentando novamente");
            EditorApplication.EnterPlaymode();
            EditorApplication.delayCall += RetryPlaymode;
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            SpawnRunner();
        }

        static void SpawnRunner()
        {
            if (Object.FindFirstObjectByType<ImprovementsRunner>() != null) return;
            var go = new GameObject("ImprovementsRunner");
            go.AddComponent<ImprovementsRunner>();
        }
    }

    public class ImprovementsRunner : MonoBehaviour
    {
        const string DIR = "docs/screens/melhorias";
        // pasta de saida absoluta (o CWD muda entre batchmode e editor grafico)
        static string AbsDir => Path.Combine(
            Directory.GetParent(Application.dataPath).FullName, "docs/screens/melhorias");

        string logPath;
        Camera cam;
        int pass, fail;
        bool done;
        float scoreT1;
        int scoreAtT1, scoreAtT2;

        void Awake()
        {
            Directory.CreateDirectory(AbsDir);
            logPath = AbsDir + "/relatorio.txt";
            File.WriteAllText(logPath, "=== CHECKLIST DE MELHORIAS " + System.DateTime.Now.ToString("dd/MM HH:mm") + " ===\n");
        }

        void Check(string name, bool ok, string detail)
        {
            if (ok) pass++; else fail++;
            Log((ok ? "[OK]   " : "[FALHA]") + " " + name + " — " + detail);
        }

        void Log(string s) { try { File.AppendAllText(logPath, s + "\n"); } catch { } Debug.Log(s); }

        void Start()
        {
            var go = new GameObject("_TestCam");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.11f, 0.14f);
            cam.fieldOfView = 55f;
            StartCoroutine(RunAll());
        }

        IEnumerator RunAll()
        {
            Log("[INIT] cena=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

            // ---------------------------------------------------------------- 3) escalonamento
            var cfg = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_Project/ScriptableObjects/Waves/WaveConfig_Default.asset");
            var counts = new List<int>();
            for (int w = 1; w <= 20; w++) counts.Add(cfg.GetEnemyCount(w));
            Log("[ONDAS] contagem = " + string.Join(", ", counts.Select((c, i) => $"o{i + 1}:{c}")));
            bool cresce = counts[0] <= 5 && counts[1] > counts[0] && counts[5] > counts[1] && counts[9] > counts[5];
            Check("Poucos inimigos na onda 1 e crescimento por onda", cresce && counts[0] <= 5,
                  $"onda1={counts[0]} onda2={counts[1]} onda6={counts[5]} onda10={counts[9]} onda20={counts[19]}");

            // ---------------------------------------------------------------- 2) 3 tiros na onda 1
            var chaser = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemy_Chaser.prefab");
            var sorcerer = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemy_Sorcerer.prefab");
            var boss = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemy_Boss.prefab");
            var stalker = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemy_Stalker.prefab");

            var e1 = Instantiate(chaser, new Vector3(-4f, 0f, -5f), Quaternion.identity);
            var e2 = Instantiate(sorcerer, new Vector3(-1f, 0f, -5f), Quaternion.identity);
            yield return null;

            var b1 = e1.GetComponent<EnemyBase>();
            var b2 = e2.GetComponent<EnemyBase>();
            b1.Initialize(1); b2.Initialize(1);
            yield return null;

            Check("HP da onda 1 limitado a 3 tiros da pistola (18 de dano)",
                  b1.MaxHealth <= 54 && b2.MaxHealth <= 54,
                  $"chasers={b1.MaxHealth}HP, bruxo azul={b2.MaxHealth}HP (limite 54)");

            // 3 tiros de 18
            b1.TakeDamage(18); b1.TakeDamage(18); b1.TakeDamage(18);
            b2.TakeDamage(18); b2.TakeDamage(18); b2.TakeDamage(18);
            yield return null;
            Check("Todos os inimigos da onda 1 morrem em ate 3 tiros", b1.IsDead && b2.IsDead,
                  $"chaser morto={b1.IsDead}, bruxo morto={b2.IsDead}");

            // ---------------------------------------------------------------- 1) barra de vida
            var e3 = Instantiate(chaser, new Vector3(-3f, 0f, -2f), Quaternion.identity);
            var e4 = Instantiate(sorcerer, new Vector3(0.2f, 0f, -2f), Quaternion.identity);
            var e5 = Instantiate(boss, new Vector3(3.6f, 0f, -2f), Quaternion.identity);
            yield return null;
            var b3 = e3.GetComponent<EnemyBase>();
            var b4 = e4.GetComponent<EnemyBase>();
            var b5 = e5.GetComponent<EnemyBase>();
            b3.Initialize(1); b4.Initialize(1); b5.Initialize(3);
            yield return null;

            var inimigos = new[] { b3, b4, b5 };
            var bars = inimigos.Select(b => b.GetComponent<EnemyHealthBar>()).ToArray();
            var barsOk = bars.All(x => x != null && x.IsBuilt && x.IsVisible);
            Check("Barra de vida criada em chaser, bruxo azul e chefe",
                  barsOk, "barras=" + bars.Count(x => x != null && x.IsBuilt) + "/3");

            float[] barAlturas = bars.Select((x, i) => x != null
                ? x.BarWorldPosition.y - inimigos[i].transform.position.y : -1f).ToArray();
            Check("Barra fica acima da cabeça do inimigo",
                  barAlturas.All(h => h > 0.5f),
                  "alturas=" + string.Join(",", barAlturas.Select(h => h.ToString("0.00"))));

            // dano parcial deixa a barra pela metade (visivel, nao some)
            b3.TakeDamage(18);
            yield return null;
            Check("Barra continua visível após levar dano (mostra o quanto falta)",
                  bars[0] != null && bars[0].IsVisible && bars[0].Normalized < 1f && bars[0].Normalized > 0f,
                  bars[0] != null ? $"normalizado={bars[0].Normalized:0.00}" : "barra ausente");

            // esconde as torres para os closes ficarem limpos (depois volta tudo)
            var torresGO = TowerHealth.All.Where(t => t != null).Select(t => t.gameObject).ToArray();
            foreach (var g in torresGO) g.SetActive(false);
            yield return null;

            yield return ShotEnemy("10_barra_monstro.png", b3);
            yield return ShotEnemy("11_barra_bruxo_azul.png", b4);
            yield return ShotEnemy("12_barra_chefe.png", b5);

            foreach (var g in torresGO) if (g != null) g.SetActive(true);
            yield return null;

            // ---------------------------------------------------------------- 5a) pontos por tempo
            scoreAtT1 = ScoreService.Instance.CurrentScore;
            scoreT1 = Time.unscaledTime;
            yield return new WaitForSecondsRealtime(2.0f);
            scoreAtT2 = ScoreService.Instance.CurrentScore;
            float dt = Time.unscaledTime - scoreT1;
            Check("Pontos por segundo enquanto o jogo roda",
                  scoreAtT2 > scoreAtT1,
                  $"{scoreAtT1} -> {scoreAtT2} em {dt:0.0}s");

            // limpa os inimigos de teste para nao interferir na onda
            foreach (var e in new[] { e3, e4, e5 })
                if (e != null) e.SetActive(false);

            // ---------------------------------------------------------------- onda 1 real
            yield return new WaitForSecondsRealtime(4f);
            var vivos = Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None).Where(e => !e.IsDead).ToArray();
            Log("[ONDA1] vivos=" + vivos.Length + " tipos=" +
                string.Join(",", vivos.GroupBy(v => v.GetType().Name).Select(g => g.Key + "x" + g.Count())));
            Check("Onda 1 nasce com poucos inimigos (<= 6)",
                  vivos.Length > 0 && vivos.Length <= 6, "vivos=" + vivos.Length);

            // diagnostico: onde a barra esta em relacao a cada inimigo vivo
            int barrasOkOnda = 0;
            foreach (var e in vivos)
            {
                var bar = e.GetComponent<EnemyHealthBar>();
                float delta = bar != null ? bar.BarWorldPosition.y - e.transform.position.y : -99f;
                bool ok = bar != null && bar.IsVisible && delta > 0.8f && delta < 2.6f;
                if (ok) barrasOkOnda++;
                var p = e.transform.position;
                float topo = -99f;
                foreach (var r in e.GetComponentsInChildren<Renderer>())
                    if (r != null && r.enabled) topo = Mathf.Max(topo, r.bounds.max.y);
                Log($"[BARRA] {e.name} tipo={e.GetType().Name} pos=({p.x:0.0},{p.z:0.0}) " +
                    $"yInimigo={p.y:0.00} yBarra={(bar != null ? bar.BarWorldPosition.y : -99f):0.00} " +
                    $"topoModelo={topo:0.00} delta={delta:0.00} visivel={bar?.IsVisible} ok={ok}");
            }
            Check("Barras corretas nos inimigos da onda 1 (acima da cabeça)",
                  barrasOkOnda == vivos.Length,
                  $"{barrasOkOnda}/{vivos.Length} corretas");

            // close-up em um inimigo real da onda
            var alvo = vivos.FirstOrDefault();
            if (alvo != null)
            {
                var tp = alvo.transform.position;
                yield return ShotHud("11_onda1_com_barras.png",
                                     tp + new Vector3(2.6f, 2.4f, -4.6f), tp + Vector3.up * 1.15f);
            }

            // ---------------------------------------------------------------- 4) nocaute x torres
            // desliga os inimigos e para o spawn para o teste de nocaute ficar determinístico
            Object.FindFirstObjectByType<WaveManager>()?.StopAllCoroutines();
            var todos = Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
            foreach (var e in todos) if (e != null) e.gameObject.SetActive(false);

            var ph = Object.FindFirstObjectByType<PlayerHealth>();
            int torresAntes = TowerHealth.All.Count(x => x != null && !x.Destroyed);

            if (ph != null)
            {
                // enche a vida primeiro para o teste ser determinístico
                ph.Heal(99);
                yield return null;

                // golpes espacados (cada golpe da 0,9s de invulnerabilidade) até nocautear
                int golpes = 0;
                while (!ph.IsDead && golpes < 6)
                {
                    golpes++;
                    ph.SetInvincible(false);
                    ph.TakeDamage(1);
                    Log($"[GOLPE {golpes}] vida={ph.CurrentHealth}/{ph.MaxHealth} " +
                        $"inv={ph.IsInvincible} morto={ph.IsDead}");
                    yield return new WaitForSecondsRealtime(1.0f);
                }
                Log($"[NOCAUTE] morto={ph.IsDead} vida={ph.CurrentHealth}/{ph.MaxHealth} " +
                    $"estado={GameManager.Instance.CurrentState} torres={torresAntes}");
                Check("Jogador nocauteado NAO encerra o jogo (torres de pe)",
                      GameManager.Instance.CurrentState == GameState.Playing,
                      "estado=" + GameManager.Instance.CurrentState);

                yield return new WaitForSecondsRealtime(2.5f);
                Check("Jogador volta para a arena com vida cheia",
                      !ph.IsDead && ph.CurrentHealth == ph.MaxHealth,
                      $"vida={ph.CurrentHealth}/{ph.MaxHealth} morto={ph.IsDead} estado={GameManager.Instance.CurrentState}");
            }
            else Check("Jogador encontrado na cena", false, "PlayerHealth nao encontrado");

            // ---------------------------------------------------------------- 5b) bonus com torres de pe
            var ss = ScoreService.Instance;
            int scoreAntesBonus = ss.CurrentScore;
            float tempoJogo = ss.PlaySeconds;

            // danifica uma torre para o bonus de vida nao ser 100%
            var torre = TowerHealth.All.FirstOrDefault(x => x != null && !x.Destroyed);
            if (torre != null) torre.TakeDamage(torre.Max / 2);
            yield return new WaitForSecondsRealtime(0.2f);

            int esperadoTorres = TowerHealth.All.Count(x => x != null && !x.Destroyed) * 250;
            GameManager.Instance.SetState(GameState.Victory);   // dispara o bônus final
            yield return new WaitForSecondsRealtime(0.3f);

            Check("Bônus de torres de pé = torres vivas x 250",
                  ss.TowerBonus == esperadoTorres && ss.TowerBonus > 0,
                  $"torres={ss.TowerBonus} (esperado {esperadoTorres})");
            Check("Bônus de vida das torres > 0 com torres danificadas",
                  ss.TowerHealthBonus > 0 && ss.TowerHealthBonus < 4 * 500,
                  $"vida={ss.TowerHealthBonus} (max {4 * 500})");
            Check("Bônus de tempo = segundos sobrevividos x 10",
                  ss.TimeBonus > 0 && Mathf.Abs(ss.TimeBonus - Mathf.RoundToInt(tempoJogo) * 10) <= 10,
                  $"tempo={ss.TimeBonus} para {tempoJogo:0.0}s");
            Check("Pontuação final recebeu o bônus",
                  ss.CurrentScore >= scoreAntesBonus + ss.TimeBonus + ss.TowerBonus + ss.TowerHealthBonus,
                  $"score {scoreAntesBonus} -> {ss.CurrentScore}");

            // ---------------------------------------------------------------- derrota na ultima torre
            var vivasAntes = TowerHealth.All.Count(x => x != null && !x.Destroyed);
            foreach (var t in TowerHealth.All.ToArray())
                if (t != null && !t.Destroyed) t.TakeDamage(99999);
            yield return new WaitForSecondsRealtime(0.5f);

            Check("Derrota só quando a ÚLTIMA torre cai",
                  GameManager.Instance.CurrentState == GameState.GameOver,
                  $"estado={GameManager.Instance.CurrentState}, torres antes={vivasAntes}, " +
                  $"vivas agora={TowerHealth.All.Count(x => x != null && !x.Destroyed)}");

            yield return ShotHud("12_fim_de_jogo.png", new Vector3(0f, 2.0f, -12f), new Vector3(0f, 2.0f, 0f));

            Log($"\n=== RESULTADO: {pass} OK / {fail} FALHAS ===");
            done = true;
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += () => EditorApplication.Exit(fail == 0 ? 0 : 1);
        }

        IEnumerator Shot(string name, Vector3 camPos, Vector3 lookAt)
        {
            yield return ShotHud(name, camPos, lookAt);
        }

        // Close em um inimigo para conferir a barra de vida no lugar certo
        IEnumerator ShotEnemy(string name, EnemyBase e)
        {
            if (e == null) { Log("[SHOT] " + name + " ignorado"); yield break; }
            var p = e.transform.position;
            yield return ShotHud(name, p + new Vector3(2.0f, 2.0f, -3.6f), p + Vector3.up * 1.25f);
        }

        // Captura com o HUD do jogo aparecendo (aponta o canvas do HUD para a camera do teste)
        IEnumerator ShotHud(string name, Vector3 camPos, Vector3 lookAt)
        {
            int w = 1600, h = 900;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.transform.position = camPos;
            cam.transform.LookAt(lookAt);

            Canvas hud = null;
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.name == "Canvas_GameHUD") { hud = c; break; }
            if (hud != null) hud.worldCamera = cam;

            cam.targetTexture = rt;
            cam.Render();
            if (hud != null) Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            File.WriteAllBytes(AbsDir + "/" + name, tex.EncodeToPNG());
            Destroy(tex); Destroy(rt);
            Log("[SHOT] " + name + (hud != null ? " (com HUD)" : " (sem HUD)"));
            yield return null;
        }
    }
}
#endif
