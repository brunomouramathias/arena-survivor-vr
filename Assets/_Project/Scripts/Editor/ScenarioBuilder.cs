using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ArenaSurvivor.World;
using ArenaSurvivor.Waves;

namespace ArenaSurvivor.Editor
{
    // Constrói o cenário completo com o pacote Forest (AurynSky):
    // torres destrutíveis, castelos, árvores, pedras, grama, barris, fogueiras e bandeiras.
    public static class ScenarioBuilder
    {
        const string ARENA = "Assets/_Project/Scenes/Arena.unity";
        const string PACK  = "Assets/AurynSky/Forest Pack";
        const string LOG   = "docs/scenario_log.txt";

        const string TOWER   = PACK + "/Prefabs/Prefabs_Blue/ForestTower_Blue.prefab";
        const string CASTLE  = PACK + "/Prefabs/Prefabs_Blue/ForestCastle_Blue.prefab";
        const string BRAZIER = PACK + "/Prefabs/Prefabs_Blue/ForestBrazzierBlue.prefab";
        const string BANNER  = PACK + "/Prefabs/Prefabs_Blue/ForestBanner_Blue.prefab";
        const string SMOKE   = PACK + "/Prefabs/Smoke.prefab";

        static readonly string[] TREES =
        {
            PACK + "/Prefabs/ForestTreePineTall.prefab",
            PACK + "/Prefabs/ForestTreePineShort.prefab",
            PACK + "/Prefabs/ForestTreeDTall.prefab",
            PACK + "/Prefabs/ForestTreeDShort.prefab",
            PACK + "/Prefabs/ForestTreeAppleTall.prefab",
            PACK + "/Prefabs/ForestTreeAppleShort.prefab",
        };
        static readonly string[] ROCKS =
        {
            PACK + "/Prefabs/ForestBlock01.prefab",
            PACK + "/Prefabs/ForestBlock02.prefab",
        };
        static readonly string[] GRASS =
        {
            PACK + "/Prefabs/ForestGrass01.prefab",
            PACK + "/Prefabs/ForestGrass02.prefab",
        };

        [MenuItem("Tools/Arena/Build Scenario (Forest Pack)", priority = 21)]
        public static void Run() { RunInternal(false); }

        public static void RunBatch() { RunInternal(true); }

        private static void RunInternal(bool isBatch)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== [SCENARIO] " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");
            try
            {
                ConvertPackMaterials(sb);

                var scene = EditorSceneManager.OpenScene(ARENA, OpenSceneMode.Single);

                // ---------- remove cenário antigo ----------
                int removed = 0;
                foreach (var name in new[] { "--- Obstacles ---", "--- Nature_Perimeter ---", "--- Cenario ---" })
                {
                    var go = GameObject.Find(name);
                    if (go != null) { UnityEngine.Object.DestroyImmediate(go); removed++; }
                }
                foreach (var go in scene.GetRootGameObjects())
                {
                    if (go.name.StartsWith("Forest_")) { UnityEngine.Object.DestroyImmediate(go); removed++; }
                }
                sb.AppendLine($"Containers antigos removidos: {removed}");

                var root = new GameObject("--- Cenario ---").transform;
                var towersRoot = new GameObject("Torres").transform; towersRoot.SetParent(root, false);
                var treesRoot = new GameObject("Arvores").transform; treesRoot.SetParent(root, false);
                var rocksRoot = new GameObject("Pedras").transform; rocksRoot.SetParent(root, false);
                var grassRoot = new GameObject("Grama").transform; grassRoot.SetParent(root, false);
                var propsRoot = new GameObject("Props").transform; propsRoot.SetParent(root, false);
                var outsideRoot = new GameObject("Fora_das_Muralhas").transform; outsideRoot.SetParent(root, false);

                // ---------- 4 torres pequenas em volta do jogador (centro) ----------
                var towerPos = new[]
                {
                    new Vector3(-3.7f, 0f, -3.7f),
                    new Vector3( 3.7f, 0f, -3.7f),
                    new Vector3(-3.7f, 0f,  3.7f),
                    new Vector3( 3.7f, 0f,  3.7f),
                };
                var smoke = AssetDatabase.LoadAssetAtPath<GameObject>(SMOKE);
                foreach (var p in towerPos)
                {
                    var tower = Place(TOWER, p, 2.6f, Quaternion.LookRotation(-p.normalized, Vector3.up), towersRoot);
                    tower.name = "Torre_Defensiva";
                    tower.tag = "Obstacle";
                    AddBoxCollider(tower, 0.8f);
                    MakeStatic(tower);

                    var th = tower.AddComponent<TowerHealth>();
                    th.Configure(300, smoke);
                    sb.AppendLine($"Torre pequena em {p} (vida 300)");
                }

                // ---------- castelos fora das muralhas (cenário de fundo) ----------
                foreach (var p in new[] {
                    new Vector3(-27f, 0f, -27f), new Vector3(27f, 0f, -27f),
                    new Vector3(-27f, 0f, 27f),  new Vector3(27f, 0f, 27f) })
                {
                    var c = Place(CASTLE, p, 11f, Quaternion.LookRotation(-p.normalized, Vector3.up), outsideRoot);
                    c.name = "Castelo_Fora";
                    MakeStatic(c);
                }

                // ---------- cobertura interna natural (caixotes, barris e moitas) ----------
                var rnd = new System.Random(7);
                var coverSpots = new[]
                {
                    new Vector3(-9f, 0f, -9f), new Vector3(9f, 0f, -9f),
                    new Vector3(-9f, 0f,  9f), new Vector3(9f, 0f,  9f),
                    new Vector3( 0f, 0f, -12f), new Vector3(0f, 0f, 12f),
                };
                foreach (var p in coverSpots)
                {
                    var crate = Place(PACK + "/Prefabs/ForestCrate.prefab", p, 1.1f,
                                      Quaternion.Euler(0f, rnd.Next(0, 360), 0f), propsRoot);
                    crate.name = "Caixote_Cobertura";
                    crate.tag = "Obstacle";
                    AddBoxCollider(crate, 0.85f);
                    MakeStatic(crate);

                    var barrel = Place(PACK + "/Prefabs/ForestBarrel.prefab", p + new Vector3(1.1f, 0f, 0.6f), 1.0f,
                                       Quaternion.Euler(0f, rnd.Next(0, 360), 0f), propsRoot);
                    barrel.name = "Barril";
                    barrel.tag = "Obstacle";
                    AddBoxCollider(barrel, 0.8f);
                    MakeStatic(barrel);
                }

                // moitas internas (cobertura baixa, não bloqueiam tiro)
                var bushSpots = new[]
                {
                    new Vector3(-16f, 0f, 3f), new Vector3(16f, 0f, -3f),
                    new Vector3(3f, 0f, -16f), new Vector3(-3f, 0f, 16f),
                    new Vector3(-8f, 0f, 12f), new Vector3(8f, 0f, -12f),
                };
                foreach (var p in bushSpots)
                {
                    var bush = Place(TREES[5], p, 1.6f, Quaternion.Euler(0f, rnd.Next(0, 360), 0f), grassRoot);
                    bush.name = "Moita";
                }

                // ---------- árvores: anel externo (fora das muralhas) ----------
                for (int i = 0; i < 26; i++)
                {
                    float ang = i * (360f / 26f) + (float)(rnd.NextDouble() * 6 - 3);
                    float r = 25.5f + (float)rnd.NextDouble() * 4.5f;
                    var p = new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad) * r, 0f, Mathf.Sin(ang * Mathf.Deg2Rad) * r);
                    var tree = Place(TREES[rnd.Next(TREES.Length)], p, 5.5f + (float)rnd.NextDouble() * 3.5f,
                                     Quaternion.Euler(0f, rnd.Next(0, 360), 0f), treesRoot);
                    tree.name = "Arvore_Externa";
                    MakeStatic(tree);
                }

                // ---------- árvores internas: cobertura nos cantos (sem bloquear o centro) ----------
                var innerTrees = new[]
                {
                    new Vector3(-18f, 0f, -4f), new Vector3(18f, 0f, 4f),
                    new Vector3(-4f, 0f, 18f), new Vector3(4f, 0f, -18f),
                    new Vector3(-18f, 0f, 9f), new Vector3(18f, 0f, -9f),
                };
                foreach (var p in innerTrees)
                {
                    var tree = Place(TREES[rnd.Next(TREES.Length)], p, 4.5f + (float)rnd.NextDouble() * 1.5f,
                                     Quaternion.Euler(0f, rnd.Next(0, 360), 0f), treesRoot);
                    tree.name = "Arvore_Interna";
                    tree.tag = "Obstacle";
                    AddCapsuleCollider(tree);
                    MakeStatic(tree);
                }

                // ---------- grama ----------
                for (int i = 0; i < 90; i++)
                {
                    float ang = (float)rnd.NextDouble() * 360f;
                    float r = 4f + (float)rnd.NextDouble() * 15.5f;
                    var p = new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad) * r, 0f, Mathf.Sin(ang * Mathf.Deg2Rad) * r);
                    if (p.magnitude < 3.5f) continue;
                    var g = Place(GRASS[rnd.Next(GRASS.Length)], p, 0.35f + (float)rnd.NextDouble() * 0.45f,
                                  Quaternion.Euler(0f, rnd.Next(0, 360), 0f), grassRoot);
                    g.name = "Grama";
                }

                // ---------- braseiros centrais (luz + fogo) ----------
                foreach (var p in new[] { new Vector3(-9f, 0f, 0f), new Vector3(9f, 0f, 0f) })
                {
                    var b = Place(BRAZIER, p, 1.5f, Quaternion.identity, propsRoot);
                    b.name = "Braseiro";
                    var fire = Place(PACK + "/Prefabs/Fire.prefab", p + Vector3.up * 1.1f, 0.7f, Quaternion.identity, propsRoot);
                    fire.name = "Fogo";
                    var lightGO = new GameObject("Luz_Braseiro");
                    lightGO.transform.SetParent(b.transform, false);
                    lightGO.transform.localPosition = Vector3.up * 1.4f;
                    var l = lightGO.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = new Color(1f, 0.6f, 0.25f);
                    l.intensity = 2.2f;
                    l.range = 7f;
                }

                // ---------- spawner junto às muralhas ----------
                var spawner = UnityEngine.Object.FindAnyObjectByType<WaveSpawner>();
                if (spawner != null)
                {
                    var so = new SerializedObject(spawner);
                    var ar = so.FindProperty("arenaRadius");
                    var mp = so.FindProperty("minPlayerDist");
                    if (ar != null) ar.floatValue = 21.2f;
                    if (mp != null) mp.floatValue = 11f;

                    // Reatribui os prefabs (referências antigas apontavam para fileIDs quebrados)
                    AssignPrefab(so, "chaserPrefab", "Assets/_Project/Prefabs/Enemy_Chaser.prefab", sb);
                    AssignPrefab(so, "shooterPrefab", "Assets/_Project/Prefabs/Enemy_Shooter.prefab", sb);
                    AssignPrefab(so, "bossPrefab", "Assets/_Project/Prefabs/Enemy_Boss.prefab", sb);
                    AssignPrefab(so, "spawnWarningPrefab", "Assets/_Project/Prefabs/SpawnWarning.prefab", sb);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    sb.AppendLine("WaveSpawner: spawn junto às muralhas (raio 21.2) e prefabs reatribuídos.");
                }

                // ---------- desativa o HUD antigo (TMP com barra 100/100) ----------
                var legacyHud = GameObject.Find("Canvas_HUD");
                if (legacyHud != null)
                {
                    legacyHud.SetActive(false);
                    sb.AppendLine("HUD antigo (Canvas_HUD) desativado; HUD novo = corações + onda + pontos.");
                }

                // ---------- HUD de corações + Game Over ----------
                var oldHud = GameObject.Find("GameHUD");
                if (oldHud != null) UnityEngine.Object.DestroyImmediate(oldHud);
                var hudGO = new GameObject("GameHUD");
                hudGO.AddComponent<ArenaSurvivor.UI.GameHUD>();

                var ph = UnityEngine.Object.FindAnyObjectByType<ArenaSurvivor.Player.PlayerHealth>();
                if (ph != null)
                {
                    var pso = new SerializedObject(ph);
                    var mh = pso.FindProperty("maxHealth");
                    if (mh != null) mh.intValue = 3;
                    pso.ApplyModifiedPropertiesWithoutUndo();
                    sb.AppendLine("PlayerHealth.maxHealth = 3 corações.");
                }

                // ---------- NavMesh ----------
                UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
                sb.AppendLine("NavMesh reconstruída.");

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

                Capture(sb);

                File.WriteAllText(LOG, sb.ToString(), Encoding.UTF8);
                Debug.Log(sb.ToString());
                if (isBatch) EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                sb.AppendLine("EXCEÇÃO: " + e);
                File.WriteAllText(LOG, sb.ToString(), Encoding.UTF8);
                Debug.LogError(e);
                if (isBatch) EditorApplication.Exit(1);
            }
        }

        // ------------------------------------------------------------------
        private static void ConvertPackMaterials(StringBuilder sb)
        {
            string dir = PACK + "/Materials";
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            int n = 0;
            foreach (var path in Directory.GetFiles(dir, "*.mat"))
            {
                string p = path.Replace('\\', '/');
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null) continue;
                if (m.shader == shader) continue;

                Texture tex = null;
                if (m.HasProperty("_MainTex")) tex = m.GetTexture("_MainTex");
                if (tex == null && m.HasProperty("_BaseMap")) tex = m.GetTexture("_BaseMap");
                Color col = Color.white;
                if (m.HasProperty("_Color")) col = m.GetColor("_Color");

                m.shader = shader;
                if (tex != null) m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", col);
                m.SetFloat("_Smoothness", 0.1f);
                m.SetFloat("_Metallic", 0f);
                EditorUtility.SetDirty(m);
                n++;
            }
            AssetDatabase.SaveAssets();
            sb.AppendLine($"Materiais do pacote convertidos para URP: {n}");
        }

        private static void AssignPrefab(SerializedObject so, string prop, string path, StringBuilder sb)
        {
            var p = so.FindProperty(prop);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (p != null && asset != null)
            {
                p.objectReferenceValue = asset;
                sb.AppendLine($"  {prop} -> {path}");
            }
            else sb.AppendLine($"  AVISO: {prop} não atribuído ({path})");
        }

        private static GameObject Place(string prefabPath, Vector3 pos, float targetHeight, Quaternion rot, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) throw new Exception("Prefab não encontrado: " + prefabPath);

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, rot);

            var b = BoundsOf(go);
            if (b.size.y > 0.001f && targetHeight > 0f)
            {
                float s = targetHeight / b.size.y;
                go.transform.localScale = go.transform.localScale * s;
            }

            // assenta a base no chão
            b = BoundsOf(go);
            go.transform.position += Vector3.up * (pos.y - b.min.y);
            return go;
        }

        private static Bounds BoundsOf(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        private static void AddBoxCollider(GameObject go, float widthFactor)
        {
            var b = BoundsOf(go);
            Vector3 ls = go.transform.lossyScale;
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(
                Mathf.Max(0.5f, b.size.x / Mathf.Max(0.001f, ls.x) * widthFactor),
                Mathf.Max(0.5f, b.size.y / Mathf.Max(0.001f, ls.y)),
                Mathf.Max(0.5f, b.size.z / Mathf.Max(0.001f, ls.z) * widthFactor));
            col.center = new Vector3(0f, col.size.y * 0.5f, 0f);
        }

        private static void AddCapsuleCollider(GameObject go)
        {
            var b = BoundsOf(go);
            Vector3 ls = go.transform.lossyScale;
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = Mathf.Max(0.25f, Mathf.Min(b.size.x / ls.x, b.size.z / ls.z) * 0.22f);
            col.height = Mathf.Max(1.5f, b.size.y / Mathf.Max(0.001f, ls.y));
            col.center = new Vector3(0f, col.height * 0.5f, 0f);
        }

        private static void MakeStatic(GameObject go)
        {
#if UNITY_EDITOR
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.NavigationStatic | StaticEditorFlags.BatchingStatic);
#endif
        }

        private static void Capture(StringBuilder sb)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var cgo = new GameObject("_CapCam");
                cam = cgo.AddComponent<Camera>();
            }
            cam.fieldOfView = 55f;

            Shot(cam, new Vector3(0f, 30f, -30f), Quaternion.Euler(45f, 0f, 0f), "docs/screens/cenario/20_cenario_top.png");
            Shot(cam, new Vector3(0f, 14f, -20f), Quaternion.Euler(32f, 0f, 0f), "docs/screens/cenario/21_cenario_game.png");
            Shot(cam, new Vector3(-8f, 5f, -8f), Quaternion.LookRotation(new Vector3(12.5f, 2f, 12.5f) - new Vector3(-8f, 5f, -8f)), "docs/screens/cenario/22_torre_close.png");
            sb.AppendLine("Capturas: 20_cenario_top.png, 21_cenario_game.png, 22_torre_close.png");
        }

        private static void Shot(Camera cam, Vector3 pos, Quaternion rot, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            cam.transform.position = pos;
            cam.transform.rotation = rot;
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
        }
    }
}
