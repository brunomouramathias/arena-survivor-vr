#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using ArenaSurvivor.Audio;
using ArenaSurvivor.Player;

namespace ArenaSurvivor.Utils
{
    // Confere os dois sons de combate: existem, são curtos, estão baixinhos e tocam na hora certa
    // (dano quando a vida cai; morte no nocaute) — sem tocar ao curar.
    public class PlayerSfxProbeRunner : MonoBehaviour
    {
        public static bool AutoRun => SessionState.GetBool("PlayerSfxProbe.AutoRun", false);

        readonly StringBuilder sb = new StringBuilder();
        bool finished;

        private IEnumerator Start()
        {
            if (!AutoRun) { enabled = false; yield break; }

            StartCoroutine(Watchdog(90f));
            yield return new WaitForSecondsRealtime(1.2f);

            sb.AppendLine("=== SONS DE DANO E MORTE ===");
            sb.AppendLine($"Data: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

            var audio = FindAnyObjectByType<AudioManager>();
            if (audio == null) { Fail("AudioManager não encontrado na cena Arena"); yield break; }

            var damage = audio.PlayerDamageClip;
            var death  = audio.PlayerDeathClip;

            // 1) clipes ligados na cena -----------------------------------------
            bool clipsOk = damage != null && death != null && damage.length > 0.1f && death.length > 0.1f;
            sb.AppendLine($"[1] clipes: dano={Name(damage)} ({Length(damage)}) morte={Name(death)} ({Length(death)}) " +
                          $"=> {(clipsOk ? "PASS" : "FAIL")}");
            if (!clipsOk) { Finish(); yield break; }

            // 2) nível do arquivo (áudio discreto, sem estourar) -----------------
            float peakDamageDb = PeakDb(damage);
            float peakDeathDb  = PeakDb(death);
            bool levelOk = peakDamageDb <= -3f && peakDeathDb <= -3f &&
                            peakDamageDb > -100f && peakDeathDb > -100f;
            sb.AppendLine($"[2] pico dos arquivos: dano={peakDamageDb:F1} dBFS morte={peakDeathDb:F1} dBFS " +
                          $"(limite -3 dBFS) => {(levelOk ? "PASS" : "FAIL")}");

            // 3) dano: toca com volume baixo ------------------------------------
            var health = FindAnyObjectByType<PlayerHealth>();
            if (health == null) { Fail("PlayerHealth não encontrado"); yield break; }

            health.SetInvincible(false);
            audio.ClearLastUISound();
            health.TakeDamage(1);
            yield return new WaitForSecondsRealtime(0.25f);

            bool damageOk = audio.LastUISound == damage && audio.LastUISoundVolume <= 0.6f && audio.LastUISoundVolume > 0f;
            sb.AppendLine($"[3] dano: clip='{Name(audio.LastUISound)}' volume={audio.LastUISoundVolume:F2} " +
                          $"(esperado até 0,60) => {(damageOk ? "PASS" : "FAIL")}");

            // 3b) curar NÃO toca o som de dano ----------------------------------
            yield return new WaitForSecondsRealtime(0.5f);
            audio.ClearLastUISound();
            health.Heal(1);
            yield return new WaitForSecondsRealtime(0.2f);
            bool healSilent = audio.LastUISound == null;
            sb.AppendLine($"[3b] curar não faz barulho: {healSilent} => {(healSilent ? "PASS" : "FAIL")}");

            // 4) morte: toca o outro clipe, também baixinho ---------------------
            yield return new WaitForSecondsRealtime(1.0f);
            for (int i = 0; i < 4 && health.CurrentHealth > 0; i++)
            {
                health.SetInvincible(false);
                health.TakeDamage(1);
                yield return new WaitForSecondsRealtime(0.1f);
            }
            yield return new WaitForSecondsRealtime(0.2f);

            bool deathOk = audio.LastUISound == death && audio.LastUISoundVolume <= 0.6f && audio.LastUISoundVolume > 0f;
            sb.AppendLine($"[4] morte: clip='{Name(audio.LastUISound)}' volume={audio.LastUISoundVolume:F2} " +
                          $"(esperado até 0,60) => {(deathOk ? "PASS" : "FAIL")}");

            bool pass = clipsOk && levelOk && damageOk && healSilent && deathOk;
            sb.AppendLine($"\n=== RESULTADO: {(pass ? "PASS" : "FAIL")} ===");
            Finish();
        }

        // ── helpers ─────────────────────────────────────────────────────────
        private static string Name(AudioClip c) => c != null ? c.name : "<null>";
        private static string Length(AudioClip c) => c != null ? $"{c.length:F2}s" : "-";

        private static float PeakDb(AudioClip clip)
        {
            if (clip == null) return -999f;
            var data = new float[clip.samples * clip.channels];
            if (!clip.GetData(data, 0)) return -999f;

            float peak = 0f;
            foreach (var s in data) peak = Mathf.Max(peak, Mathf.Abs(s));
            return peak > 0f ? 20f * Mathf.Log10(peak) : -999f;
        }

        private void Fail(string why) { sb.AppendLine($"[ERRO] {why}\n\n=== RESULTADO: FAIL ==="); Finish(); }

        private void Finish()
        {
            if (finished) return;
            finished = true;

            Directory.CreateDirectory("docs/screens/audio");
            File.WriteAllText("docs/screens/audio/sons.txt", sb.ToString(), Encoding.UTF8);
            Debug.Log("[SONS] " + sb);
            SessionState.SetBool("PlayerSfxProbe.AutoRun", false);
            EditorApplication.Exit(0);
        }

        private IEnumerator Watchdog(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (!finished) Fail("timeout");
        }
    }
}
#endif
