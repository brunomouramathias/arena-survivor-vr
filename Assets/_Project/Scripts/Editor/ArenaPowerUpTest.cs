#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ArenaSurvivor.Core;
using ArenaSurvivor.Enemies;
using ArenaSurvivor.Items;
using ArenaSurvivor.Player;
using ArenaSurvivor.Waves;

namespace ArenaSurvivor.Editor
{
    // Valida: tiros por onda (2,3,4...), velocidade crescente, revive sem ficar deitado
    // e o ícone de poder que brota ao limpar a onda.
    public static class ArenaPowerUpTest
    {
        [MenuItem("Tools/Arena/Testar Poderes e Ritmo (checklist 2)")]
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
            if (Object.FindFirstObjectByType<PowerUpTestRunner>() != null) return;
            var go = new GameObject("PowerUpTestRunner");
            go.AddComponent<PowerUpTestRunner>();
        }
    }

    public class PowerUpTestRunner : MonoBehaviour
    {
        const string DIR = "docs/screens/poderes";
        // pasta de saida absoluta (o CWD muda entre batchmode e editor grafico)
        static string AbsDir => Path.Combine(
            Directory.GetParent(Application.dataPath).FullName, "docs/screens/poderes");

        string logPath;
        Camera cam;
        int pass, fail;

        void Awake()
        {
            Directory.CreateDirectory(AbsDir);
            logPath = AbsDir + "/relatorio.txt";
            File.WriteAllText(logPath, "=== CHECKLIST 2 (poderes / ritmo) " +
                              System.DateTime.Now.ToString("dd/MM HH:mm") + " ===\n");
        }

        void Log(string s) { try { File.AppendAllText(logPath, s + "\n"); } catch { } Debug.Log(s); }

        void Check(string name, bool ok, string detail)
        {
            if (ok) pass++; else fail++;
            Log((ok ? "[OK]   " : "[FALHA]") + " " + name + " — " + detail);
        }

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

            var chaser = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemy_Chaser.prefab");

            // ---------------------------------------------------------------- 1) tiros por onda
            var tiros = new int[9];
            var vidas = new int[9];
            for (int w = 1; w <= 8; w++)
            {
                tiros[w] = WaveTuning.ShotsToKill(w);
                vidas[w] = WaveTuning.HealthForWave(w);
            }
            Log("[TIROS] " + string.Join(", ", Enumerable.Range(1, 8).Select(w => $"onda{w}:{tiros[w]} tiros ({vidas[w]}HP)")));
            Check("Onda 1 = 2 tiros, onda 2 = 3, onda 3 = 4",
                  tiros[1] == 2 && tiros[2] == 3 && tiros[3] == 4,
                  $"tiros: o1={tiros[1]} o2={tiros[2]} o3={tiros[3]}");
            Check("Cada onda seguinte pede mais tiros (até 8)",
                  tiros[4] > tiros[3] && tiros[8] == 8,
                  $"o4={tiros[4]} o6={tiros[6]} o8={tiros[8]}");

            // confere na prática: onda 1 morre em 2 tiros, não em 1; onda 2 em 3
            foreach (int w in new[] { 1, 2, 3 })
            {
                var e1 = Instantiate(chaser, new Vector3(-30f + w, 0f, 30f), Quaternion.identity);
                yield return null;
                var b1 = e1.GetComponent<EnemyBase>();
                b1.Initialize(w);

                int hits = 0;
                while (!b1.IsDead && hits < 12) { hits++; b1.TakeDamage(WaveTuning.PistolDamage); }

                Check($"Onda {w}: inimigo cai em exatamente {tiros[w]} tiros ({vidas[w]} HP)",
                      hits == tiros[w] && b1.MaxHealth == vidas[w],
                      $"vida={b1.MaxHealth}HP tiros={hits}");

                e1.SetActive(false);
            }

            // ---------------------------------------------------------------- 2) velocidade crescente
            var e2 = Instantiate(chaser, new Vector3(-30f, 0f, 32f), Quaternion.identity);
            yield return null;
            var b2 = e2.GetComponent<EnemyBase>();
            var agent2 = e2.GetComponent<UnityEngine.AI.NavMeshAgent>();

            b2.Initialize(1);
            float velOnda1 = agent2 != null ? agent2.speed : 0f;
            b2.Initialize(6);
            float velOnda6 = agent2 != null ? agent2.speed : 0f;
            b2.Initialize(12);
            float velOnda12 = agent2 != null ? agent2.speed : 0f;
            e2.SetActive(false);

            Check("Inimigos lentos na onda 1 e mais rápidos depois",
                  velOnda1 > 0f && velOnda1 < velOnda6 && velOnda6 < velOnda12,
                  $"onda1={velOnda1:0.00} m/s | onda6={velOnda6:0.00} m/s | onda12={velOnda12:0.00} m/s");

            // ---------------------------------------------------------------- 3) poderes (explícitos)
            var player = Object.FindFirstObjectByType<PlayerController>();
            var stats  = Object.FindFirstObjectByType<PlayerStats>();
            var ph     = Object.FindFirstObjectByType<PlayerHealth>();
            var shooter = Object.FindFirstObjectByType<PlayerShooter>();

            // longe do jogador para o ímã não coletar antes da hora
            Vector3 basePos = player != null ? player.transform.position + new Vector3(7f, 0f, 7f)
                                             : new Vector3(8f, 0f, 8f);

            // ícone visual
            var puIcon = PowerUpPickup.Spawn(PowerUpKind.Speed, 0, basePos);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("Ícone de poder é criado e fica ativo no chão",
                  puIcon != null && puIcon.IsBuiltIcon && PowerUpPickup.Active >= 1,
                  $"tipo={puIcon?.Kind} ativos={PowerUpPickup.Active}");

            yield return Shot("10_icone_poder.png", basePos + new Vector3(1.5f, 1.3f, -2.6f), basePos + Vector3.up * 0.9f);

            // 1º poder: velocidade
            float speedAntes = stats != null ? stats.SpeedMultiplier : 1f;
            puIcon.Collect();
            yield return null;
            Check("Poder de VELOCIDADE deixa o jogador mais rápido",
                  stats != null && stats.SpeedMultiplier > speedAntes,
                  stats != null ? $"speed {speedAntes:0.00} -> {stats.SpeedMultiplier:0.00} " +
                                  $"({player.EffectiveMoveSpeed:0.0} m/s em jogo)" : "sem PlayerStats");

            // 2º poder: arma mais forte
            int armasAntes = shooter != null ? ContarArmas(shooter) : 0;
            float danoAntes = stats != null ? stats.DamageMultiplier : 1f;
            var puWeapon = PowerUpPickup.Spawn(PowerUpKind.Weapon, 0,
                player != null ? player.transform.position + new Vector3(-7f, 0f, 7f) : new Vector3(-8f, 0f, 8f));
            yield return new WaitForSecondsRealtime(0.2f);
            // aplica direto (o Collect já foi validado no poder de velocidade)
            puWeapon.Apply(stats, shooter, ph);
            yield return null;

            int armasDepois = shooter != null ? ContarArmas(shooter) : 0;
            var slotsTxt = new System.Text.StringBuilder();
            if (shooter != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    var w = shooter.GetWeaponInSlot(i);
                    slotsTxt.Append(w == null ? "null" :
                        (w.Data != null ? w.Data.weaponName : "?") + (w.IsUnlocked ? "(on)" : "(off)"));
                    if (i < 2) slotsTxt.Append(",");
                }
            }
            Log($"[ARMA] shooter={shooter != null} stats={stats != null} slots={slotsTxt}");
            Check("Poder de ARMA MAIS FORTE libera arma nova ou aumenta o dano",
                  stats != null && (armasDepois > armasAntes || stats.DamageMultiplier > danoAntes),
                  $"armas {armasAntes}->{armasDepois} | dano x{danoAntes:0.00}->x{(stats != null ? stats.DamageMultiplier : 1f):0.00}");

            // 3) o poder da ONDA: mata todo mundo e vê o ícone brotar sozinho
            float spd0 = stats != null ? stats.SpeedMultiplier : 1f;
            float dmg0 = stats != null ? stats.DamageMultiplier : 1f;
            float fr0  = stats != null ? stats.FireRateMultiplier : 1f;

            // garante o jogo rodando (pode estar na loja com timeScale 0)
            GameManager.Instance?.SetState(GameState.Playing);
            var wm = WaveManager.Instance;
            if (wm != null) wm.DebugSkipToWave(2);

            float nasc = 0f;
            while (nasc < 14f && Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None)
                                       .Count(e => e != null && !e.IsDead) == 0)
            {
                nasc += Time.unscaledDeltaTime;
                yield return null;
            }

            var vivos = Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None).Where(e => !e.IsDead).ToArray();
            Log($"[ONDA] vivos antes de limpar = {vivos.Length} (onda {wm?.CurrentWave}, estado {GameManager.Instance?.CurrentState}, " +
                $"espera={nasc:0.0}s)");
            foreach (var e in vivos) e.TakeDamage(99999);

            float espera = 0f;
            bool apareceu = false;
            while (espera < 10f)
            {
                espera += Time.unscaledDeltaTime;
                if (PowerUpPickup.Active > 0) { apareceu = true; break; }
                if (stats != null && (stats.SpeedMultiplier > spd0 || stats.DamageMultiplier > dmg0 ||
                                      stats.FireRateMultiplier > fr0)) { apareceu = true; break; }
                yield return null;
            }

            var daOnda = Object.FindObjectsByType<PowerUpPickup>(FindObjectsSortMode.None)
                               .FirstOrDefault(p => p != null && !p.Collected);
            Check("Ao limpar a onda o jogo entrega um ícone de poder",
                  apareceu || daOnda != null,
                  $"ativos={PowerUpPickup.Active} espera={espera:0.0}s" +
                  (daOnda != null ? $" tipo={daOnda.Kind}" : "") +
                  (stats != null ? $" | speed {spd0:0.00}->{stats.SpeedMultiplier:0.00} dano {dmg0:0.00}->{stats.DamageMultiplier:0.00}" : ""));

            if (daOnda != null)
            {
                var dp = daOnda.transform.position;
                yield return Shot("11_poder_da_onda.png", dp + new Vector3(1.8f, 1.4f, -3.0f), dp + Vector3.up * 0.6f);
            }

            // ---------------------------------------------------------------- 4) revive sem ficar deitado
            var anim = ph != null ? ph.GetComponent<PlayerAnimation>() : null;

            if (ph != null)
            {
                ph.Heal(9999);
                int golpes = 0;
                while (!ph.IsDead && golpes < 8)
                {
                    golpes++;
                    ph.SetInvincible(false);
                    ph.TakeDamage(1);
                    yield return new WaitForSecondsRealtime(1.0f);
                }
                Log($"[MORTE] golpes={golpes} morto={ph.IsDead}");

                yield return new WaitForSecondsRealtime(2.4f);
                Check("Jogador volta de pé depois do nocaute (animação de morte resetada)",
                      !ph.IsDead && anim != null && anim.ReviveCount > 0,
                      $"revives={anim?.ReviveCount} vida={ph.CurrentHealth}/{ph.MaxHealth}");

                if (anim != null)
                {
                    var a = ph.GetComponent<Animator>();
                    string clipe = "?";
                    if (a != null && a.GetCurrentAnimatorClipInfo(0).Length > 0)
                        clipe = a.GetCurrentAnimatorClipInfo(0)[0].clip.name;
                    Log($"[ANIM] clipe atual depois do revive = {clipe}");
                    Check("Animator não está mais no clipe de morte",
                          !clipe.ToLower().Contains("death") && !clipe.ToLower().Contains("die"),
                          "clipe=" + clipe);
                }

                var ppos = ph.transform.position;
                yield return Shot("11_jogador_de_pe.png", ppos + new Vector3(2.6f, 1.9f, -3.4f), ppos + Vector3.up * 1.1f);
            }

            Log($"\n=== RESULTADO: {pass} OK / {fail} FALHAS ===");
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += () => EditorApplication.Exit(fail == 0 ? 0 : 1);
        }

        static int ContarArmas(PlayerShooter s)
        {
            int n = 0;
            for (int i = 0; i < 3; i++)
                if (s.GetWeaponInSlot(i) != null && s.GetWeaponInSlot(i).IsUnlocked) n++;
            return n;
        }

        IEnumerator Shot(string name, Vector3 camPos, Vector3 lookAt)
        {
            int w = 1600, h = 900;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.transform.position = camPos;
            cam.transform.LookAt(lookAt);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            File.WriteAllBytes(AbsDir + "/" + name, tex.EncodeToPNG());
            Destroy(tex); Destroy(rt);
            Log("[SHOT] " + name);
            yield return null;
        }
    }
}
#endif
