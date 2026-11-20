using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

namespace ArenaSurvivor.Editor
{
    // Mede a "velocidade de solo" real de cada clipe (pé de apoio) e normaliza o Blend Tree
    // para que a passada acompanhe a velocidade do jogador (elimina o deslize).
    public static class StrideCalibrator
    {
        public const string ARENA = "Assets/_Project/Scenes/Arena.unity";
        public const string CTRL  = "Assets/_Project/Animations/Player/AnimatorController_Player.controller";
        public const string REPORT = "docs/stride_calibration.txt";

        // Velocidade de referência (m/s) para a qual todos os clipes serão normalizados
        public const float REFERENCE_SPEED = 4.0f;

        [MenuItem("Tools/Arena/Calibrate Locomotion Stride")]
        public static void Run() { RunInternal(false); }

        public static void RunBatch() { RunInternal(true); }

        private static void RunInternal(bool isBatch)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== [STRIDE] " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");

            try
            {
                var scene = EditorSceneManager.OpenScene(ARENA, OpenSceneMode.Single);
                var player = GameObject.FindGameObjectWithTag("Player");
                var anim = player.GetComponent<Animator>();
                var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CTRL);

                var clips = new Dictionary<string, AnimationClip>();
                foreach (var c in AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/ThirdParty/PolyPizza_Swat/Swat.fbx").OfType<AnimationClip>())
                {
                    if (c.name.StartsWith("__preview__")) continue;
                    string n = c.name.Contains("|") ? c.name.Substring(c.name.LastIndexOf('|') + 1) : c.name;
                    clips[n] = c;
                }

                string[] names = { "Walk", "Run", "Run_Back", "Run_Left", "Run_Right", "Run_Shoot" };
                var speeds = new Dictionary<string, float>();

                anim.enabled = false;
                Transform hips = FindDeep(player.transform, "Hips");
                Transform footL = FindDeep(player.transform, "Foot.L");
                Transform footR = FindDeep(player.transform, "Foot.R");
                sb.AppendLine($"Ossos: hips={(hips != null)} footL={(footL != null)} footR={(footR != null)}");

                foreach (var n in names)
                {
                    if (!clips.TryGetValue(n, out var clip) || clip == null) continue;
                    float spd = MeasureGroundSpeed(player, clip, hips, footL, footR, 36);
                    speeds[n] = spd;
                    sb.AppendLine($"{n,-10} len={clip.length:F2}s  velocidade_solo={spd:F2} m/s");
                }

                // Normaliza cada clipe para REFERENCE_SPEED via timeScale dos filhos do Blend Tree
                var tree = FindBlendTree(ctrl);
                if (tree != null)
                {
                    var children = tree.children;
                    for (int i = 0; i < children.Length; i++)
                    {
                        var m = children[i].motion as AnimationClip;
                        if (m == null) continue;
                        string n = m.name.Contains("|") ? m.name.Substring(m.name.LastIndexOf('|') + 1) : m.name;
                        if (speeds.TryGetValue(n, out float spd) && spd > 0.05f)
                        {
                            children[i].timeScale = REFERENCE_SPEED / spd;
                            sb.AppendLine($"  filho '{n}' timeScale={children[i].timeScale:F2}");
                        }
                    }
                    tree.children = children;
                    EditorUtility.SetDirty(tree);
                }
                else sb.AppendLine("AVISO: Blend Tree não encontrado no controller.");

                EditorUtility.SetDirty(ctrl);
                AssetDatabase.SaveAssets();

                // Ajusta a fórmula de velocidade no PlayerController para a referência
                PatchPlayerControllerReference(sb);

                File.WriteAllText(REPORT, sb.ToString(), Encoding.UTF8);
                Debug.Log(sb.ToString());
                if (isBatch) EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                sb.AppendLine("EXCEÇÃO: " + e);
                File.WriteAllText(REPORT, sb.ToString(), Encoding.UTF8);
                Debug.LogError(e);
                if (isBatch) EditorApplication.Exit(1);
            }
        }

        // Mede a velocidade do pé de apoio (deslocamento horizontal por segundo)
        private static float MeasureGroundSpeed(GameObject player, AnimationClip clip, Transform hips, Transform footL, Transform footR, int samples)
        {
            if (hips == null || footL == null || footR == null) return 0f;

            var lPos = new Vector3[samples];
            var rPos = new Vector3[samples];
            var lY = new float[samples];
            var rY = new float[samples];
            float dt = clip.length / samples;

            for (int i = 0; i < samples; i++)
            {
                clip.SampleAnimation(player, clip.length * i / samples);
                Vector3 l = player.transform.InverseTransformPoint(footL.position);
                Vector3 r = player.transform.InverseTransformPoint(footR.position);
                lPos[i] = l; rPos[i] = r;
                lY[i] = footL.position.y; rY[i] = footR.position.y;
            }

            float minL = lY.Min(), minR = rY.Min();
            var speeds = new List<float>();
            for (int i = 1; i < samples; i++)
            {
                // pé de apoio = mais próximo do chão
                bool leftIsStance = (lY[i] - minL) <= (rY[i] - minR);
                Vector3 a = leftIsStance ? lPos[i - 1] : rPos[i - 1];
                Vector3 b = leftIsStance ? lPos[i] : rPos[i];
                float y = leftIsStance ? lY[i] : rY[i];
                float minY = leftIsStance ? minL : minR;
                if (y - minY > 0.06f) continue; // só na fase de apoio
                float v = new Vector2(b.x - a.x, b.z - a.z).magnitude / dt;
                if (v > 0.05f && v < 20f) speeds.Add(v);
            }
            if (speeds.Count == 0) return 0f;
            speeds.Sort();
            // mediana das maiores velocidades de apoio (evita outliers)
            int from = Mathf.Clamp(speeds.Count / 3, 0, speeds.Count - 1);
            var tail = speeds.GetRange(from, speeds.Count - from);
            return tail.Average();
        }

        private static BlendTree FindBlendTree(AnimatorController ctrl)
        {
            foreach (var layer in ctrl.layers)
                foreach (var st in layer.stateMachine.states)
                    if (st.state.motion is BlendTree bt) return bt;
            return null;
        }

        private static void PatchPlayerControllerReference(StringBuilder sb)
        {
            const string path = "Assets/_Project/Scripts/Player/PlayerController.cs";
            string code = File.ReadAllText(path);
            string oldExpr = "animator.SetFloat(LegSpeedHash, Mathf.Clamp(planar / 2.6f, 0.8f, 2.4f), 0.1f, Time.deltaTime);";
            string newExpr = $"animator.SetFloat(LegSpeedHash, Mathf.Clamp(planar / {REFERENCE_SPEED.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}f, 0.5f, 2.2f), 0.1f, Time.deltaTime);";
            if (code.Contains(oldExpr))
            {
                code = code.Replace(oldExpr, newExpr);
                File.WriteAllText(path, code);
                sb.AppendLine("PlayerController.cs: fórmula de LegSpeed atualizada para a referência.");
            }
            else sb.AppendLine("PlayerController.cs: fórmula já atualizada (ou não encontrada).");
        }

        private static Transform FindDeep(Transform p, string name)
        {
            if (p.name == name) return p;
            for (int i = 0; i < p.childCount; i++)
            {
                var f = FindDeep(p.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }
    }
}
