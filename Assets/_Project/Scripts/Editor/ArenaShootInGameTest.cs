#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ArenaSurvivor.Enemies;
using ArenaSurvivor.Player;
using ArenaSurvivor.Weapons;

namespace ArenaSurvivor.Editor
{
    // Teste em jogo: a ARMA real do jogador acerta e mata os bruxos?
    public static class ArenaShootInGameTest
    {
        [MenuItem("Tools/Arena/Testar Tiro nos Inimigos (em jogo)")]
        public static void Run()
        {
            try { if (Directory.Exists("Temp/__Backupscenes")) Directory.Delete("Temp/__Backupscenes", true); } catch { }

            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Arena.unity", OpenSceneMode.Single);

            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;

            SpawnRunner();
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            SpawnRunner();
        }

        static void SpawnRunner()
        {
            if (Object.FindFirstObjectByType<ShootRunner>() != null) return;
            var go = new GameObject("ShootRunner");
            go.AddComponent<ShootRunner>();
        }
    }

    public class ShootRunner : MonoBehaviour
    {
        static string AbsDir => Path.Combine(
            Directory.GetParent(Application.dataPath).FullName, "docs/screens/tiro");

        string logPath;
        int pass, fail;
        bool done;

        void Awake()
        {
            Directory.CreateDirectory(AbsDir);
            logPath = AbsDir + "/relatorio.txt";
            File.WriteAllText(logPath, "=== TIRO NOS INIMIGOS (em jogo) " +
                              System.DateTime.Now.ToString("dd/MM HH:mm") + " ===\n");
        }

        void Log(string s) { try { File.AppendAllText(logPath, s + "\n"); } catch { } Debug.Log(s); }

        void Check(string name, bool ok, string detail)
        {
            if (ok) pass++; else fail++;
            Log((ok ? "[OK]   " : "[FALHA]") + " " + name + " — " + detail);
        }

        void Start() => StartCoroutine(RunTest());

        IEnumerator RunTest()
        {
            yield return new WaitForSecondsRealtime(1.0f);

            var player = GameObject.FindGameObjectWithTag("Player");
            var shooter = player != null ? player.GetComponent<PlayerShooter>() : null;
            var weapon = shooter != null ? shooter.CurrentWeapon : null;
            Log($"[INIT] player={player != null} shooter={shooter != null} arma={(weapon != null ? weapon.Data.weaponName : "nenhuma")}");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemy_Sorcerer.prefab");
            if (player == null || weapon == null || prefab == null)
            {
                Check("Pré-requisitos (jogador + arma + prefab do bruxo)", false, "faltou algo");
                Finish();
                yield break;
            }

            // coloca o bruxo a 9 m na frente do jogador
            Vector3 fwd = player.transform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 spawn = player.transform.position + fwd * 9f;
            spawn.y = 0.05f;

            var enemy = Instantiate(prefab, spawn, Quaternion.identity);
            var eb = enemy.GetComponent<EnemyBase>();
            eb.Initialize(1);
            yield return null;

            Log($"[ALVO] bruxo em {spawn} (vida {eb.CurrentHealth}HP) | jogador em {player.transform.position}");

            int tiros = 0;
            int vidaAnterior = eb.CurrentHealth;

            while (!eb.IsDead && tiros < 12)
            {
                tiros++;
                Vector3 mira = enemy.transform.position + Vector3.up * 1.15f;
                weapon.TryFire(mira);

                // espera o projétil chegar
                float t = 0f;
                while (t < 0.6f) { t += Time.deltaTime; yield return null; }

                Log($"[TIRO {tiros}] vida do bruxo = {eb.CurrentHealth}HP (antes {vidaAnterior}HP)" +
                    (eb.CurrentHealth < vidaAnterior ? "  <-- acertou" : "  <-- NÃO acertou"));
                if (eb.CurrentHealth == vidaAnterior)
                {
                    // tenta de novo uma vez, caso a bala tenha saído torta
                    yield return null;
                }
                vidaAnterior = eb.CurrentHealth;
            }

            Check("Tiro da arma real acerta o bruxo",
                  eb.CurrentHealth < eb.MaxHealth,
                  $"vida {eb.MaxHealth} -> {eb.CurrentHealth} em {tiros} tiros");
            Check("Bruxo morre com poucos tiros",
                  eb.IsDead, $"morto={eb.IsDead} em {tiros} tiros");

            Finish();
        }

        void Finish()
        {
            Log($"\n=== RESULTADO: {pass} OK / {fail} FALHAS ===");
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += () => EditorApplication.Exit(fail == 0 ? 0 : 1);
        }
    }
}
#endif
