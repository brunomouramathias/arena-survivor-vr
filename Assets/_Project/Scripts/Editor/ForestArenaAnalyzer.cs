using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ArenaSurvivor.Editor
{
    // Importa o pacote Forest Arena (OBJ) e gera previews para identificar cada peça.
    public static class ForestArenaAnalyzer
    {
        public const string OBJ_PATH = "Assets/_Project/ThirdParty/ForestArena/ForestArena.obj";
        public const string MAT_DIR  = "Assets/_Project/Materials/ForestArena";
        public const string SHOT_DIR = "docs/screens/cenario";

        [MenuItem("Tools/Arena/Analyze Forest Arena OBJ")]
        public static void Run() { RunInternal(false); }

        public static void RunBatch() { RunInternal(true); }

        private static void RunInternal(bool isBatch)
        {
            Directory.CreateDirectory(SHOT_DIR);
            Directory.CreateDirectory(MAT_DIR);
            var sb = new StringBuilder();
            sb.AppendLine("=== [FOREST ARENA] " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");

            try
            {
                AssetDatabase.Refresh();
                var mi = AssetImporter.GetAtPath(OBJ_PATH) as ModelImporter;
                if (mi == null) throw new Exception("ModelImporter não encontrado: " + OBJ_PATH);
                mi.globalScale = 1f;
                mi.importAnimation = false;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                mi.importNormals = ModelImporterNormals.Import;
                mi.SaveAndReimport();

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(OBJ_PATH);
                if (model == null) throw new Exception("Falha ao carregar o OBJ.");

                // Materiais -> URP Lit
                ConvertMaterials(model, sb);

                // Cena de preview
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var lightGO = new GameObject("Sun");
                var light = lightGO.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.25f;
                light.color = new Color(1f, 0.97f, 0.9f);
                lightGO.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

                var clone = UnityEngine.Object.Instantiate(model);
                clone.name = "ForestArena_Preview";
                clone.transform.position = Vector3.zero;

                // Neutraliza shaders rosa
                foreach (var r in clone.GetComponentsInChildren<Renderer>())
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i] != null && mats[i].shader != null && mats[i].shader.name.Contains("Standard"))
                            mats[i].shader = Shader.Find("Universal Render Pipeline/Lit");
                    r.sharedMaterials = mats;
                }

                var bounds = ComputeBounds(clone);
                sb.AppendLine($"Diorama bounds: centro={bounds.center} tamanho={bounds.size}");
                sb.AppendLine($"Peças (filhos diretos): {clone.transform.childCount}");

                // Preview geral
                var camGO = new GameObject("Cam");
                var cam = camGO.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.55f, 0.65f, 0.75f);
                cam.fieldOfView = 35f;

                float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                Vector3 c = bounds.center;
                cam.transform.position = c + new Vector3(maxDim * 1.1f, maxDim * 0.8f, -maxDim * 1.4f);
                cam.transform.LookAt(c);
                Capture(cam, $"{SHOT_DIR}/00_diorama_persp.png");

                cam.transform.position = c + new Vector3(0f, maxDim * 1.8f, -0.001f);
                cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                Capture(cam, $"{SHOT_DIR}/01_diorama_top.png");

                // Grid das peças isoladas
                var pieces = new List<Transform>();
                for (int i = 0; i < clone.transform.childCount; i++) pieces.Add(clone.transform.GetChild(i));

                float cell = 0.0f;
                var pieceInfo = new List<(Transform t, Bounds b)>();
                foreach (var p in pieces)
                {
                    var b = ComputeBounds(p.gameObject);
                    pieceInfo.Add((p, b));
                    cell = Mathf.Max(cell, Mathf.Max(b.size.x, b.size.z) * 1.6f);
                }
                cell = Mathf.Max(cell, 0.15f);

                int cols = 6;
                var gridRoot = new GameObject("GRID").transform;
                for (int i = 0; i < pieceInfo.Count; i++)
                {
                    var (p, b) = pieceInfo[i];
                    var copy = UnityEngine.Object.Instantiate(p.gameObject, gridRoot);
                    copy.name = $"G{i:00}_{p.name}";
                    int col = i % cols, row = i / cols;
                    copy.transform.position = new Vector3(col * cell, 0f, -row * cell) - b.center + Vector3.up * (b.extents.y);
                    sb.AppendLine($"  [{i:00}] {p.name} | size=({b.size.x:F3},{b.size.y:F3},{b.size.z:F3}) | centroLocal={p.localPosition} | tris={CountTris(p.gameObject)}");
                }

                var gb = ComputeBounds(gridRoot.gameObject);
                float gmax = Mathf.Max(gb.size.x, gb.size.z);
                cam.transform.position = gb.center + new Vector3(0f, gmax * 1.25f, -gmax * 0.02f);
                cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                Capture(cam, $"{SHOT_DIR}/02_pecas_grid.png");

                // Grid em perspectiva
                cam.transform.position = gb.center + new Vector3(gmax * 0.55f, gmax * 0.55f, -gmax * 0.65f);
                cam.transform.LookAt(gb.center);
                Capture(cam, $"{SHOT_DIR}/03_pecas_persp.png");

                File.WriteAllText("docs/forest_arena_pecas.txt", sb.ToString(), Encoding.UTF8);
                Debug.Log(sb.ToString());
                if (isBatch) EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                sb.AppendLine("EXCEÇÃO: " + e);
                File.WriteAllText("docs/forest_arena_pecas.txt", sb.ToString(), Encoding.UTF8);
                Debug.LogError(e);
                if (isBatch) EditorApplication.Exit(1);
            }
        }

        private static void ConvertMaterials(GameObject model, StringBuilder sb)
        {
            var seen = new HashSet<Material>();
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || seen.Contains(m)) continue;
                    seen.Add(m);
                    Color col = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.gray;
                    var urp = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = m.name };
                    urp.SetColor("_BaseColor", col);
                    urp.SetFloat("_Smoothness", 0.15f);
                    string path = $"{MAT_DIR}/{Sanitize(m.name)}.mat";
                    AssetDatabase.DeleteAsset(path);
                    AssetDatabase.CreateAsset(urp, path);
                    mats[i] = urp;
                    sb.AppendLine($"  material {m.name} -> {col}");
                }
                r.sharedMaterials = mats;
            }
            AssetDatabase.SaveAssets();
        }

        private static string Sanitize(string s)
        {
            foreach (var ch in Path.GetInvalidFileNameChars()) s = s.Replace(ch, '_');
            return s.Replace(' ', '_');
        }

        private static int CountTris(GameObject go)
        {
            int n = 0;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null) n += mf.sharedMesh.triangles.Length / 3;
            return n;
        }

        public static Bounds ComputeBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        private static void Capture(Camera cam, string path)
        {
            int w = 1600, h = 900;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(rt);
            Debug.Log("[FOREST] captura " + path);
        }
    }
}
