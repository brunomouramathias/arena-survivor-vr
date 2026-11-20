#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine.AI;
using ArenaSurvivor.Enemies;

namespace ArenaSurvivor.Editor
{
    // Pinta skins de verdade (texturas) direto sobre as UVs reais das malhas.
    // Cada vértice é classificado pelo OSSO dominante (head/torso/arm/leg/hand/foot),
    // então a pintura segue a anatomia mesmo com os braços erguidos (pose de mago).
    // Aplica nos 3 monstros + no Mago e deixa o poder do mago AZUL.
    public static class CharacterSkinBaker
    {
        const string PREFABS = "Assets/_Project/Prefabs";
        const string MAT = "Assets/_Project/Materials";
        const string TEX = "Assets/_Project/Textures/Skins";
        const int N = 1024;
        static readonly StringBuilder sb = new StringBuilder();

        enum Part { Head, Face, Torso, Arm, Hand, Leg, Foot }

        [MenuItem("Tools/Arena/Bake Character Skins")]
        public static void Run()
        {
            sb.AppendLine("=== BAKE CHARACTER SKINS ===");
            Directory.CreateDirectory(TEX);

            // ---------- monstros (zumbis) ----------
            string err;
            var red = Bake($"{PREFABS}/Enemy_Chaser.prefab",
                (part, p) => MonsterSkin(part, p, new Color(0.72f, 0.16f, 0.13f), new Color(0.34f, 0.07f, 0.06f), 1.00f),
                "monster_red.png", out err);
            if (red == null) { Debug.LogError(err); File.WriteAllText("docs/screens/diag/skinbake.txt", sb.ToString()); EditorApplication.Exit(2); return; }

            var orange = Bake($"{PREFABS}/Enemy_Shooter.prefab",
                (part, p) => MonsterSkin(part, p, new Color(0.88f, 0.46f, 0.10f), new Color(0.42f, 0.18f, 0.05f), 1.02f),
                "monster_orange.png", out err);
            var purple = Bake($"{PREFABS}/Enemy_Stalker.prefab",
                (part, p) => MonsterSkin(part, p, new Color(0.36f, 0.16f, 0.46f), new Color(0.15f, 0.06f, 0.22f), 0.95f),
                "monster_purple.png", out err);

            // ---------- mago ----------
            var robe = Bake($"{PREFABS}/Enemy_Boss.prefab", MageRobe, "mage_robe.png", out err);

            // ---------- materiais ----------
            ApplyTex($"{MAT}/Mat_EnemyChaser.mat", red);
            ApplyTex($"{MAT}/Mat_EnemyShooter.mat", orange);
            ApplyTex($"{MAT}/Mat_Stalker_Skin.mat", purple);
            ApplyTex($"{MAT}/Mat_Boss_Mage.mat", robe);
            sb.AppendLine("[MAT] skins aplicadas aos 4 materiais");

            // ---------- poder azul do mago ----------
            var projMat = AssetDatabase.LoadAssetAtPath<Material>($"{MAT}/Mat_ProjMagicBoss.mat");
            projMat.SetColor("_BaseColor", new Color(0.25f, 0.55f, 1f));
            projMat.SetColor("_Color", new Color(0.25f, 0.55f, 1f));
            projMat.SetColor("_EmissionColor", new Color(0.4f, 1.6f, 4.5f, 3f));
            EnableEmission(projMat);
            sb.AppendLine("[BLUE] Mat_ProjMagicBoss azul emissivo");

            SetBossWarning($"{PREFABS}/Enemy_Boss.prefab");

            // ---------- movimento mais natural ----------
            PolishAgents();
            PolishAnimator();

            AssetDatabase.SaveAssets();
            File.WriteAllText("docs/screens/diag/skinbake.txt", sb.ToString(), Encoding.UTF8);
            Debug.Log(sb.ToString());
            EditorApplication.Exit(0);
        }

        // =================================================================
        static Texture2D Bake(string prefabPath, System.Func<Part, Vector3, Color> colorFn, string fileName, out string error)
        {
            error = null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) { error = "prefab ausente " + prefabPath; return null; }

            SkinnedMeshRenderer surf = null;
            foreach (var r in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.name == "Beta_Surface") surf = r;
            if (surf == null) { error = "Beta_Surface não encontrada em " + prefabPath; return null; }

            var mesh = surf.sharedMesh;
            var verts = mesh.vertices; var uvs = mesh.uv; var tris = mesh.triangles;

            // --- classificação GEOMÉTRICA: vértice -> osso mais próximo (por nome,
            // imune a qualquer desordem entre boneWeights e o array de ossos) ---
            var visual = surf.transform;
            var keyBones = new List<(Part part, Vector3 pos)>();
            var map = new Dictionary<string, Part>
            {
                { "Head", Part.Head }, { "HeadTop_End", Part.Head },
                { "Neck", Part.Torso }, { "Spine", Part.Torso }, { "Spine1", Part.Torso }, { "Spine2", Part.Torso }, { "Hips", Part.Torso },
                { "LeftShoulder", Part.Arm }, { "LeftArm", Part.Arm }, { "LeftForeArm", Part.Arm },
                { "RightShoulder", Part.Arm }, { "RightArm", Part.Arm }, { "RightForeArm", Part.Arm },
                { "LeftHand", Part.Hand }, { "RightHand", Part.Hand },
                { "LeftUpLeg", Part.Leg }, { "LeftLeg", Part.Leg },
                { "RightUpLeg", Part.Leg }, { "RightLeg", Part.Leg },
                { "LeftFoot", Part.Foot }, { "LeftToeBase", Part.Foot },
                { "RightFoot", Part.Foot }, { "RightToeBase", Part.Foot },
            };

            // Posição de CADA osso no espaço da MALHA, via bindpose:
            // bindposes[i] muda espaço-malha -> espaço-osso; a inversa leva a
            // origem do osso de volta ao espaço da malha. Imune à pose atual.
            var bind = surf.sharedMesh.bindposes;
            var boneList = surf.bones;
            for (int i = 0; i < boneList.Length && i < bind.Length; i++)
            {
                var b = boneList[i];
                if (b == null) continue;
                string n = b.name.StartsWith("mixamorig:") ? b.name.Substring("mixamorig:".Length) : b.name;
                if (map.TryGetValue(n, out var part))
                {
                    Vector3 pos = bind[i].inverse.MultiplyPoint3x4(Vector3.zero);
                    keyBones.Add((part, pos));
                }
            }
            sb.AppendLine($"  [BONES] {prefab.name}: {keyBones.Count} ossos-chave");

            // --- calibra o espaço: vértices podem estar no espaço do rootBone
            // (quadril) ou no espaço do visual. Testa os dois e escolhe o que
            // dá menor distância média vértice->osso. ---
            var root = surf.rootBone != null ? surf.rootBone : visual;
            var bonesVisual = new Vector3[keyBones.Count];
            var bonesRoot = new Vector3[keyBones.Count];
            for (int k = 0; k < keyBones.Count; k++)
            {
                bonesVisual[k] = keyBones[k].pos;
                bonesRoot[k] = root.InverseTransformPoint(visual.TransformPoint(keyBones[k].pos));
            }
            float MeanDist(Vector3[] bones)
            {
                float acc = 0f;
                foreach (var v in verts)
                {
                    float best = float.MaxValue;
                    foreach (var b in bones) best = Mathf.Min(best, (v - b).sqrMagnitude);
                    acc += Mathf.Sqrt(best);
                }
                return acc / verts.Length;
            }
            float dVisual = MeanDist(bonesVisual);
            float dRoot = MeanDist(bonesRoot);
            var bonesFinal = dRoot < dVisual ? bonesRoot : bonesVisual;
            sb.AppendLine($"  [SPACE] {prefab.name}: meanVisual={dVisual:F3} meanRoot={dRoot:F3} -> {(dRoot < dVisual ? "ROOT-BONE space" : "VISUAL space")}");
            if (Mathf.Min(dVisual, dRoot) > 0.2f) sb.AppendLine($"  [WARN] distância média alta — verificar bindpose");
            sb.AppendLine($"  [MESH] {prefab.name}: bounds={mesh.bounds.center.ToString("F2")}/{mesh.bounds.size.ToString("F2")} visualPos={visual.localPosition.ToString("F2")} visualScale={visual.lossyScale.ToString("F2")} root={root.name} rootPos={root.position.ToString("F2")}");

            var parts = new Part[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                float best = float.MaxValue; Part bestPart = Part.Torso;
                for (int k = 0; k < bonesFinal.Length; k++)
                {
                    float d = (verts[i] - bonesFinal[k]).sqrMagnitude;
                    if (d < best) { best = d; bestPart = keyBones[k].part; }
                }
                parts[i] = bestPart;
            }

            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var col = new Color[N * N];
            var mask = new bool[N * N];

            // histograma das partes (debug da classificação)
            var hist = new Dictionary<Part, int>();
            foreach (var pt in parts) hist[pt] = hist.TryGetValue(pt, out var c0) ? c0 + 1 : 1;
            sb.AppendLine($"  [PARTS] {prefab.name}: " + string.Join(", ", new[] { Part.Head, Part.Face, Part.Torso, Part.Arm, Part.Hand, Part.Leg, Part.Foot }.Select(k => $"{k}={(hist.TryGetValue(k, out var v) ? v : 0)}")));

            for (int t = 0; t < tris.Length; t += 3)
            {
                int i0 = tris[t], i1 = tris[t + 1], i2 = tris[t + 2];
                Vector2 a = uvs[i0] * N, b = uvs[i1] * N, c = uvs[i2] * N;
                Vector3 pa = verts[i0], pb = verts[i1], pc = verts[i2];
                Part part = parts[i0];

                float minX = Mathf.Max(0, Mathf.Floor(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - 1));
                float maxX = Mathf.Min(N - 1, Mathf.Ceil(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) + 1));
                float minY = Mathf.Max(0, Mathf.Floor(Mathf.Min(a.y, Mathf.Min(b.y, c.y)) - 1));
                float maxY = Mathf.Min(N - 1, Mathf.Ceil(Mathf.Max(a.y, Mathf.Max(b.y, c.y)) + 1));

                float den = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                if (Mathf.Abs(den) < 1e-6f) continue;

                for (int py = (int)minY; py <= (int)maxY; py++)
                for (int px = (int)minX; px <= (int)maxX; px++)
                {
                    Vector2 p = new Vector2(px + 0.5f, py + 0.5f);
                    float w0 = ((b.y - c.y) * (p.x - c.x) + (c.x - b.x) * (p.y - c.y)) / den;
                    float w1 = ((c.y - a.y) * (p.x - c.x) + (a.x - c.x) * (p.y - c.y)) / den;
                    float w2 = 1f - w0 - w1;
                    if (w0 < -0.04f || w1 < -0.04f || w2 < -0.04f) continue;
                    w0 = Mathf.Clamp01(w0); w1 = Mathf.Clamp01(w1); w2 = Mathf.Clamp01(w2);
                    var pos = pa * w0 + pb * w1 + pc * w2;
                    col[py * N + px] = colorFn(part, pos);
                    mask[py * N + px] = true;
                }
            }

            // 1 passe de dilatação para esconder costuras entre ilhas de UV
            {
                var fill = new List<int>();
                for (int i = 0; i < N * N; i++)
                {
                    if (mask[i]) continue;
                    int x = i % N; Color acc = default; int n = 0;
                    foreach (var o in new[] { -1, 1, -N, N })
                    {
                        int j = i + o;
                        if (j < 0 || j >= N * N) continue;
                        if ((o == -1 && x == 0) || (o == 1 && x == N - 1)) continue;
                        if (mask[j]) { acc += col[j]; n++; }
                    }
                    if (n > 0) { fill.Add(i); col[i] = acc / n; }
                }
                foreach (var i in fill) mask[i] = true;
            }

            tex.SetPixels(col);
            tex.Apply();
            var path = $"{TEX}/{fileName}";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path);

            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.sRGBTexture = true;
            ti.mipmapEnabled = true;
            ti.maxTextureSize = 1024;
            ti.SaveAndReimport();

            sb.AppendLine($"[BAKE] {fileName}: tris={tris.Length / 3} verts={verts.Length} pref={prefab.name}");
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }


        // ---------------- funções de pintura ----------------
        static float Hash(float x, float y, float z)
        {
            return Mathf.Abs(Mathf.Sin(x * 12.9898f + y * 78.233f + z * 37.719f) * 43758.5453f) % 1f;
        }
        static float Noise(Vector3 p, float cell)
        {
            return Hash(Mathf.Floor(p.x / cell), Mathf.Floor(p.y / cell), Mathf.Floor(p.z / cell));
        }

        static Color MonsterSkin(Part part, Vector3 p, Color skin, Color dark, float bright)
        {
            Color c;
            switch (part)
            {
                case Part.Head:
                    c = skin * 0.82f;
                    if (p.z > 0.10f) c = Color.Lerp(c, c * 1.4f, 0.5f);   // focinho/face
                    break;
                case Part.Hand:
                    c = skin * 0.55f;                                     // garras escuras
                    break;
                case Part.Foot:
                    c = dark * 1.1f;                                      // pés de monstro
                    break;
                case Part.Leg:
                    c = Color.Lerp(dark, skin, 0.55f);
                    break;
                default: // torso
                    c = skin;
                    if (p.z > 0.12f) c = Color.Lerp(c, c * 1.25f, 0.5f);  // barriga clara
                    if (p.z < -0.15f) c *= 0.85f;                          // dorso escuro
                    break;
            }

            // pele podre: manchas + veias
            float m = Noise(p, 0.06f);
            c = Color.Lerp(c, c * 0.72f, Mathf.SmoothStep(0.55f, 1f, m) * 0.5f);
            float vein = Mathf.Sin(p.y * 90f + p.x * 55f) * Mathf.Sin(p.z * 75f + p.y * 30f);
            c *= 1f - Mathf.Clamp01(vein) * 0.10f;

            // grão fino
            c *= 1f + (Hash(p.x * 61f, p.y * 53f, p.z * 47f) - 0.5f) * 0.10f;
            return c * bright;
        }

        static Color MageRobe(Part part, Vector3 p)
        {
            Color navy = new Color(0.11f, 0.13f, 0.34f);
            Color robe = new Color(0.15f, 0.19f, 0.47f);
            Color gold = new Color(0.82f, 0.63f, 0.22f);
            Color skin = new Color(0.84f, 0.63f, 0.48f);
            Color hood = new Color(0.07f, 0.08f, 0.24f);
            Color blueGlow = new Color(0.25f, 0.62f, 1.00f);
            Color c;

            switch (part)
            {
                case Part.Head:
                    c = hood;
                    if (p.z > 0.08f) c = skin;                            // rosto
                    break;
                case Part.Hand:
                    c = navy * 0.92f;                                     // luvas do manto
                    break;
                case Part.Arm:
                    c = navy * 0.9f;                                      // mangas
                    break;
                case Part.Leg:
                case Part.Foot:
                    c = navy * 0.8f;                                      // manto cobre as pernas
                    if (part == Part.Foot) c = hood;                      // botas escuras
                    // barra com brilho azul
                    if (p.y < 0.18f) c = Color.Lerp(c, blueGlow, 0.55f);
                    // estrelinhas arcanas
                    float ang2 = Mathf.Atan2(p.z, p.x);
                    if (Noise(new Vector3(ang2 * 2.2f, p.y * 6f, 7f), 0.5f) > 0.86f)
                        c = Color.Lerp(c, blueGlow * 1.4f, 0.5f);
                    break;
                default: // torso
                    float ang = Mathf.Atan2(p.z, p.x);
                    float panel = 0.5f + 0.5f * Mathf.Sin(ang * 10f);
                    c = Color.Lerp(robe * 0.85f, robe, panel * 0.6f);
                    if (p.y > 1.30f && p.y < 1.40f) c = gold;              // colar dourado fino
                    if (p.y > 0.98f && p.y < 1.06f) c = gold * 0.8f;       // cinto
                    break;
            }

            // sombra geral por altura + grão
            c *= 1f - Mathf.Clamp01(1.6f - p.y) * 0.15f;
            c *= 1f + (Hash(p.x * 71f, p.y * 43f, p.z * 29f) - 0.5f) * 0.07f;
            return c;
        }

        static void ApplyTex(string matPath, Texture2D tex)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            m.SetTexture("_BaseMap", tex);
            m.SetTexture("_MainTex", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetColor("_Color", Color.white);
            m.SetFloat("_Smoothness", 0.28f);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
        }

        static void EnableEmission(Material m)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
        }

        static void SetBossWarning(string prefabPath)
        {
            // material azul de aviso
            string matPath = $"{MAT}/Mat_Warning_Blue.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(matPath) == null)
            {
                var mat = new Material(AssetDatabase.LoadAssetAtPath<Material>($"{MAT}/Mat_Warning.mat"));
                mat.name = "Mat_Warning_Blue";
                mat.SetColor("_BaseColor", new Color(0.15f, 0.45f, 1f));
                mat.SetColor("_Color", new Color(0.15f, 0.45f, 1f));
                mat.SetColor("_EmissionColor", new Color(0.3f, 1.4f, 4f, 3f));
                EnableEmission(mat);
                AssetDatabase.CreateAsset(mat, matPath);
                sb.AppendLine("[BLUE] Mat_Warning_Blue criada");
            }

            // prefab azul de aviso
            string bluePath = $"{PREFABS}/SpawnWarning_Blue.prefab";
            var warn = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFABS}/SpawnWarning.prefab");
            if (warn != null && AssetDatabase.LoadAssetAtPath<GameObject>(bluePath) == null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(warn);
                PrefabUtility.SaveAsPrefabAsset(inst, bluePath);
                Object.DestroyImmediate(inst);
                using (var s2 = new PrefabUtility.EditPrefabContentsScope(bluePath))
                {
                    var blueMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    foreach (var r in s2.prefabContentsRoot.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats = r.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++) mats[i] = blueMat;
                        r.sharedMaterials = mats;
                    }
                }
            }

            // liga no boss + movimento suave
            using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
            {
                var root = scope.prefabContentsRoot;
                var boss = root.GetComponent<BossController>();
                var so = new SerializedObject(boss);
                var p = so.FindProperty("aoeWarningPrefab");
                var blueWarn = AssetDatabase.LoadAssetAtPath<GameObject>(bluePath);
                if (p != null && blueWarn != null) p.objectReferenceValue = blueWarn;
                so.ApplyModifiedPropertiesWithoutUndo();

                var agent = root.GetComponent<NavMeshAgent>();
                if (agent != null) { agent.acceleration = 14f; agent.angularSpeed = 480f; }
            }
            sb.AppendLine("[BOSS] nova azul (SpawnWarning_Blue) ligada + agent suavizado");
        }

        static void PolishAgents()
        {
            foreach (var n in new[] { "Enemy_Chaser", "Enemy_Shooter", "Enemy_Stalker" })
            {
                using (var scope = new PrefabUtility.EditPrefabContentsScope($"{PREFABS}/{n}.prefab"))
                {
                    var agent = scope.prefabContentsRoot.GetComponent<NavMeshAgent>();
                    if (agent != null) { agent.acceleration = 14f; agent.angularSpeed = 480f; }
                }
            }
            sb.AppendLine("[AGENT] aceleração 14 / rotação 480°/s nos 3 monstros");
        }

        static void PolishAnimator()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/_Project/Animations/Zombie/AnimatorController_Zombie.controller");
            if (ctrl == null) return;
            foreach (var layer in ctrl.layers)
            foreach (var st in layer.stateMachine.states)
            {
                if (st.state.name == "Walk") st.state.speed = 1.1f;
                if (st.state.name == "Run") st.state.speed = 1.15f;
            }
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            sb.AppendLine("[ANIM] Walk 1.1x / Run 1.15x");
        }
    }
}
#endif
