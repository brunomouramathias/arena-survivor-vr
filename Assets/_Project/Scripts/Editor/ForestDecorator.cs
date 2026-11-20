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
    // Substitui a vegetação do perímetro pelos modelos do pacote Forest (OBJ) e adiciona nuvens,
    // mantendo exatamente as posições já validadas do anel externo da arena.
    public static class ForestDecorator
    {
        public const string OBJ_PATH = "Assets/_Project/ThirdParty/ForestArena/ForestArena.obj";
        public const string MTL_PATH = "Assets/_Project/ThirdParty/ForestArena/materials.mtl";
        public const string MAT_DIR  = "Assets/_Project/Materials/ForestArena";
        public const string ARENA    = "Assets/_Project/Scenes/Arena.unity";
        public const string PERIMETER = "--- Nature_Perimeter ---";
        public const string LOG_PATH = "docs/forest_decor_log.txt";

        [MenuItem("Tools/Arena/Decorate Arena With Forest Pack")]
        public static void Run() { RunInternal(false); }

        public static void RunBatch() { RunInternal(true); }

        private static void RunInternal(bool isBatch)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== [FOREST DECOR] " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");

            try
            {
                Directory.CreateDirectory(MAT_DIR);
                MaintainMtl(sb);
                AssetDatabase.Refresh();

                var mi = AssetImporter.GetAtPath(OBJ_PATH) as ModelImporter;
                if (mi == null) throw new Exception("ModelImporter não encontrado.");
                mi.globalScale = 1f;
                mi.importAnimation = false;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                mi.SaveAndReimport();

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(OBJ_PATH);
                if (model == null) throw new Exception("OBJ não carregou.");

                var mats = ConvertToUrp(model, sb);

                // Separa peças: árvores (não nuvens, não base)
                var pieces = new List<Transform>();
                for (int i = 0; i < model.transform.childCount; i++)
                    pieces.Add(model.transform.GetChild(i));

                var treePieces = new List<Transform>();
                var cloudPieces = new List<Transform>();
                Transform basePiece = null;

                foreach (var p in pieces)
                {
                    var b = BoundsOf(p.gameObject);
                    float w = Mathf.Max(b.size.x, b.size.z);
                    if (w > 0.6f) { basePiece = p; continue; }
                    if (IsCloudPiece(p)) { cloudPieces.Add(p); continue; }
                    if (IsRockPiece(p)) continue;   // pedras: não usar como árvore
                    treePieces.Add(p);
                }
                sb.AppendLine($"Peças: árvores={treePieces.Count} nuvens={cloudPieces.Count} base={(basePiece != null)}");

                // Cena
                var scene = EditorSceneManager.OpenScene(ARENA, OpenSceneMode.Single);
                var perimeter = GameObject.Find(PERIMETER);
                if (perimeter == null) throw new Exception("Container do perímetro não encontrado.");

                // Guarda posições/rotações das árvores antigas
                var slots = new List<(Vector3 pos, Quaternion rot, float height)>();
                var toRemove = new List<GameObject>();
                foreach (Transform child in perimeter.transform)
                {
                    string ln = child.name.ToLowerInvariant();
                    if (!ln.Contains("tree") && !ln.StartsWith("forest_")) continue;
                    var b = BoundsOf(child.gameObject);
                    slots.Add((child.position, child.rotation, Mathf.Max(2f, b.size.y)));
                    toRemove.Add(child.gameObject);
                }
                foreach (var go in toRemove) UnityEngine.Object.DestroyImmediate(go);
                sb.AppendLine($"Árvores antigas removidas: {toRemove.Count}; substituindo por modelos do pacote Forest.");

                var rnd = new System.Random(2026);
                int placed = 0;
                foreach (var slot in slots)
                {
                    var piece = treePieces[rnd.Next(treePieces.Count)];
                    var go = InstantiatePiece(piece, perimeter.transform, slot.pos, slot.rot);
                    // altura ~ slot.height (mantém silhueta do anel)
                    float h = BoundsOf(piece.gameObject).size.y;
                    float targetH = Mathf.Clamp(slot.height, 4.5f, 7.5f);
                    float scale = h > 0.01f ? targetH / h : 12f;
                    go.transform.localScale = Vector3.one * scale;
                    go.name = "Forest_" + piece.name;

                    // collider simples no tronco (não interfere no gameplay: fora dos muros)
                    var capsule = go.AddComponent<CapsuleCollider>();
                    capsule.radius = 0.35f;
                    capsule.height = 3f;
                    capsule.center = new Vector3(0f, 1.5f, 0f);
                    placed++;
                }
                sb.AppendLine($"Árvores do pacote Forest colocadas: {placed}");

                // Nuvens no céu
                for (int i = 0; i < cloudPieces.Count; i++)
                {
                    var p = cloudPieces[i];
                    var pos = new Vector3(i == 0 ? -14f : 16f, 17f, i == 0 ? -6f : 10f);
                    var go = InstantiatePiece(p, null, pos, Quaternion.Euler(0f, i * 35f, 0f));
                    go.name = "Forest_Cloud" + (i + 1);
                    go.transform.localScale = Vector3.one * 22f;
                    var mr = go.GetComponentInChildren<MeshRenderer>();
                    if (mr != null) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                sb.AppendLine($"Nuvens adicionadas: {cloudPieces.Count}");

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

                // Captura de verificação (câmera do jogo)
                CaptureArena(sb);

                File.WriteAllText(LOG_PATH, sb.ToString(), Encoding.UTF8);
                Debug.Log(sb.ToString());
                if (isBatch) EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                sb.AppendLine("EXCEÇÃO: " + e);
                File.WriteAllText(LOG_PATH, sb.ToString(), Encoding.UTF8);
                Debug.LogError(e);
                if (isBatch) EditorApplication.Exit(1);
            }
        }

        private static void MaintainMtl(StringBuilder sb)
        {
            string src = "C:/Users/BRUNO/Desktop/Baixados/Cenário/materials.mtl";
            if (!File.Exists(MTL_PATH) && File.Exists(src)) File.Copy(src, MTL_PATH, true);
            sb.AppendLine("MTL presente: " + File.Exists(MTL_PATH));
        }

        private static Dictionary<string, Material> ConvertToUrp(GameObject model, StringBuilder sb)
        {
            foreach (var old in Directory.GetFiles(MAT_DIR, "*.mat"))
                AssetDatabase.DeleteAsset(old.Replace('\\', '/'));
            AssetDatabase.Refresh();

            var map = new Dictionary<string, Material>();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var sm = r.sharedMaterials;
                for (int i = 0; i < sm.Length; i++)
                {
                    var src = sm[i];
                    if (src == null) continue;
                    if (!map.TryGetValue(src.name, out var urp))
                    {
                        Color col = src.HasProperty("_Color") ? src.GetColor("_Color") : Color.gray;
                        string path = $"{MAT_DIR}/{src.name}.mat";
                        urp = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (urp == null)
                        {
                            urp = new Material(shader) { name = src.name };
                            AssetDatabase.CreateAsset(urp, path);
                        }
                        urp.shader = shader;
                        urp.SetColor("_BaseColor", col);
                        urp.SetFloat("_Smoothness", 0.12f);
                        EditorUtility.SetDirty(urp);
                        map[src.name] = urp;
                        sb.AppendLine($"  {src.name} -> {col}");
                    }
                    sm[i] = urp;
                }
                r.sharedMaterials = sm;
            }
            AssetDatabase.SaveAssets();
            return map;
        }

        private static bool IsCloudPiece(Transform p)
        {
            var rs = p.GetComponentsInChildren<MeshRenderer>();
            if (rs.Length == 0) return false;
            foreach (var r in rs)
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || !m.name.Contains("mat21")) return false;
                }
            return true;
        }

        private static bool IsRockPiece(Transform p)
        {
            foreach (var r in p.GetComponentsInChildren<MeshRenderer>())
                foreach (var m in r.sharedMaterials)
                    if (m != null && (m.name.Contains("mat16") || m.name.Contains("mat17")))
                        return true;
            return false;
        }

        private static GameObject InstantiatePiece(Transform piece, Transform parent, Vector3 pos, Quaternion rot)
        {
            var wrapper = new GameObject("Piece");
            if (parent != null) wrapper.transform.SetParent(parent, false);
            wrapper.transform.position = pos;
            wrapper.transform.rotation = rot;

            var copy = UnityEngine.Object.Instantiate(piece.gameObject, wrapper.transform);
            copy.name = piece.name;
            var b = BoundsOf(piece.gameObject);
            copy.transform.localPosition = -b.center + Vector3.up * b.extents.y;
            copy.transform.localRotation = Quaternion.identity;
            copy.transform.localScale = Vector3.one;

            return wrapper;
        }

        private static Bounds BoundsOf(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        private static void CaptureArena(StringBuilder sb)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var cgo = new GameObject("_CapCam");
                cam = cgo.AddComponent<Camera>();
            }
            cam.transform.position = new Vector3(0f, 15f, -24f);
            cam.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
            cam.fieldOfView = 60f;

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
            Directory.CreateDirectory("docs/screens/cenario");
            File.WriteAllBytes("docs/screens/cenario/10_arena_decorada.png", tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(rt);
            sb.AppendLine("Captura: docs/screens/cenario/10_arena_decorada.png");
        }
    }
}
