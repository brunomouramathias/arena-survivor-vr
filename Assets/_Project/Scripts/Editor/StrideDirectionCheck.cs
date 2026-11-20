using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ArenaSurvivor.Editor
{
    // Verifica para onde cada clipe de locomoção "empurra" o pé de apoio (direção implícita de caminhada).
    public static class StrideDirectionCheck
    {
        const string ARENA = "Assets/_Project/Scenes/Arena.unity";

        [MenuItem("Tools/Arena/Check Stride Directions")]
        public static void Run() { RunInternal(false); }

        public static void RunBatch() { RunInternal(true); }

        private static void RunInternal(bool isBatch)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== [STRIDE DIRECTION] " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");
            try
            {
                var scene = EditorSceneManager.OpenScene(ARENA, OpenSceneMode.Single);
                var player = GameObject.FindGameObjectWithTag("Player");
                var anim = player.GetComponent<Animator>();
                anim.enabled = false;

                var clips = new Dictionary<string, AnimationClip>();
                foreach (var c in AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/ThirdParty/PolyPizza_Swat/Swat.fbx").OfType<AnimationClip>())
                {
                    if (c.name.StartsWith("__preview__")) continue;
                    string n = c.name.Contains("|") ? c.name.Substring(c.name.LastIndexOf('|') + 1) : c.name;
                    clips[n] = c;
                }

                Transform footL = FindDeep(player.transform, "Foot.L");
                Transform footR = FindDeep(player.transform, "Foot.R");

                foreach (var n in new[] { "Walk", "Run", "Run_Back", "Run_Left", "Run_Right", "Run_Shoot" })
                {
                    if (!clips.TryGetValue(n, out var clip)) continue;
                    var info = Measure(player, clip, footL, footR, 48);
                    sb.AppendLine($"{n,-10} passo: v={info.speed:F2} m/s  vetor_local=({info.vec.x:F2},{info.vec.z:F2})  direção_implícita=({-info.vec.x:F2},{-info.vec.z:F2})");
                }

                File.WriteAllText("docs/stride_directions.txt", sb.ToString(), Encoding.UTF8);
                Debug.Log(sb.ToString());
                if (isBatch) EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                sb.AppendLine("EXCEÇÃO: " + e);
                File.WriteAllText("docs/stride_directions.txt", sb.ToString(), Encoding.UTF8);
                Debug.LogError(e);
                if (isBatch) EditorApplication.Exit(1);
            }
        }

        private static (float speed, Vector3 vec) Measure(GameObject player, AnimationClip clip, Transform footL, Transform footR, int samples)
        {
            float dt = clip.length / samples;
            var lP = new Vector3[samples];
            var rP = new Vector3[samples];
            var lY = new float[samples];
            var rY = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                clip.SampleAnimation(player, clip.length * i / samples);
                lP[i] = player.transform.InverseTransformPoint(footL.position);
                rP[i] = player.transform.InverseTransformPoint(footR.position);
                lY[i] = footL.position.y; rY[i] = footR.position.y;
            }
            float minL = lY.Min(), minR = rY.Min();
            Vector3 sum = Vector3.zero; int count = 0; float speedSum = 0f;
            for (int i = 1; i < samples; i++)
            {
                bool left = (lY[i] - minL) <= (rY[i] - minR);
                Vector3 a = left ? lP[i - 1] : rP[i - 1];
                Vector3 b = left ? lP[i] : rP[i];
                float y = left ? lY[i] : rY[i];
                float minY = left ? minL : minR;
                if (y - minY > 0.06f) continue;
                Vector3 d = b - a;
                d.y = 0f;
                sum += d;
                speedSum += d.magnitude / dt;
                count++;
            }
            if (count == 0) return (0f, Vector3.zero);
            return (speedSum / count, sum / count);
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
