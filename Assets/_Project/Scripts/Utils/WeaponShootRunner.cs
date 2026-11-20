#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using ArenaSurvivor.Core;
using ArenaSurvivor.Player;
using ArenaSurvivor.Weapons;

namespace ArenaSurvivor.Utils
{
    // Prova que TODAS as armas conseguem atirar depois de desbloqueadas/equipadas
    // (loja e ícone de poder). Regressão do bug "não consigo mais atirar".
    public class WeaponShootRunner : MonoBehaviour
    {
        public static bool AutoRun => SessionState.GetBool("WeaponShoot.AutoRun", false);

        readonly StringBuilder sb = new StringBuilder();
        int errors;

        void Awake() { Application.logMessageReceived += OnLog; }
        void OnDestroy() { Application.logMessageReceived -= OnLog; }
        void OnLog(string c, string s, LogType t)
        {
            if (t == LogType.Error || t == LogType.Exception)
            {
                // exceção interna do editor (índice de busca do Unity) não é erro do jogo
                if (s.Contains("UnityEditor.Search")) return;
                errors++;
                if (errors <= 10) sb.AppendLine($"  [ERRO] {s.Replace("\n", " ")}");
            }
        }

        private IEnumerator Start()
        {
            if (!AutoRun) { enabled = false; yield break; }

            sb.AppendLine("=== TESTE: atirar com todas as armas (pistola / escopeta / energia) ===");
            sb.AppendLine($"Data: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            yield return new WaitForSecondsRealtime(4f);

            var shooter = FindAnyObjectByType<PlayerShooter>();
            if (shooter == null) { sb.AppendLine("PlayerShooter AUSENTE"); Finish(); yield break; }

            var player = shooter.transform;
            Vector3 aim = player.position + player.forward * 12f + Vector3.up * 1.2f;

            // ── 1) pistola (arma inicial) ────────────────────────────────────
            bool p1 = false;
            yield return TryShoot(shooter.CurrentWeapon, aim, "PISTOLA", r => p1 = r);

            // ── 2) caminho da LOJA: compra desbloqueia e o jogador equipa ────
            var shotgun = shooter.GetWeaponInSlot(1);
            sb.AppendLine($"[2] antes: escopeta '{shotgun?.name}' desbloqueada={shotgun?.IsUnlocked} clip={shotgun?.CurrentClip}");
            shooter.UnlockWeapon(1);                       // TryUnlockShotgun faz exatamente isso
            bool equipped = shooter.EquipSlot(1);          // tecla "2"
            yield return null; yield return null;          // Awake/OnEnable da arma desativada
            sb.AppendLine($"[2] equipou={equipped} arma atual='{shooter.CurrentWeapon?.name}' desbloqueada={shooter.CurrentWeapon?.IsUnlocked} clip={shooter.CurrentWeapon?.CurrentClip} reserva={shooter.CurrentWeapon?.CurrentReserve}");

            bool p2 = false;
            yield return TryShoot(shooter.CurrentWeapon, aim, "ESCOPETA", r => p2 = r);

            // ── 3) caminho do PODER: desbloqueia e equipa a próxima ─────────
            bool next = shooter.UnlockAndEquipNext();
            yield return null; yield return null;
            sb.AppendLine($"[3] poder liberou próxima={next} arma atual='{shooter.CurrentWeapon?.name}' desbloqueada={shooter.CurrentWeapon?.IsUnlocked} clip={shooter.CurrentWeapon?.CurrentClip} reserva={shooter.CurrentWeapon?.CurrentReserve}");

            bool p3 = false;
            yield return TryShoot(shooter.CurrentWeapon, aim, "ENERGIA", r => p3 = r);

            // ── 4) trocar de volta para a pistola (teclas 1/2/3) ────────────
            shooter.EquipSlot(0);
            yield return null; yield return null;
            bool p4 = false;
            yield return TryShoot(shooter.CurrentWeapon, aim, "PISTOLA (de volta)", r => p4 = r);

            // ── 5) arma seca volta sozinha para a pistola (nunca fica sem atirar)
            var shot = shooter.GetWeaponInSlot(1);
            if (shot != null)
            {
                shooter.EquipSlot(1);
                yield return null;
                shot.DebugDrainAmmo();
                bool dry = shot.IsDry;
                yield return new WaitForSecondsRealtime(1.2f);   // PlayerShooter.Update deve trocar
                bool recovered = shooter.CurrentWeapon != null && shooter.CurrentWeapon.IsUnlocked &&
                                 !shooter.CurrentWeapon.IsDry && shooter.CurrentWeapon != shot;
                sb.AppendLine($"[5] escopeta seca={dry} -> arma atual='{shooter.CurrentWeapon?.name}' recuperou={recovered} => {(dry && recovered ? "PASS" : "FAIL")}");
                if (!(dry && recovered)) p4 = false;
            }

            bool ok = p1 && p2 && p3 && p4 && errors == 0;
            sb.AppendLine($"\nErros/exceções: {errors}");
            sb.AppendLine($"RESULTADO: {(ok ? "PASS" : "FAIL")}");
            Finish();
        }

        // Dispara e confere que o pente diminuiu (ou seja: Fire() rodou)
        private IEnumerator TryShoot(Weapon w, Vector3 aim, string tag, System.Action<bool> result)
        {
            if (w == null) { sb.AppendLine($"[{tag}] arma NULA => FAIL"); result(false); yield break; }

            int before = w.CurrentClip;
            w.TryFire(aim);
            yield return new WaitForSecondsRealtime(0.5f);
            int after = w.CurrentClip;
            bool fired = after < before;
            sb.AppendLine($"[{tag}] '{w.name}' desbloqueada={w.IsUnlocked} pente {before}->{after} atirou={fired} => {(fired ? "PASS" : "FAIL")}");
            result(fired);
        }

        private void Finish()
        {
            Directory.CreateDirectory("docs/screens/armas");
            File.WriteAllText("docs/screens/armas/tiro_armas.txt", sb.ToString(), Encoding.UTF8);
            Debug.Log(sb.ToString());
            SessionState.SetBool("WeaponShoot.AutoRun", false);
            EditorApplication.Exit(0);
        }
    }
}
#endif
