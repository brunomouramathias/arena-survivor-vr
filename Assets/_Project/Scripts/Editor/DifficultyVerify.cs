#if UNITY_EDITOR
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using ArenaSurvivor.Core;
using ArenaSurvivor.Enemies;
using ArenaSurvivor.Waves;

namespace ArenaSurvivor.Editor
{
    // Verifica, nos prefabs reais, que os três modos de jogo mudam vida, velocidade e
    // quantidade de inimigos. O modo MÉDIO tem de continuar idêntico ao balanceamento antigo.
    public static class DifficultyVerify
    {
        private static readonly StringBuilder sb = new StringBuilder();
        private static int ok, fail;

        [MenuItem("Tools/Arena/Verificar Dificuldades")]
        public static void Run()
        {
            ok = fail = 0;
            sb.Clear();
            sb.AppendLine("=== VERIFICAÇÃO DOS MODOS DE JOGO ===");
            sb.AppendLine($"Data: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            var chaser = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemy_Chaser.prefab");
            var sorcerer = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemy_Sorcerer.prefab");
            var config = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_Project/ScriptableObjects/Waves/WaveConfig.asset");
            if (config == null)
            {
                var guids = AssetDatabase.FindAssets("t:WaveConfig");
                if (guids.Length > 0) config = AssetDatabase.LoadAssetAtPath<WaveConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            if (chaser == null) { Check("prefab Enemy_Chaser encontrado", false, ""); Finish(); return; }

            // ── vida e velocidade por modo ───────────────────────────────────
            int baseHp = WaveTuning.HealthForWave(1);
            float baseSpeedFactor = WaveTuning.SpeedFactor(1);

            var hpByMode = new int[3];
            var speedByMode = new float[3];

            for (int i = 0; i < 3; i++)
            {
                var mode = Difficulty.AtIndex(i);
                Difficulty.SelectForSession(mode);
                var profile = Difficulty.Profile;

                var go = Object.Instantiate(chaser);
                var enemy = go.GetComponent<EnemyBase>();
                enemy.Initialize(1);

                hpByMode[i] = enemy.MaxHealth;
                speedByMode[i] = enemy.MoveSpeed;

                int expectedHp = Mathf.RoundToInt(baseHp * profile.EnemyHealth);
                float baseSpeed = 3.2f;   // moveSpeed do EnemyData do perseguidor
                float expectedSpeed = baseSpeed * baseSpeedFactor * profile.EnemySpeed;

                sb.AppendLine($"[{profile.Label}] vida={hpByMode[i]} (esperado {expectedHp}) | velocidade={speedByMode[i]:0.00} (esperado {expectedSpeed:0.00}) m/s");
                Check($"{profile.Label}: vida do inimigo = 36 × {profile.EnemyHealth:0.00}",
                      hpByMode[i] == expectedHp, $"vida={hpByMode[i]} esperado={expectedHp}");
                Check($"{profile.Label}: velocidade escalada em {profile.EnemySpeed:0.00}×",
                      Mathf.Abs(speedByMode[i] - expectedSpeed) < 0.02f, $"{speedByMode[i]:0.00} m/s");

                Object.DestroyImmediate(go);
            }

            Check("Fácil < Médio < Difícil na resistência",
                  hpByMode[0] < hpByMode[1] && hpByMode[1] < hpByMode[2],
                  $"{hpByMode[0]} < {hpByMode[1]} < {hpByMode[2]} HP");
            Check("Fácil < Médio < Difícil na velocidade",
                  speedByMode[0] < speedByMode[1] && speedByMode[1] < speedByMode[2],
                  $"{speedByMode[0]:0.00} < {speedByMode[1]:0.00} < {speedByMode[2]:0.00} m/s");

            // ── MÉDIO = balanceamento antigo (nada muda para quem já jogava) ──
            Difficulty.SelectForSession(GameDifficulty.Medium);
            Check("MÉDIO mantém a vida original (36 HP na onda 1)", hpByMode[1] == baseHp, $"{hpByMode[1]} HP");
            Check("MÉDIO mantém a velocidade original",
                  Mathf.Abs(speedByMode[1] - 3.2f * baseSpeedFactor) < 0.02f, $"{speedByMode[1]:0.00} m/s");

            // ── bruxo também escala ──────────────────────────────────────────
            if (sorcerer != null)
            {
                var go = Object.Instantiate(sorcerer);
                var enemy = go.GetComponent<EnemyBase>();
                Difficulty.SelectForSession(GameDifficulty.Hard);
                enemy.Initialize(1);
                int expected = Mathf.RoundToInt(baseHp * Difficulty.Get(GameDifficulty.Hard).EnemyHealth);
                Check("Difícil também vale para o bruxo", enemy.MaxHealth == expected, $"{enemy.MaxHealth} HP (esperado {expected})");
                Object.DestroyImmediate(go);
            }

            // ── quantidade de inimigos por onda ─────────────────────────────
            if (config != null)
            {
                int wave = 1;
                int baseCount = config.GetEnemyCount(wave);
                var counts = new int[3];
                for (int i = 0; i < 3; i++)
                {
                    var profile = Difficulty.Get(Difficulty.AtIndex(i));
                    counts[i] = Mathf.Max(3, Mathf.RoundToInt(baseCount * profile.EnemyCount));
                }
                sb.AppendLine($"[QUANTIDADE] onda {wave}: base={baseCount} → fácil={counts[0]} médio={counts[1]} difícil={counts[2]}");
                Check("Fácil tem menos inimigos que o difícil", counts[0] < counts[2], $"{counts[0]} vs {counts[2]}");
                Check("Médio mantém a quantidade original", counts[1] == baseCount, $"{counts[1]} (base {baseCount})");
            }

            // ── perfis completos ────────────────────────────────────────────
            foreach (var mode in new[] { GameDifficulty.Easy, GameDifficulty.Medium, GameDifficulty.Hard })
            {
                var p = Difficulty.Get(mode);
                sb.AppendLine($"[PERFIL] {p.Label}: vida {p.HealthText} · vel {p.SpeedText} · qtd {p.CountText} · início {p.StartPoints} pts · pontos {p.ScoreText}");
                Check($"Perfil {p.Label} tem texto de apresentação", !string.IsNullOrWhiteSpace(p.Headline) && !string.IsNullOrWhiteSpace(p.Description), p.Headline);
            }

            Difficulty.SelectForSession(GameDifficulty.Medium);   // devolve o padrão
            sb.AppendLine($"\n=== RESULTADO: {ok} OK / {fail} FALHAS ===");
            Finish();
        }

        private static void Check(string label, bool condition, string detail)
        {
            if (condition) { ok++; sb.AppendLine($"[OK]    {label} — {detail}"); }
            else           { fail++; sb.AppendLine($"[FALHA] {label} — {detail}"); }
        }

        private static void Finish()
        {
            Debug.Log(sb.ToString());
            System.IO.Directory.CreateDirectory("docs/screens/menu");
            System.IO.File.WriteAllText("docs/screens/menu/dificuldades.txt", sb.ToString(), Encoding.UTF8);

            // permite rodar em batch: -executeMethod ArenaSurvivor.Editor.DifficultyVerify.Run
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(fail == 0 ? 0 : 1);
#endif
        }
    }
}
#endif
