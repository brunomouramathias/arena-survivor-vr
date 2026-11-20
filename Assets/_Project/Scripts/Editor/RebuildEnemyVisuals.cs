#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ArenaSurvivor.Enemies;

namespace ArenaSurvivor.Editor
{
    // Reconstrói a malha/esqueleto dos inimigos (o import antigo em 0.01 deixou os prefabs
    // com esqueleto em centímetros e a malha em metros = personagem microscópico).
    public static class RebuildEnemyVisuals
    {
        const string MAT = "Assets/_Project/Materials";
        const string PREFABS = "Assets/_Project/Prefabs";
        const string INBOX = "Assets/_Project/ThirdParty/_INBOX/Mixamo";
        static readonly StringBuilder sb = new StringBuilder();

        [MenuItem("Tools/Arena/Rebuild Enemy Visuals")]
        public static void Run()
        {
            Rebuild("Enemy_Chaser", $"{INBOX}/Zombie Walk.fbx", "Mat_EnemyChaser", "Mat_Chaser_Eyes", 1.0f, 0.06f);
            Rebuild("Enemy_Shooter", $"{INBOX}/Zombie Run.fbx", "Mat_EnemyShooter", "Mat_Shooter_Eyes", 1.0f, 0.06f);
            Rebuild("Enemy_Stalker", $"{INBOX}/Zombie Run.fbx", "Mat_Stalker_Skin", "Mat_Stalker_Eyes", 1.0f, 0.06f);
            Rebuild("Enemy_Boss", $"{INBOX}/Standing 2H Magic Attack 05.fbx", "Mat_Boss_Mage", "Mat_Boss_Eyes", 1.55f, 0.11f);

            File.WriteAllText("docs/screens/diag/rebuild.txt", sb.ToString(), Encoding.UTF8);
            Debug.Log(sb.ToString());
            EditorApplication.Exit(0);
        }

        static void Rebuild(string prefabName, string fbx, string bodyMatName, string eyeMatName, float scale, float eyeSize)
        {
            string prefabPath = $"{PREFABS}/{prefabName}.prefab";
            var body = AssetDatabase.LoadAssetAtPath<Material>($"{MAT}/{bodyMatName}.mat");
            var eyes = AssetDatabase.LoadAssetAtPath<Material>($"{MAT}/{eyeMatName}.mat");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (body == null || eyes == null || model == null) { sb.AppendLine($"FALTA ASSET em {prefabName}"); return; }

            using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
            {
                var root = scope.prefabContentsRoot;
                Transform old = root.transform.Find("Model_Zombie") ?? root.transform.Find("Model_Mage");
                string visualName = old != null ? old.name : "Model_Zombie";
                if (old != null) Object.DestroyImmediate(old.gameObject);

                var visual = (GameObject)Object.Instantiate(model, root.transform);
                visual.name = visualName;
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one * scale;

                int n = 0;
                foreach (var r in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    r.sharedMaterial = body;
                    r.updateWhenOffscreen = true;
                    n++;
                }

                // olhos grudados na cabeça do modelo
                var head = FindDeep(visual.transform, "mixamorig:Head");
                Vector3 headLocal = head != null
                    ? visual.transform.InverseTransformPoint(head.position)
                    : new Vector3(0, 1.6f, 0.1f);
                float h = 0f;
                foreach (var r in visual.GetComponentsInChildren<Renderer>())
                    h = Mathf.Max(h, r.bounds.max.y - visual.transform.position.y);

                Vector3 eyePos = headLocal + new Vector3(0, 0.11f, 0.10f);
                MakeEye(visual.transform, "Eye_Glow_L", eyePos + new Vector3(-0.045f, 0, 0), eyeSize, eyes);
                MakeEye(visual.transform, "Eye_Glow_R", eyePos + new Vector3(0.045f, 0, 0), eyeSize, eyes);

                sb.AppendLine($"{prefabName}: visual={visualName} scale={scale} skinned={n} headLocal={headLocal} alturaLocal={h:F2}m alturaFinal={h * scale:F2}m");
            }
            AssetDatabase.SaveAssets();
        }

        static void MakeEye(Transform parent, string name, Vector3 localPos, float size, Material mat)
        {
            var e = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            e.name = name;
            Object.DestroyImmediate(e.GetComponent<Collider>());
            e.transform.SetParent(parent, false);
            e.transform.localPosition = localPos;
            e.transform.localScale = Vector3.one * size;
            e.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
#endif
