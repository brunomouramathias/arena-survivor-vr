#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.AI;
using ArenaSurvivor.Enemies;

namespace ArenaSurvivor.Editor
{
    // Importa os 2 bruxos do pack Wizard_Free (Lowpo, licença aberta) e os usa
    // nos inimigos "mago": Fire_Wizard = Enemy_Boss, Air_Wizard = novo inimigo
    // Enemy_Sorcerer (caster de curta distância). Ambos com a textura original
    // do pack (skin de verdade), rig nativo e movimentos/ataques no Animator.
    public static class WizardPackInstaller
    {
        const string SRC = "Wizard_Free";                 // pasta solta no Desktop
        const string DEST_FBX = "Assets/_Project/ThirdParty/Wizard_Free/Characters";
        const string DEST_TEX = "Assets/_Project/Textures/Wizard_Free";
        const string MAT = "Assets/_Project/Materials";
        const string PREFABS = "Assets/_Project/Prefabs";
        const string ANIM = "Assets/_Project/Animations/Wizards";
        static readonly StringBuilder sb = new StringBuilder();

        [MenuItem("Tools/Arena/Install Wizard Pack")]
        public static void Run()
        {
            sb.AppendLine("=== INSTALL WIZARD PACK ===");
            string sourceDir = FindSourceDir();
            if (sourceDir == null) { Debug.LogError("Wizard_Free não encontrado no Desktop."); EditorApplication.Exit(2); return; }
            sb.AppendLine("[SRC] " + sourceDir);

            Directory.CreateDirectory(DEST_FBX);
            Directory.CreateDirectory(DEST_TEX);
            Directory.CreateDirectory(ANIM);
            Directory.CreateDirectory(ANIM + "/Clips");

            // ---------- 1. copiar FBX + textura ----------
            foreach (var fbx in new[] { "Fire_Wizard.fbx", "Air_Wizard.fbx" })
            {
                string dst = $"{DEST_FBX}/{fbx}";
                File.Copy(Path.Combine(sourceDir, "FBX/Characters", fbx), dst, true);
                AssetDatabase.ImportAsset(dst);
            }
            string texPath = $"{DEST_TEX}/Texture.png";
            File.Copy(Path.Combine(sourceDir, "Texture/Texture.png"), texPath, true);
            AssetDatabase.ImportAsset(texPath);
            var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
            ti.sRGBTexture = true;
            ti.SaveAndReimport();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            sb.AppendLine($"[COPY] FBXs + textura ({tex.width}x{tex.height})");

            // ---------- 2. import settings dos FBX ----------
            var models = new Dictionary<string, GameObject>();
            foreach (var n in new[] { "Fire_Wizard", "Air_Wizard" })
            {
                string p = $"{DEST_FBX}/{n}.fbx";
                var mi = (ModelImporter)AssetImporter.GetAtPath(p);
                mi.animationType = ModelImporterAnimationType.Generic;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.optimizeGameObjects = false;
                mi.importAnimation = true;
                mi.materialImportMode = ModelImporterMaterialImportMode.None; // criamos materiais URP manualmente
                mi.useFileScale = true;
                mi.SaveAndReimport();
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                models[n] = model;

                // lista clipes nativos
                var clips = AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")).ToList();
                sb.AppendLine($"[MODEL] {n}: clips=[{string.Join(", ", clips.Select(c => c.name + " " + c.length.ToString("0.00") + "s"))}]");
                if (clips.Count == 0) sb.AppendLine($"[WARN] {n}: sem animações embutidas");
            }

            // ---------- 3. materiais URP com a textura do pack ----------
            var matFire = MakeMaterial("Mat_Wizard_Fire", tex, new Color(1f, 0.45f, 0.25f));
            var matAir = MakeMaterial("Mat_Wizard_Air", tex, new Color(0.35f, 0.85f, 0.9f));
            sb.AppendLine("[MAT] Mat_Wizard_Fire / Mat_Wizard_Air criadas");

            // ---------- 4. Animator com Speed/Attack/CastSpell/Death ----------
            BuildAnimators(models);
            sb.AppendLine("[ANIM] controladores dos bruxos criados");

            // ---------- 5. prefabs ----------
            var projPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFABS}/Projectile_Enemy.prefab");
            var warnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFABS}/SpawnWarning.prefab");
            var minionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFABS}/Enemy_Chaser.prefab");

            BuildMageBoss(models["Fire_Wizard"], matFire, projPrefab, warnPrefab, minionPrefab);
            BuildSorcerer(models["Air_Wizard"], matAir, projPrefab);
            sb.AppendLine("[PREFAB] Enemy_Boss (Fire_Wizard) e Enemy_Sorcerer (Air_Wizard) prontos");

            // ---------- 6. spawner usa o novo inimigo ----------
            WireSorcererInScene();
            AssetDatabase.SaveAssets();

            File.WriteAllText("docs/screens/diag/wizard_install.txt", sb.ToString(), Encoding.UTF8);
            Debug.Log(sb.ToString());
            EditorApplication.Exit(0);
        }

        static string FindSourceDir()
        {
            foreach (var dir in new[]
            {
                "C:/Users/BRUNO/Desktop/Wizard_Free",
                "C:/Users/BRUNO/Desktop/Baixados/Wizard_Free",
            })
                if (Directory.Exists(dir)) return dir;
            return null;
        }

        static Material MakeMaterial(string name, Texture tex, Color tint)
        {
            string path = $"{MAT}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", tex);
            m.SetTexture("_MainTex", tex);
            m.SetColor("_BaseColor", tint);
            m.SetColor("_Color", tint);
            m.SetFloat("_Smoothness", 0.35f);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // =================================================================
        static void BuildAnimators(Dictionary<string, GameObject> models)
        {
            foreach (var kv in models)
            {
                string path = $"{ANIM}/AnimatorController_{kv.Key}.controller";
                var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
                ctrl.parameters = new AnimatorControllerParameter[0];
                ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
                ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
                ctrl.AddParameter("CastSpell", AnimatorControllerParameterType.Trigger);
                ctrl.AddParameter("Death", AnimatorControllerParameterType.Trigger);

                var sm = ctrl.layers[0].stateMachine;
                sm.states = new ChildAnimatorState[0];
                sm.anyStateTransitions = new AnimatorStateTransition[0];

                var clips = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(kv.Value))
                    .OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")).ToList();
                var idleClip = PickClip(clips, "idle", "Idle") ?? clips.FirstOrDefault();
                var walkClip = PickClip(clips, "walk", "run", "Walk", "Run") ?? idleClip;
                var atkClip = PickClip(clips, "attack", "cast", "spell", "Attack") ?? idleClip;

                var sIdle = sm.AddState("Idle");
                var sWalk = sm.AddState("Walk");
                var sAttack = sm.AddState("Attack");
                var sCast = sm.AddState("CastSpell");
                sIdle.motion = idleClip; sWalk.motion = walkClip; sAttack.motion = atkClip; sCast.motion = atkClip;
                sm.defaultState = sIdle;

                var t1 = sIdle.AddTransition(sWalk);
                t1.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
                var t2 = sWalk.AddTransition(sIdle);
                t2.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");
                var t3 = sm.AddAnyStateTransition(sAttack);
                t3.AddCondition(AnimatorConditionMode.If, 0, "Attack");
                t3.canTransitionToSelf = false;
                var t4 = sm.AddAnyStateTransition(sCast);
                t4.AddCondition(AnimatorConditionMode.If, 0, "CastSpell");
                t4.canTransitionToSelf = false;
                EditorUtility.SetDirty(ctrl);
                sb.AppendLine($"[CTRL] {kv.Key}: idle={(idleClip != null ? idleClip.name : "-")} walk={(walkClip != null ? walkClip.name : "-")} attack={(atkClip != null ? atkClip.name : "-")}");
            }
            AssetDatabase.SaveAssets();
        }

        static AnimationClip PickClip(List<AnimationClip> clips, params string[] keys)
        {
            foreach (var k in keys)
                foreach (var c in clips)
                    if (c.name.IndexOf(k, System.StringComparison.OrdinalIgnoreCase) >= 0) return c;
            return null;
        }

        // =================================================================
        static GameObject BuildMageBoss(GameObject model, Material mat, GameObject projPrefab, GameObject warnPrefab, GameObject minionPrefab)
        {
            string path = $"{PREFABS}/Enemy_Boss.prefab";

            using (var scope = new PrefabUtility.EditPrefabContentsScope(path))
            {
                var root = scope.prefabContentsRoot;

                // troca o visual pelo Fire_Wizard
                var old = root.transform.Find("Model_Mage") ?? root.transform.Find("Model_Zombie");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var visual = (GameObject)Object.Instantiate(model, root.transform);
                visual.name = "Model_Mage";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one * 1.35f; // chefe colossal

                foreach (var r in visual.GetComponentsInChildren<Renderer>())
                    r.sharedMaterial = mat;

                // olhos brilhando (mantém identidade do chefe)
                float h = 0f;
                foreach (var r in visual.GetComponentsInChildren<Renderer>())
                    h = Mathf.Max(h, r.bounds.size.y / 1.35f);
                AttachEyes(visual.transform, h, 0.075f, $"{MAT}/Mat_Boss_Eyes.mat");

                // collider/agent na altura real
                var col = root.GetComponent<CapsuleCollider>();
                if (col != null) { col.height = h * 1.35f + 0.4f; col.center = new Vector3(0, col.height / 2f, 0); col.radius = 0.6f; }
                var agent = root.GetComponent<NavMeshAgent>();
                if (agent != null) { agent.height = col.height; agent.radius = 0.6f; agent.acceleration = 14f; agent.angularSpeed = 480f; }

                // animator novo
                var anim = root.GetComponent<Animator>();
                anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{ANIM}/AnimatorController_Fire_Wizard.controller");
                anim.applyRootMotion = false;
                anim.avatar = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(model)).OfType<Avatar>().FirstOrDefault();

                // BossController: projéteis azuis + nova azul
                var boss = root.GetComponent<BossController>();
                if (boss != null)
                {
                    var so = new SerializedObject(boss);
                    var pProj = so.FindProperty("projectilePrefab");
                    var pWarn = so.FindProperty("aoeWarningPrefab");
                    var pMin = so.FindProperty("minionPrefab");
                    var pShoot = so.FindProperty("shootPoint");
                    if (pProj != null) pProj.objectReferenceValue = projPrefab;
                    if (pWarn != null) pWarn.objectReferenceValue = warnPrefab;
                    if (pMin != null) pMin.objectReferenceValue = minionPrefab;
                    var sp = FindDeep(root.transform, "ShootPoint");
                    if (sp != null && pShoot != null) pShoot.objectReferenceValue = sp;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            sb.AppendLine($"[BOSS] {path} atualizado (Fire_Wizard 1.35x)");
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static GameObject BuildSorcerer(GameObject model, Material mat, GameObject projPrefab)
        {
            string path = $"{PREFABS}/Enemy_Sorcerer.prefab";
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/_Project/ScriptableObjects/Enemies/EnemyData_Shooter.asset");

            AssetDatabase.DeleteAsset(path);
            var go = new GameObject("Enemy_Sorcerer");
            go.tag = "Enemy";

            var visual = (GameObject)Object.Instantiate(model, go.transform);
            visual.name = "Model_Sorcerer";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one;

            foreach (var r in visual.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = mat;

            float h = 0f;
            foreach (var r in visual.GetComponentsInChildren<Renderer>())
                h = Mathf.Max(h, r.bounds.size.y);
            AttachEyes(visual.transform, h, 0.05f, $"{MAT}/Mat_Shooter_Eyes.mat");

            var col = go.AddComponent<CapsuleCollider>();
            col.height = h + 0.3f; col.center = new Vector3(0, col.height / 2f, 0); col.radius = 0.4f;

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false;

            var agent = go.AddComponent<NavMeshAgent>();
            agent.speed = 2.4f; agent.stoppingDistance = 6.5f;
            agent.height = col.height; agent.radius = 0.4f;
            agent.acceleration = 14f; agent.angularSpeed = 480f;

            var anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{ANIM}/AnimatorController_Air_Wizard.controller");
            anim.applyRootMotion = false;
            anim.avatar = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(model)).OfType<Avatar>().FirstOrDefault();

            var spGO = new GameObject("ShootPoint");
            spGO.transform.SetParent(go.transform);
            spGO.transform.localPosition = new Vector3(0, h * 0.72f, 0.5f);

            var shooter = go.AddComponent<EnemyShooter>();
            var so = new SerializedObject(shooter);
            so.FindProperty("projectilePrefab").objectReferenceValue = projPrefab;
            so.FindProperty("shootPoint").objectReferenceValue = spGO.transform;
            so.FindProperty("data").objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            sb.AppendLine($"[SORCERER] {path} criado (Air_Wizard, altura {h:0.00}m)");
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static void AttachEyes(Transform parent, float height, float size, string matPath)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Vector3 pos = new Vector3(0, height * 0.88f, 0.07f);
            foreach (var side in new[] { -1, 1 })
            {
                var e = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(e.GetComponent<Collider>());
                e.name = side < 0 ? "Eye_Glow_L" : "Eye_Glow_R";
                e.transform.SetParent(parent, false);
                e.transform.localPosition = pos + new Vector3(side * 0.055f, 0.02f, 0.03f);
                e.transform.localScale = Vector3.one * size;
                e.GetComponent<Renderer>().sharedMaterial = mat;
            }
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

        static void WireSorcererInScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Arena.unity", OpenSceneMode.Single);
            var spawner = Object.FindAnyObjectByType<ArenaSurvivor.Waves.WaveSpawner>();
            if (spawner == null) return;
            var so = new SerializedObject(spawner);

            // o shooterPrefab passa a ser o Enemy_Sorcerer; chasers continuam zumbis
            var sorc = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFABS}/Enemy_Sorcerer.prefab");
            var p = so.FindProperty("shooterPrefab");
            if (p != null && sorc != null) p.objectReferenceValue = sorc;

            // Data_Shooter já está no prefab; salva a cena
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            sb.AppendLine("[SCENE] WaveSpawner.shooterPrefab = Enemy_Sorcerer");
        }
    }
}
#endif
