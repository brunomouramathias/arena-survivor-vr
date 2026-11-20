#if UNITY_EDITOR
using System.Text;
using UnityEngine;
using UnityEditor;

namespace ArenaSurvivor.Editor
{
    public static class BoneListProbe
    {
        [MenuItem("Tools/Arena/Bone List Probe")]
        public static void Run()
        {
            var sb = new StringBuilder();
            foreach (var n in new[] { "Enemy_Chaser", "Enemy_Boss" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/{n}.prefab");
                sb.AppendLine($"=== {n}");
                foreach (var r in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (r.name != "Beta_Surface") continue;
                    foreach (var b in r.bones) sb.AppendLine("  " + b.name + "  @ " + b.localPosition.ToString("F3"));
                }
            }
            System.IO.File.WriteAllText("docs/screens/diag/bones.txt", sb.ToString());
            Debug.Log(sb.ToString());
            EditorApplication.Exit(0);
        }
    }
}
#endif
