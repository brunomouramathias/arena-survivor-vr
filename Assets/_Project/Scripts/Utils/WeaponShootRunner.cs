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

            // medições sempre no balanceamento oficial (independe do modo salvo na tela de abertura)
            ArenaSurvivor.Core.Difficulty.SelectForSession(ArenaSurvivor.Core.GameDifficulty.Medium);

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

            // ── 6) girar o jogador enquanto a bala voa: o tiro continua reto ──
            bool p6 = false;
            yield return StraightWhileTurning(shooter, r => p6 = r);

            // ── 7) tremor de câmera não pode mover a mira ────────────────────
            bool p7 = false;
            yield return SteadyAimWhileShaking(shooter, r => p7 = r);

            bool ok = p1 && p2 && p3 && p4 && p6 && p7 && errors == 0;
            sb.AppendLine($"\nErros/exceções: {errors}");
            sb.AppendLine($"RESULTADO: {(ok ? "PASS" : "FAIL")}");
            Finish();
        }

        // O tremor (shake) do disparo não pode mexer no ponto de mira
        private IEnumerator SteadyAimWhileShaking(PlayerShooter shooter, System.Action<bool> result)
        {
            var cam = Camera.main;
            var cc  = cam != null ? cam.GetComponent<CameraController>() : null;
            if (cam == null || cc == null) { sb.AppendLine("[7] CameraController ausente => FAIL"); result(false); yield break; }

            var planoJogador = shooter.transform.position;
            Vector2 meio = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            Vector3 Mira(Ray r)
            {
                var pl = new Plane(Vector3.up, planoJogador);
                return pl.Raycast(r, out float d) ? r.GetPoint(d) : r.origin;
            }

            yield return null;
            Vector3 antes = Mira(cc.ScreenRay(meio, cam));

            cc.Shake(2.0f);                 // tremor forte, como um disparo
            yield return null;
            yield return null;

            Vector3 depois    = Mira(cc.ScreenRay(meio, cam));
            Vector3 tremida   = Mira(cam.ScreenPointToRay(meio));   // raio da câmera de verdade
            float   desvioMira = Vector3.Distance(antes, depois);
            float   tremorReal = Vector3.Distance(depois, tremida);

            sb.AppendLine($"[7] tremor aplicado={tremorReal:F2} m | mira desviou={desvioMira:F3} m " +
                          $"=> {(desvioMira < 0.05f ? "PASS" : "FAIL")}");

            result(desvioMira < 0.05f);
        }

        // Dispara e GIRA o jogador em seguida: a bala não pode herdar a rotação do corpo.
        private IEnumerator StraightWhileTurning(PlayerShooter shooter, System.Action<bool> result)
        {
            var weapon = shooter.GetWeaponInSlot(0);
            if (weapon == null || !shooter.EquipSlot(0)) { sb.AppendLine("[6] pistola indisponível => FAIL"); result(false); yield break; }

            yield return null;
            yield return null;

            Vector3 aim = shooter.transform.position + shooter.transform.forward * 15f + Vector3.up * 1.2f;
            weapon.TryFire(aim);
            yield return null;

            var pool = GameObject.Find("Projectiles_Pool");

            // balas em voo ficam soltas no mundo (fora de qualquer pai)
            var ativos = FindObjectsByType<ArenaSurvivor.Weapons.Projectile>(FindObjectsInactive.Exclude);

            Vector3 antes = ativos.Length > 0 ? ativos[0].Direction : Vector3.zero;

            // gira o corpo 90° (é o que acontece ao mirar em outra direção)
            shooter.transform.rotation = shooter.transform.rotation * Quaternion.Euler(0f, 90f, 0f);
            yield return null;
            yield return null;

            float desvio = ativos.Length > 0 ? Vector3.Angle(antes, ativos[0].Direction) : -1f;
            bool dentroDoPool = pool != null && pool.transform.parent == null;

            sb.AppendLine($"[6] balas no container próprio={dentroDoPool} direção antes/depois de girar o jogador: desvio={desvio:F2}° " +
                          $"=> {((dentroDoPool && ativos.Length > 0 && desvio < 1f) ? "PASS" : "FAIL")}");

            result(dentroDoPool && ativos.Length > 0 && desvio < 1f);
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
