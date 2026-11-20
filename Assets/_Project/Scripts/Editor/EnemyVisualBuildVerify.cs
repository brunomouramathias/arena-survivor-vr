#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using ArenaSurvivor.Enemies;

namespace ArenaSurvivor.Editor
{
    // Cria o prefab que faltava (Enemy_Stalker) e faz a verificação visual dos personagens.
    public static class EnemyVisualBuildVerify
    {
        const string OUT = "docs/screens/diag";
        const string MAT = "Assets/_Project/Materials";
        const string PREFABS = "Assets/_Project/Prefabs";
        static readonly StringBuilder sb = new StringBuilder();

        [MenuItem("Tools/Arena/Build Stalker + Verify Visual")]
        public static void Run()
        {
            Directory.CreateDirectory(OUT);
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Arena.unity", OpenSceneMode.Single);

            BuildStalkerPrefab();
            WireStalker(scene);
            VisualCheck();

            File.WriteAllText(OUT + "/verify.txt", sb.ToString(), Encoding.UTF8);
            Debug.Log(sb.ToString());
            EditorApplication.Exit(0);
        }

        static Material M(string n) => AssetDatabase.LoadAssetAtPath<Material>($"{MAT}/{n}.mat");

        static void BuildStalkerPrefab()
        {
            string path = $"{PREFABS}/Enemy_Stalker.prefab";
            AssetDatabase.DeleteAsset(path);

            var chaser = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFABS}/Enemy_Chaser.prefab");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(chaser);
            inst.name = "Enemy_Stalker";
            var old = inst.GetComponent<EnemyChaser>();
            if (old != null) Object.DestroyImmediate(old);
            var stalker = inst.AddComponent<EnemyStalker>();
            var so = new SerializedObject(stalker);
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/_Project/ScriptableObjects/Enemies/EnemyData_Stalker.asset");
            so.FindProperty("data").objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();

            var skin = M("Mat_Stalker_Skin");
            var eyes = M("Mat_Stalker_Eyes");
            int bodies = 0, eyeCount = 0;
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.StartsWith("Eye_Glow")) { r.sharedMaterial = eyes; eyeCount++; }
                else if (r.GetComponent<SkinnedMeshRenderer>() != null) { r.sharedMaterial = skin; bodies++; }
            }

            // matar o NavMeshAgent duplicado não é necessário; o prefab do chaser já tem um
            PrefabUtility.SaveAsPrefabAsset(inst, path);
            Object.DestroyImmediate(inst);
            sb.AppendLine($"[STALKER] prefab criado: {path} bodies={bodies} eyes={eyeCount} data={(data != null)}");
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            sb.AppendLine($"[STALKER] componentes: {saved.GetComponents<Component>().Length}, EnemyStalker={saved.GetComponent<EnemyStalker>() != null}");
        }

        static void WireStalker(UnityEngine.SceneManagement.Scene scene)
        {
            var stalkerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFABS}/Enemy_Stalker.prefab");
            var spawner = Object.FindAnyObjectByType<ArenaSurvivor.Waves.WaveSpawner>();
            if (spawner != null)
            {
                var so = new SerializedObject(spawner);
                so.FindProperty("stalkerPrefab").objectReferenceValue = stalkerPrefab;
                so.ApplyModifiedPropertiesWithoutUndo();
                sb.AppendLine("[WIRE] WaveSpawner.stalkerPrefab = Enemy_Stalker.prefab");
            }
            var init = Object.FindAnyObjectByType<ArenaSurvivor.Core.GameInitializer>();
            if (init != null)
            {
                var so = new SerializedObject(init);
                var p = so.FindProperty("stalkerPrefab");
                if (p != null)
                {
                    p.objectReferenceValue = stalkerPrefab;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    sb.AppendLine("[WIRE] GameInitializer.stalkerPrefab = Enemy_Stalker.prefab");
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void VisualCheck()
        {
            var player = GameObject.Find("Player");
            Vector3 center = player != null ? player.transform.position : Vector3.zero;
            sb.AppendLine($"[VIS] player em {center}");

            var prefabs = new[] { "Enemy_Chaser", "Enemy_Shooter", "Enemy_Stalker", "Enemy_Boss" };
            var insts = new System.Collections.Generic.List<GameObject>();
            if (player != null) insts.Add(player);
            foreach (var n in prefabs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFABS}/{n}.prefab");
                if (prefab == null) { sb.AppendLine($"[VIS] PREFAB AUSENTE {n}"); continue; }
                insts.Add((GameObject)PrefabUtility.InstantiatePrefab(prefab));
            }

            var cam = Camera.main;
            if (cam == null) { sb.AppendLine("[VIS] SEM MAIN CAMERA"); return; }
            var camData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            sb.AppendLine($"[VIS] camera post={camData?.renderPostProcessing} pos={cam.transform.position} fov={cam.fieldOfView}");

            float spacing = 3.0f;
            Vector3 basePos = center + new Vector3(-spacing * (insts.Count - 1) * 0.5f, 0, 9f);
            for (int i = 0; i < insts.Count; i++)
            {
                insts[i].transform.position = basePos + new Vector3(i * spacing, 0, 0);
                insts[i].transform.rotation = Quaternion.Euler(0, 180f, 0); // de frente para a c�mera
            }

            foreach (var go in insts)
            {
                float h = 0f;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (!(r is SkinnedMeshRenderer)) continue;
                    h = Mathf.Max(h, r.bounds.size.y);
                    var m = r.sharedMaterial;
                    sb.AppendLine($"   [{go.name}/{r.name}] boundsY={r.bounds.max.y:F2}..{r.bounds.min.y:F2} mat='{(m != null ? m.name : "NULL")}'");
                }
                sb.AppendLine($"--- {go.name} alturaVisual={h:F2}m");

                Vector3 look = go.transform.position + new Vector3(0, h * 0.55f, 0);
                cam.fieldOfView = 40;
                cam.transform.position = look + new Vector3(0, h * 0.08f, -Mathf.Max(3.4f, h * 2.1f));
                cam.transform.LookAt(look);
                Capture(cam, $"20_{go.name}.png");
            }

            Vector3 mid = basePos + new Vector3(spacing * (insts.Count - 1) * 0.5f, 0, 0);
            cam.fieldOfView = 46;
            cam.transform.position = mid + new Vector3(0, 2.6f, -13f);
            cam.transform.LookAt(mid + new Vector3(0, 1.1f, 0));
            Capture(cam, "10_personagens.png");
        }

        static void Capture(Camera cam, string file)
        {
            int w = 1600, h = 900;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            File.WriteAllBytes($"{OUT}/{file}", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
        }
    }
}
#endif
