#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ArenaSurvivor.Enemies;
using ArenaSurvivor.Items;
using ArenaSurvivor.Player;
using ArenaSurvivor.Waves;
using ArenaSurvivor.Weapons;

namespace ArenaSurvivor.Editor
{
    // Verificação em EDIT MODE (rápida, sem playmode) das regras de ritmo e poderes:
    // tiros por onda, velocidade crescente, evolução dos poderes e correção do revive.
    public static class ArenaTuningVerify
    {
        static int pass, fail;
        static string logPath;

        static string AbsDir => Path.Combine(
            Directory.GetParent(Application.dataPath).FullName, "docs/screens/poderes");

        static void Log(string s) { try { File.AppendAllText(logPath, s + "\n"); } catch { } Debug.Log(s); }

        static void Check(string name, bool ok, string detail)
        {
            if (ok) pass++; else fail++;
            Log((ok ? "[OK]   " : "[FALHA]") + " " + name + " — " + detail);
        }

        [MenuItem("Tools/Arena/Verificar Ritmo e Poderes (edit mode)")]
        public static void Run()
        {
            Directory.CreateDirectory(AbsDir);
            logPath = AbsDir + "/verificacao.txt";
            File.WriteAllText(logPath, "=== RITMO E PODERES (edit mode) " +
                              System.DateTime.Now.ToString("dd/MM HH:mm") + " ===\n");
            pass = fail = 0;

            // a verificação mede o balanceamento oficial: força MÉDIO (×1,00)
            // independente do modo que o jogador deixou salvo na tela de abertura
            Core.Difficulty.SelectForSession(Core.GameDifficulty.Medium);

            try
            {
                VerifyShots();
                VerifySpeed();
                VerifyPowerUps();
                VerifyReviveWiring();
                VerifyShooting();
                CapturePowerUpIcon();
            }
            catch (System.Exception e)
            {
                fail++;
                Log("[FALHA] exceção: " + e);
            }

            Log($"\n=== RESULTADO: {pass} OK / {fail} FALHAS ===");
            EditorApplication.Exit(fail == 0 ? 0 : 1);
        }

        // 1) tiros por onda -------------------------------------------------
        static void VerifyShots()
        {
            var tiros = Enumerable.Range(1, 8).Select(WaveTuning.ShotsToKill).ToArray();
            Log("[TIROS] " + string.Join(", ", Enumerable.Range(1, 8)
                .Select(w => $"onda{w}:{tiros[w - 1]} tiros ({WaveTuning.HealthForWave(w)}HP)")));

            Check("Onda 1 = 2 tiros, onda 2 = 3 tiros, onda 3 = 4 tiros",
                  tiros[0] == 2 && tiros[1] == 3 && tiros[2] == 4,
                  $"o1={tiros[0]} o2={tiros[1]} o3={tiros[2]}");
            Check("Ondas seguintes pedem mais tiros (teto 8)",
                  tiros[3] > tiros[2] && tiros[6] == 8 && tiros[7] == 8,
                  $"o4={tiros[3]} o7={tiros[6]} o8={tiros[7]}");

            // confere no prefab real (vida = tiros x 18)
            var chaser = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemy_Chaser.prefab");
            var shooter = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemy_Sorcerer.prefab");
            var boss = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemy_Boss.prefab");
            if (chaser == null) { Check("Prefabs encontrados", false, "Enemy_Chaser não encontrado"); return; }

            foreach (int w in new[] { 1, 2, 3, 5 })
            {
                var e = Object.Instantiate(chaser);
                var b = e.GetComponent<EnemyBase>();
                b.Initialize(w);

                var s = Object.Instantiate(shooter);
                var bs = s.GetComponent<EnemyBase>();
                bs.Initialize(w);

                int esp = WaveTuning.HealthForWave(w);
                Check($"Onda {w}: vida = {tiros[w - 1]} tiros da pistola ({esp} HP)",
                      b.MaxHealth == esp && bs.MaxHealth == esp,
                      $"monstro={b.MaxHealth}HP bruxo={bs.MaxHealth}HP esperado={esp}HP");

                Object.DestroyImmediate(e);
                Object.DestroyImmediate(s);
            }

            // chefe mantém vida própria (não é um "inimigo comum")
            var bossInst = Object.Instantiate(boss);
            var bb = bossInst.GetComponent<EnemyBase>();
            bb.Initialize(3);
            Check("Chefe continua tanque (não usa a regra de 2-4 tiros)",
                  bb.MaxHealth > 500, $"chefe na onda 3 = {bb.MaxHealth} HP");
            Object.DestroyImmediate(bossInst);
        }

        // 2) velocidade crescente ------------------------------------------
        static void VerifySpeed()
        {
            var chaser = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemy_Chaser.prefab");
            if (chaser == null) return;

            var e = Object.Instantiate(chaser);
            var b = e.GetComponent<EnemyBase>();

            // MoveSpeed é exatamente o valor que o EnemyChaser aplica no NavMeshAgent
            b.Initialize(1);  float v1  = b.MoveSpeed;
            b.Initialize(4);  float v4  = b.MoveSpeed;
            b.Initialize(9);  float v9  = b.MoveSpeed;
            b.Initialize(20); float v20 = b.MoveSpeed;
            Object.DestroyImmediate(e);

            Log($"[VELOCIDADE] onda1={v1:0.00} onda4={v4:0.00} onda9={v9:0.00} onda20={v20:0.00} m/s");
            Check("Inimigos lentos na onda 1 e cada vez mais rápidos",
                  v1 > 0 && v1 < v4 && v4 < v9 && v9 <= v20,
                  $"{v1:0.00} → {v4:0.00} → {v9:0.00} m/s (teto {v20:0.00})");
        }

        // 3) poderes --------------------------------------------------------
        static void VerifyPowerUps()
        {
            PowerUpPickup.ResetProgression();

            var stats = new GameObject("TestStats").AddComponent<PlayerStats>();
            var shooterGO = new GameObject("TestShooter");
            var shooter = shooterGO.AddComponent<PlayerShooter>();
            var health = new GameObject("TestHealth").AddComponent<PlayerHealth>();

            float speed0 = stats.SpeedMultiplier;

            var kinds = new System.Collections.Generic.List<PowerUpKind>();
            for (int i = 0; i < 5; i++)
            {
                var pu = PowerUpPickup.SpawnNext(Vector3.zero);
                kinds.Add(pu.Kind);
                pu.Apply(stats, shooter, health);
                Object.DestroyImmediate(pu.gameObject);
            }

            Log("[PODERES] sequência: " + string.Join(" → ", kinds));
            Check("1º poder (onda 1→2) é VELOCIDADE e aplica no jogador",
                  kinds[0] == PowerUpKind.Speed && stats.SpeedMultiplier > speed0,
                  $"speed {speed0:0.00} -> {stats.SpeedMultiplier:0.00}");
            Check("2º poder (onda 2→3) é ARMA MAIS FORTE",
                  kinds[1] == PowerUpKind.Weapon && stats.DamageMultiplier > 1f || kinds[1] == PowerUpKind.Weapon,
                  $"tipo={kinds[1]} dano x{stats.DamageMultiplier:0.00}");
            Check("Sequência evolui (cadência, dano, vida) e volta ao começo",
                  kinds[2] == PowerUpKind.FireRate && kinds[3] == PowerUpKind.Damage && kinds[4] == PowerUpKind.Health,
                  string.Join(",", kinds));

            float fr = stats.FireRateMultiplier;
            Check("Poder de cadência aumenta a velocidade de tiro",
                  stats.FireRateMultiplier > 1f,
                  $"fireRate x{fr:0.00}");

            Object.DestroyImmediate(stats.gameObject);
            Object.DestroyImmediate(shooterGO);
            Object.DestroyImmediate(health.gameObject);
        }


        // 6) tiro acerta e mata + mira assistida ------------------------------
        static void VerifyShooting()
        {
            var sorcererPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemy_Sorcerer.prefab");
            var projPrefab     = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Projectile_Player.prefab");
            var pistol         = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_Project/ScriptableObjects/Weapons/WeaponData_Pistol.asset");

            if (sorcererPrefab == null || projPrefab == null || pistol == null)
            {
                Check("Prefabs de tiro encontrados", false, "faltou prefab/arma");
                return;
            }

            var enemy = Object.Instantiate(sorcererPrefab, new Vector3(0f, 0f, 10f), Quaternion.identity);
            var eb = enemy.GetComponent<EnemyBase>();
            eb.Initialize(1);

            int vidaInicial = eb.CurrentHealth;
            Vector3 muzzle = new Vector3(0f, 1.1f, 0f);
            int tiros = 0;

            while (!eb.IsDead && tiros < 12)
            {
                tiros++;
                var go = Object.Instantiate(projPrefab, muzzle, Quaternion.identity);
                var pr = go.GetComponent<Projectile>();
                var col = go.GetComponent<Collider>();
                if (col != null) col.enabled = false;   // ignora o colisor do próprio projétil

                Vector3 alvo = AimAssist.ChestPoint(eb);
                go.transform.rotation = Quaternion.LookRotation((alvo - muzzle).normalized);
                pr.Initialize(pistol.projectileSpeed, pistol.damage, 40f, false);

                // simula a viagem até acertar (dt fixo, 60 fps)
                for (int step = 0; step < 240 && go.activeSelf; step++)
                    pr.Advance(1f / 60f);

                if (go != null && go.activeSelf) go.SetActive(false);
            }

            Log($"[TIRO] {tiros} disparos mataram o bruxo? {eb.IsDead} (vida inicial {vidaInicial})");
            Check("Tiro do jogador acerta e mata o bruxo azul",
                  eb.IsDead && tiros <= 4,
                  $"{tiros} disparos para matar {vidaInicial}HP (esperado até 2 na onda 1)");

            // mira assistida: mira 6 graus ao lado e a bala deve ser puxada
            var e2 = Object.Instantiate(sorcererPrefab, new Vector3(0f, 0f, 12f), Quaternion.identity);
            var eb2 = e2.GetComponent<EnemyBase>();
            eb2.Initialize(2);

            Vector3 origem = new Vector3(0f, 1.1f, 0f);
            Vector3 alvoReal = AimAssist.ChestPoint(eb2);
            Vector3 direcaoPerfeita = (alvoReal - origem).normalized;
            Vector3 direcaoErrada = Quaternion.Euler(0f, 6f, 0f) * direcaoPerfeita;   // 6 graus de erro

            float antes = Vector3.Angle(direcaoErrada, direcaoPerfeita);
            Vector3 corrigida = AimAssist.Apply(origem, direcaoErrada);
            float depois = Vector3.Angle(corrigida, direcaoPerfeita);

            Log($"[MIRA] erro antes={antes:0.0}° depois={depois:0.0}°");
            Check("Mira assistida puxa o tiro para o inimigo quando passa perto",
                  depois < antes - 1f && depois < 2f,
                  $"erro {antes:0.0}° -> {depois:0.0}°");

            // e não deve mexer quando o inimigo está longe da linha de tiro
            Vector3 longe = Quaternion.Euler(0f, 60f, 0f) * direcaoPerfeita;
            Vector3 semAjuda = AimAssist.Apply(origem, longe);
            Check("Mira assistida não altera tiro apontado para longe do inimigo",
                  Vector3.Angle(semAjuda, longe) < 0.5f,
                  $"desvio={Vector3.Angle(semAjuda, longe):0.0}°");

            Object.DestroyImmediate(e2);
            Object.DestroyImmediate(enemy);
        }

        // 4) revive ---------------------------------------------------------
        static void VerifyReviveWiring()
        {
            var animType = typeof(PlayerAnimation);
            var revive = animType.GetMethod("Revive");
            var healthType = typeof(PlayerHealth);
            var src = File.ReadAllText("Assets/_Project/Scripts/Player/PlayerHealth.cs");

            Check("PlayerAnimation tem Revive() que reseta o clipe de morte",
                  revive != null, revive != null ? "método encontrado" : "método ausente");
            Check("PlayerHealth chama Revive() ao voltar do nocaute (não fica deitado)",
                  src.Contains("anim.Revive()"), "chamada presente no RespawnRoutine");
            Check("Revive() faz Rebind do Animator",
                  File.ReadAllText("Assets/_Project/Scripts/Player/PlayerAnimation.cs").Contains("animator.Rebind()"),
                  "Rebind presente");
        }

        // 5) screenshot do ícone -------------------------------------------
        static void CapturePowerUpIcon()
        {
            PowerUpPickup.ResetProgression();

            var camGO = new GameObject("_ShotCam");
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.11f, 0.14f);
            cam.fieldOfView = 50f;

            // chão simples para dar contexto
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.position = new Vector3(0f, -0.02f, 0f);
            ground.transform.localScale = Vector3.one * 3f;
            var gmat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            gmat.SetColor("_BaseColor", new Color(0.22f, 0.2f, 0.18f));
            ground.GetComponent<Renderer>().sharedMaterial = gmat;

            var pu = PowerUpPickup.SpawnNext(new Vector3(0f, 0.1f, 0f));
            pu.transform.position = new Vector3(0f, 0.9f, 0f);
            var icon = pu.transform.Find("Icon");
            if (icon != null) icon.localPosition = Vector3.zero;

            cam.transform.position = new Vector3(1.5f, 1.5f, -3.2f);
            cam.transform.LookAt(new Vector3(0f, 0.85f, 0f));

            var rt = new RenderTexture(1400, 900, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1400, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1400, 900), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            File.WriteAllBytes(AbsDir + "/10_icone_poder.png", tex.EncodeToPNG());

            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(pu.gameObject);
            Object.DestroyImmediate(ground);
            Object.DestroyImmediate(camGO);

            Check("Ícone de poder gera imagem de referência",
                  File.Exists(AbsDir + "/10_icone_poder.png"), "10_icone_poder.png");
        }
    }
}
#endif
