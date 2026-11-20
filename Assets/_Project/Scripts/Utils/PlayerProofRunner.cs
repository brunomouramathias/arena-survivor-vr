#if UNITY_EDITOR   // harness de prova: existe apenas no Editor
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using ArenaSurvivor.Player;
using ArenaSurvivor.Weapons;

namespace ArenaSurvivor.Utils
{
    // Harness temporário de provas: roda em Play Mode e grava capturas + números.
    public class PlayerProofRunner : MonoBehaviour
    {
        // Só executa quando o teste é disparado por linha de comando (-executeMethod).
        // Com Play normal (humano), o harness fica inerte: não atira, não move e NUNCA fecha o Editor.
        // SessionState sobrevive ao reload de domínio ao entrar em Play Mode
        public static bool AutoRun => UnityEditor.SessionState.GetBool("Proof.AutoRun", false);

        private const string SCREEN_DIR = "docs/screens/personagem";
        private const string REPORT     = "docs/prova_final_player.txt";

        private GameObject player;
        private Animator animator;
        private Camera cam;
        private Transform muzzle;
        private Weapon weapon;
        private readonly StringBuilder sb = new StringBuilder();
        private readonly List<string> captured = new List<string>();
        private int errorCount;
        private int warnCount;

        private void Awake()
        {
            Application.logMessageReceived += OnLog;
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errorCount++;
            if (type == LogType.Warning) warnCount++;
        }

        private IEnumerator Start()
        {
            if (!AutoRun)
            {
                Debug.LogWarning("[PROOF] PlayerProofRunner desativado (AutoRun=false). Cena de teste sem efeito.");
                enabled = false;
                yield break;
            }

            Directory.CreateDirectory(SCREEN_DIR);
            Debug.Log("[PROOF] Runner iniciado.");
            sb.AppendLine("==================================================");
            sb.AppendLine("      PROVA FINAL DO JOGADOR (PLAY MODE)          ");
            sb.AppendLine("==================================================");
            sb.AppendLine($"Data/Hora: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) { Finish("Player não encontrado"); yield break; }

            animator = player.GetComponent<Animator>();
            foreach (var w in player.GetComponentsInChildren<Weapon>(true))
            {
                if (w.gameObject.activeInHierarchy) { weapon = w; break; }
            }
            if (weapon == null) weapon = player.GetComponentInChildren<Weapon>(true);
            if (weapon != null)
                muzzle = FindDeep(weapon.transform, "Muzzle") ?? FindDeep(weapon.transform, "ShootPoint");
            if (muzzle == null)
                muzzle = FindDeep(player.transform, "Muzzle") ?? FindDeep(player.transform, "ShootPoint");

            // Remove interferência de inputs reais durante a prova
            var pc = player.GetComponent<PlayerController>();
            var pa = player.GetComponent<PlayerAnimation>();
            var ps = player.GetComponent<PlayerShooter>();
            if (pc) pc.enabled = false;
            if (pa) pa.enabled = false;
            if (ps) ps.enabled = false;

            cam = Camera.main;
            if (cam == null)
            {
                var camGO = new GameObject("ProofCamera");
                cam = camGO.AddComponent<Camera>();
            }
            cam.fieldOfView = 42f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.13f, 0.16f);

            // ---------- Números de skin / avatar ----------
            Avatar av = animator.avatar;
            sb.AppendLine("\n[SKIN / RIG]");
            sb.AppendLine($"Avatar: {av.name} | isValid={av.isValid} | isHuman={av.isHuman}");
            foreach (var smr in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                sb.AppendLine($"  SMR {smr.name}: verts={smr.sharedMesh.vertexCount} bones={smr.bones.Length} bindposes={smr.sharedMesh.bindposes.Length} boneWeights={(smr.sharedMesh.boneWeights.Length > 0 ? "SIM" : "NÃO")} rootBone={smr.rootBone?.name}");
                foreach (var m in smr.sharedMaterials)
                    sb.AppendLine($"    material='{m.name}' textura={(m.mainTexture != null ? m.mainTexture.name : "NENHUMA")}");
            }
            sb.AppendLine($"  Altura (bounds): {MeasureHeight():F2} m");
            sb.AppendLine($"  Weapon: {(weapon != null ? weapon.name : "NULO")} | Muzzle: {(muzzle != null ? muzzle.name : "NULO")}");

            // ---------- Teste de esticamento nos clipes finais ----------
            sb.AppendLine("\n[TESTE DE ESTICAMENTO - LIMITE 3%]");
            animator.enabled = false;
            foreach (var clip in FinalClips())
            {
                sb.AppendLine("  " + StretchReport(player, clip));
            }
            animator.enabled = true;
            animator.Rebind();
            animator.Update(0.05f);

            // ---------- Capturas ----------
            Debug.Log("[PROOF] esticamento concluído; iniciando capturas.");
            animator.SetFloat("Speed", 0f);
            yield return new WaitForSeconds(0.6f);
            PoseCamera(new Vector3(0f, 1.35f, 2.75f), new Vector3(0f, 0.95f, 0f));
            yield return Shot("01_idle_frente.png");
            sb.AppendLine("1. Idle de frente: capturado.");

            // Caminhada - 4 instantes do ciclo
            animator.SetFloat("Speed", 0.62f);
            yield return new WaitForSeconds(0.6f);
            PoseCamera(new Vector3(2.65f, 1.2f, 0.35f), new Vector3(0f, 0.95f, 0f));
            for (int i = 0; i < 4; i++)
            {
                yield return Shot($"02_walk_perfil_0{i + 1}.png");
                sb.AppendLine($"2. Caminhada perfil {i + 1}/4: capturado.");
                yield return new WaitForSeconds(0.24f);
            }

            PoseCamera(new Vector3(0f, 4.0f, -2.9f), new Vector3(0f, 0.9f, 0.4f));
            yield return Shot("03_walk_topdown.png");
            sb.AppendLine("3. Caminhada vista de cima (53°): capturado.");

            // ---------- Disparo parado ----------
            animator.SetFloat("Speed", 0f);
            yield return new WaitForSeconds(0.35f);
            PoseCamera(new Vector3(-1.7f, 1.55f, 1.9f), new Vector3(0f, 1.15f, 0.3f));
            FireWeapon();
            yield return new WaitForSeconds(0.02f);
            yield return Shot("04_disparo_parado.png");
            sb.AppendLine("4. Disparo parado (flash + braços): capturado.");

            // ---------- Disparo ANDANDO (frente / lado / trás) ----------
            animator.SetFloat("Speed", 0.7f);
            yield return new WaitForSeconds(0.25f);
            FireWeapon();
            yield return new WaitForSeconds(0.03f);
            yield return Shot("05_disparo_andando_frente.png");
            sb.AppendLine("5. Disparo andando para frente: capturado.");

            animator.SetFloat("MoveX", 0.8f); animator.SetFloat("MoveZ", 0f);
            yield return new WaitForSeconds(0.2f);
            FireWeapon();
            yield return new WaitForSeconds(0.03f);
            yield return Shot("06_disparo_andando_lado.png");
            sb.AppendLine("6. Disparo andando de lado: capturado.");

            animator.SetFloat("MoveX", 0f); animator.SetFloat("MoveZ", -0.8f); 
            yield return new WaitForSeconds(0.2f);
            FireWeapon();
            yield return new WaitForSeconds(0.03f);
            yield return Shot("07_disparo_andando_tras.png");
            sb.AppendLine("7. Disparo andando para trás: capturado.");
            animator.SetFloat("MoveX", 0f); animator.SetFloat("MoveZ", 0f);

            // ---------- Bala em voo com rastro ----------
            animator.SetFloat("Speed", 0f);
            yield return new WaitForSeconds(0.3f);
            PoseCamera(new Vector3(2.1f, 1.65f, 1.1f), new Vector3(-0.4f, 1.15f, 3.6f));
            FireWeapon();
            yield return new WaitForSeconds(0.09f);
            var proj = FindProjectile();
            if (proj != null)
            {
                var trail = proj.GetComponent<TrailRenderer>();
                sb.AppendLine($"8. Bala em voo: posição={proj.transform.position} | trail={(trail != null ? trail.time.ToString("F2") + "s" : "NULO")} | distância do muzzle={Vector3.Distance(proj.transform.position, muzzle.position):F2}m");
            }
            yield return Shot("08_bala_fogo.png");
            sb.AppendLine("8. Bala em voo com rastro: capturado.");

            // ---------- Impacto ----------
            PoseCamera(new Vector3(1.1f, 1.55f, 4.2f), new Vector3(0f, 1.25f, 5.9f));
            FireWeapon();
            yield return new WaitForSeconds(0.155f);
            yield return Shot("09_impacto.png");
            sb.AppendLine("9. Impacto: capturado.");

            // ---------- Teste de movimento ----------
            Vector3 start = player.transform.position;
            float t = 0f;
            while (t < 1f)
            {
                player.transform.position += player.transform.forward * 6f * Time.deltaTime;
                t += Time.deltaTime;
                yield return null;
            }
            float delta = (player.transform.position - start).magnitude;
            sb.AppendLine($"\n[MOVIMENTO] 1s a 6 m/s => deslocamento={delta:F2} m (esperado ~6.00 m)");

            // ---------- 10 disparos ----------
            if (weapon != null && weapon.CurrentClip < 10)
            {
                weapon.TryReload();
                yield return new WaitForSeconds(1.4f);
            }
            int ok = 0, trav = 0, total = 10;
            for (int i = 0; i < total; i++)
            {
                FireWeapon();
                var p = FindProjectile();
                if (p != null && muzzle != null && Vector3.Distance(p.transform.position, muzzle.position) < 0.05f) ok++;
                yield return new WaitForSeconds(0.15f);
                var p2 = FindProjectile();
                if (p2 != null && muzzle != null && Vector3.Distance(p2.transform.position, muzzle.position) > 3f) trav++;
                yield return new WaitForSeconds(0.25f);
            }
            sb.AppendLine($"[TIRO] {ok}/{total} projéteis nasceram no Muzzle; {trav}/{total} percorreram a trajetória (>3m).");

            sb.AppendLine($"\n[CONSOLE] erros={errorCount} warnings={warnCount}");
            Finish(null);
        }

        private void FireWeapon()
        {
            if (weapon == null) { Debug.LogWarning("[PROOF] Weapon nula"); return; }
            try { FireWeaponInternal(); } catch (Exception e) { Debug.LogError("[PROOF] Falha ao atirar: " + e); }
        }

        private void FireWeaponInternal()
        {
            Vector3 target = player.transform.position + player.transform.forward * 25f;
            target.y = muzzle != null ? muzzle.position.y : 1.2f;
            weapon.TryFire(target);
        }

        private Projectile FindProjectile()
        {
            Projectile best = null;
            float bestDist = float.MaxValue;
            foreach (var p in Resources.FindObjectsOfTypeAll<Projectile>())
            {
                if (!p.gameObject.scene.IsValid() || !p.gameObject.activeInHierarchy) continue;
                float d = muzzle != null ? Vector3.Distance(p.transform.position, muzzle.position) : 0f;
                if (d < bestDist) { bestDist = d; best = p; }
            }
            return best;
        }

        private IEnumerable<AnimationClip> FinalClips()
        {
            var ctrl = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
            var seen = new HashSet<AnimationClip>();
            if (ctrl == null) yield break;
            foreach (var layer in ctrl.layers)
                foreach (var st in layer.stateMachine.states)
                {
                    var m = st.state.motion;
                    var tree = m as UnityEditor.Animations.BlendTree;
                    if (tree != null)
                    {
                        foreach (var ch in tree.children)
                            if (ch.motion is AnimationClip c && seen.Add(c)) yield return c;
                    }
                    else if (m is AnimationClip c2 && seen.Add(c2)) yield return c2;
                }
        }

        private static string StretchReport(GameObject root, AnimationClip clip)
        {
            // Ossos do SWAT (Quaternius): Hips/Chest/UpperArm/LowerArm/Wrist/UpperLeg/LowerLeg/Foot
            var pairs = new[]
            {
                new[] { "Hips", "Chest" },
                new[] { "UpperArm.R", "LowerArm.R" },
                new[] { "LowerArm.R", "Wrist.R" },
                new[] { "LowerArm.L", "Wrist.L" },
                new[] { "UpperLeg.R", "LowerLeg.R" },
                new[] { "LowerLeg.R", "Foot.R" },
                new[] { "UpperLeg.L", "LowerLeg.L" },
                new[] { "LowerLeg.L", "Foot.L" }
            };

            var first = new float[pairs.Length];
            var worst = new float[pairs.Length];
            int samples = 12;

            for (int i = 0; i < samples; i++)
            {
                clip.SampleAnimation(root, clip.length * i / samples);
                for (int p = 0; p < pairs.Length; p++)
                {
                    var a = FindDeep(root.transform, pairs[p][0]);
                    var b = FindDeep(root.transform, pairs[p][1]);
                    if (a == null || b == null) continue;
                    float d = Vector3.Distance(a.position, b.position);
                    if (i == 0) { first[p] = d; continue; }
                    if (first[p] > 0.0001f)
                        worst[p] = Mathf.Max(worst[p], Mathf.Abs(d - first[p]) / first[p] * 100f);
                }
            }

            int wi = 0;
            for (int p = 1; p < worst.Length; p++) if (worst[p] > worst[wi]) wi = p;
            string status = worst[wi] < 3f ? "PASS" : "FAIL";
            return $"{clip.name}: pior {pairs[wi][0]}->{pairs[wi][1]} = {worst[wi]:F2}% [{status}]";
        }

        private float MeasureHeight()
        {
            var b = new Bounds(player.transform.position, Vector3.zero);
            bool first = true;
            foreach (var r in player.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            return b.size.y;
        }

        private void PoseCamera(Vector3 pos, Vector3 lookAt)
        {
            cam.transform.position = pos;
            cam.transform.LookAt(lookAt);
        }

        private IEnumerator Shot(string file)
        {
            // Mesma regra do PlayerShooter: cano orientado para a frente/mira
            if (weapon != null)
            {
                weapon.transform.rotation = Quaternion.LookRotation(player.transform.forward, Vector3.up);
            }
            yield return null;   // batchmode: sem WaitForEndOfFrame (não renderiza em headless)
            int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = prev;
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(SCREEN_DIR, file), tex.EncodeToPNG());
            Destroy(tex);
            Destroy(rt);
            captured.Add(file);
            Debug.Log("[PROOF] captura " + file);
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var f = FindDeep(parent.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }

        private void Finish(string error)
        {
            // Segurança: jamais encerrar o Editor se o harness não foi disparado por linha de comando
            if (!AutoRun) { Debug.LogWarning("[PROOF] Finish ignorado (AutoRun=false)."); return; }

            if (!string.IsNullOrEmpty(error)) sb.AppendLine("FALHA: " + error);
            sb.AppendLine("\nImagens: " + string.Join(", ", captured));
            File.WriteAllText(REPORT, sb.ToString(), Encoding.UTF8);
            Debug.Log(sb.ToString());
#if UNITY_EDITOR
            EditorApplication.Exit(string.IsNullOrEmpty(error) ? 0 : 1);
#else
            Application.Quit(string.IsNullOrEmpty(error) ? 0 : 1);
#endif
        }
    }
}
#endif
