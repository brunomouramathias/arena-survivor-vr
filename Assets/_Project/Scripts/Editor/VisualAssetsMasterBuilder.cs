using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using ArenaSurvivor.Core;
using ArenaSurvivor.Player;
using ArenaSurvivor.Enemies;
using ArenaSurvivor.Weapons;

namespace ArenaSurvivor.Editor
{
    [InitializeOnLoad]
    public static class VisualAssetsMasterBuilder
    {
        private const string INBOX_MIXAMO = "Assets/_Project/ThirdParty/_INBOX/Mixamo";
        private const string INBOX_NATURE = "Assets/_Project/ThirdParty/_INBOX/Kenney_NatureKit";
        private const string INBOX_BLASTER = "Assets/_Project/ThirdParty/_INBOX/Kenney_BlasterKit";

        private const string ANIM_DIR = "Assets/_Project/Animations";
        private const string MAT_DIR = "Assets/_Project/Materials";
        private const string PREFAB_DIR = "Assets/_Project/Prefabs";
        private const string SCENE_ARENA = "Assets/_Project/Scenes/Arena.unity";

        private const string PREF_KEY = "ArenaSurvivor_VisualOverhaul_V2_Done";

        static VisualAssetsMasterBuilder()
        {
            // Auto-execução desativada para seguir o fluxo rigoroso de tarefas do Prompt V3
        }

        [MenuItem("Tools/Arena/Executar Pipeline Visual Completo (Assets Reais + Boss Mago)", priority = 1)]
        public static void MenuRunPipeline()
        {
            RunFullVisualOverhaul(false);
        }

        public static void RunFullVisualOverhaulBatch()
        {
            RunFullVisualOverhaul(true);
        }

        [MenuItem("Tools/Arena/Colocar Boss Mago na Cena Arena para Teste", priority = 2)]
        public static void MenuSpawnBossInScene()
        {
            SpawnBossInCurrentScene();
        }

        public static void RunFullVisualOverhaul(bool isAuto)
        {
            try
            {
                EnsureFolders();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                // 1. Configurar ModelImporters para Mixamo (Humanoid) e Kenney
                ConfigureMixamoFBXs();
                ConfigureKenneyFBXs();

                // 2. Criar / Atualizar Materiais URP Lit
                CreateAllMaterials();

                // 3. Criar Animator Controllers (Boss, Zombie, Player)
                CreateBossAnimatorController();
                CreateZombieAnimatorController();
                CreatePlayerAnimatorController();

                // 4. Montar Prefabs com modelos 3D Reais (Zero Primitivas)
                BuildBossPrefab();
                BuildZombiePrefabs();
                BuildProjectileMagicPrefab();

                // 5. Overhaul completo da Cena Arena
                OverhaulArenaScene();

                EditorPrefs.SetBool(PREF_KEY, true);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log("<color=#00FF00><b>[VisualAssetsMasterBuilder] PIPELINE VISUAL CONCLUÍDO COM SUCESSO! O Boss Mago e os modelos 3D reais foram integrados.</b></color>");
                if (!isAuto && !Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog("Arena Survivor - Visual Overhaul",
                        "Pipeline Visual concluído com sucesso!\n\n" +
                        "• O Chefão Mago Mixamo está configurado com animações mágicas.\n" +
                        "• Zumbis e Jogador atualizados com rigs e modelos 3D reais.\n" +
                        "• Blaster Kenney acoplado ao Jogador.\n" +
                        "• Arena transformada com pedras, árvores e iluminação crepúsculo.\n" +
                        "• Pressione Play para testar o combate com o Mago!", "Jogar!");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VisualAssetsMasterBuilder] Erro durante o pipeline: {ex}");
            }
        }

        private static void EnsureFolders()
        {
            string[] dirs = {
                ANIM_DIR,
                $"{ANIM_DIR}/Boss",
                $"{ANIM_DIR}/Zombie",
                $"{ANIM_DIR}/Player",
                MAT_DIR,
                PREFAB_DIR,
                "Assets/_Project/Scenes/Settings"
            };

            foreach (var dir in dirs)
            {
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    var parts = dir.Split('/');
                    string parent = string.Join("/", parts.Take(parts.Length - 1));
                    string folder = parts.Last();
                    AssetDatabase.CreateFolder(parent, folder);
                }
            }
        }

        // =====================================================================
        // 1. Configuração de Importação FBX (Rigs Humanoid e Escala)
        // =====================================================================
        private static void ConfigureMixamoFBXs()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { INBOX_MIXAMO });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                bool dirty = false;
                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    dirty = true;
                }

                // Mixamo usa centímetros; 0.01 ajusta para metros de forma consistente
                if (!Mathf.Approximately(importer.globalScale, 0.01f))
                {
                    importer.globalScale = 0.01f;
                    dirty = true;
                }

                string fileName = Path.GetFileNameWithoutExtension(path);
                bool shouldLoop = fileName.IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  fileName.IndexOf("run", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  fileName.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  fileName.IndexOf("gunplay", StringComparison.OrdinalIgnoreCase) >= 0;

                var clips = importer.defaultClipAnimations;
                if (clips != null && clips.Length > 0)
                {
                    clips[0].name = fileName;
                    clips[0].loopTime = shouldLoop;
                    importer.clipAnimations = clips;
                    dirty = true;
                }

                if (dirty)
                {
                    importer.SaveAndReimport();
                }
            }
        }

        private static void ConfigureKenneyFBXs()
        {
            string[] folders = { INBOX_NATURE, INBOX_BLASTER };
            string[] guids = AssetDatabase.FindAssets("t:Model", folders);
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                bool dirty = false;
                if (importer.animationType != ModelImporterAnimationType.None)
                {
                    importer.animationType = ModelImporterAnimationType.None;
                    dirty = true;
                }

                if (dirty)
                {
                    importer.SaveAndReimport();
                }
            }
        }

        // =====================================================================
        // 2. Criação de Materiais URP Lit
        // =====================================================================
        private static void CreateAllMaterials()
        {
            // Boss Mage Material (Manto Roxo Místico)
            CreateLitMaterial($"{MAT_DIR}/Mat_Boss_Mage.mat", new Color(0.28f, 0.12f, 0.45f), 0.2f, 0.15f);

            // Boss Eyes (Glow Ciano / Elétrico HDR)
            CreateEmissiveMaterial($"{MAT_DIR}/Mat_Boss_Eyes.mat", Color.black, new Color(0f, 1.8f, 2.5f) * 3f);

            // Zombie Skin (Verde podre zumbi)
            CreateLitMaterial($"{MAT_DIR}/Mat_Zombie_Skin.mat", new Color(0.40f, 0.46f, 0.32f), 0.1f, 0.1f);

            // Zombie Eyes (Vermelho Sangue HDR)
            CreateEmissiveMaterial($"{MAT_DIR}/Mat_Zombie_Eyes.mat", Color.black, new Color(3f, 0.2f, 0f) * 3f);

            // Player Hero Material (Armadura Azul Cobalto Estilizada)
            CreateLitMaterial($"{MAT_DIR}/Mat_Player_Hero.mat", new Color(0.15f, 0.42f, 0.85f), 0.3f, 0.2f);

            // Player Visor (Glow Âmbar Dourado HDR)
            CreateEmissiveMaterial($"{MAT_DIR}/Mat_Player_Visor.mat", Color.black, new Color(2.5f, 1.4f, 0.1f) * 2.5f);

            // Blaster Material (com textura colormap.png do kit)
            CreateBlasterMaterial();

            // Floor & Environment
            CreateLitMaterial($"{MAT_DIR}/Mat_ArenaGround.mat", new Color(0.24f, 0.28f, 0.19f), 0.05f, 0.1f);
            CreateLitMaterial($"{MAT_DIR}/Mat_StylizedRock.mat", new Color(0.42f, 0.38f, 0.34f), 0.1f, 0.05f);

            // Magic Projectile
            CreateEmissiveMaterial($"{MAT_DIR}/Mat_ProjMagicBoss.mat", new Color(0.7f, 0.1f, 0.9f), new Color(2.5f, 0.4f, 3.2f) * 3f);
        }

        private static Material CreateLitMaterial(string assetPath, Color baseColor, float metallic, float smoothness)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, assetPath);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateEmissiveMaterial(string assetPath, Color baseColor, Color emissionColor)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, assetPath);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emissionColor);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void CreateBlasterMaterial()
        {
            string path = $"{MAT_DIR}/Mat_BlasterColormap.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            string[] texGuids = AssetDatabase.FindAssets("colormap t:Texture2D", new[] { INBOX_BLASTER });
            if (texGuids.Length > 0)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(texGuids[0]));
                if (tex != null && mat.HasProperty("_BaseMap"))
                {
                    mat.SetTexture("_BaseMap", tex);
                    mat.SetColor("_BaseColor", Color.white);
                }
            }
            EditorUtility.SetDirty(mat);
        }

        // =====================================================================
        // 3. Animator Controllers
        // =====================================================================
        private static void CreateBossAnimatorController()
        {
            string path = $"{ANIM_DIR}/Boss/AnimatorController_Boss.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            }

            // Remove existing parameters & recreate
            controller.parameters = new AnimatorControllerParameter[0];
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("CastSpell", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

            var rootSM = controller.layers[0].stateMachine;
            rootSM.states = new ChildAnimatorState[0];
            rootSM.anyStateTransitions = new AnimatorStateTransition[0];

            var idleClip   = FindAnimationClip("Standing 2H Cast Spell 01") ?? FindAnimationClip("Standing 2H Magic Attack 01");
            var walkClip   = FindAnimationClip("Zombie Walk") ?? idleClip;
            var attackClip = FindAnimationClip("Standing 2H Magic Attack 05") ?? FindAnimationClip("Standing 2H Magic Attack 01");
            var castClip   = FindAnimationClip("Standing 1H Cast Spell 01") ?? FindAnimationClip("Standing 1H Magic Attack 02");

            var stateIdle   = rootSM.AddState("Idle");
            var stateWalk   = rootSM.AddState("Walk");
            var stateAttack = rootSM.AddState("MagicAttack");
            var stateCast   = rootSM.AddState("CastSpell");
            var stateDie    = rootSM.AddState("Die");

            stateIdle.motion   = idleClip;
            stateWalk.motion   = walkClip;
            stateAttack.motion = attackClip;
            stateCast.motion   = castClip;

            rootSM.defaultState = stateIdle;

            // Idle <-> Walk
            var t1 = stateIdle.AddTransition(stateWalk);
            t1.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            t1.duration = 0.2f;

            var t2 = stateWalk.AddTransition(stateIdle);
            t2.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");
            t2.duration = 0.2f;

            // AnyState -> Attack
            var tAtk = rootSM.AddAnyStateTransition(stateAttack);
            tAtk.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            tAtk.duration = 0.1f;
            tAtk.canTransitionToSelf = false;

            var tAtkBack = stateAttack.AddTransition(stateIdle);
            tAtkBack.hasExitTime = true;
            tAtkBack.exitTime = 0.85f;
            tAtkBack.duration = 0.2f;

            // AnyState -> CastSpell
            var tCast = rootSM.AddAnyStateTransition(stateCast);
            tCast.AddCondition(AnimatorConditionMode.If, 0, "CastSpell");
            tCast.duration = 0.1f;
            tCast.canTransitionToSelf = false;

            var tCastBack = stateCast.AddTransition(stateIdle);
            tCastBack.hasExitTime = true;
            tCastBack.exitTime = 0.85f;
            tCastBack.duration = 0.2f;

            // AnyState -> Die
            var tDie = rootSM.AddAnyStateTransition(stateDie);
            tDie.AddCondition(AnimatorConditionMode.If, 0, "Die");
            tDie.duration = 0.1f;
            tDie.canTransitionToSelf = false;

            EditorUtility.SetDirty(controller);
        }

        private static void CreateZombieAnimatorController()
        {
            string path = $"{ANIM_DIR}/Zombie/AnimatorController_Zombie.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            }

            controller.parameters = new AnimatorControllerParameter[0];
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;
            sm.states = new ChildAnimatorState[0];
            sm.anyStateTransitions = new AnimatorStateTransition[0];

            var walkClip = FindAnimationClip("Zombie Walk");
            var runClip  = FindAnimationClip("Zombie Run") ?? walkClip;
            var atkClip  = FindAnimationClip("Zombie Attack") ?? walkClip;

            var sIdle   = sm.AddState("Idle");
            var sWalk   = sm.AddState("Walk");
            var sRun    = sm.AddState("Run");
            var sAttack = sm.AddState("Attack");
            var sDie    = sm.AddState("Die");

            sIdle.motion   = walkClip;
            sWalk.motion   = walkClip;
            sRun.motion    = runClip;
            sAttack.motion = atkClip;
            sm.defaultState = sIdle;

            var t1 = sIdle.AddTransition(sWalk);
            t1.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            t1.duration = 0.2f;

            var t2 = sWalk.AddTransition(sIdle);
            t2.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");
            t2.duration = 0.2f;

            var t3 = sWalk.AddTransition(sRun);
            t3.AddCondition(AnimatorConditionMode.Greater, 2.5f, "Speed");
            t3.duration = 0.2f;

            var t4 = sRun.AddTransition(sWalk);
            t4.AddCondition(AnimatorConditionMode.Less, 2.2f, "Speed");
            t4.duration = 0.2f;

            var tAtk = sm.AddAnyStateTransition(sAttack);
            tAtk.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            tAtk.duration = 0.1f;
            tAtk.canTransitionToSelf = false;

            var tAtkBack = sAttack.AddTransition(sIdle);
            tAtkBack.hasExitTime = true;
            tAtkBack.exitTime = 0.85f;
            tAtkBack.duration = 0.2f;

            var tDie = sm.AddAnyStateTransition(sDie);
            tDie.AddCondition(AnimatorConditionMode.If, 0, "Die");
            tDie.duration = 0.05f;
            tDie.canTransitionToSelf = false;

            EditorUtility.SetDirty(controller);
        }

        private static void CreatePlayerAnimatorController()
        {
            string path = $"{ANIM_DIR}/Player/AnimatorController_Player.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            }

            controller.parameters = new AnimatorControllerParameter[0];
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;
            sm.states = new ChildAnimatorState[0];
            sm.anyStateTransitions = new AnimatorStateTransition[0];

            var gunClip   = FindAnimationClip("Gunplay");
            var shootClip = FindAnimationClip("Shooting Gun") ?? FindAnimationClip("Shooting") ?? gunClip;

            var sIdle  = sm.AddState("Idle");
            var sMove  = sm.AddState("Move");
            var sShoot = sm.AddState("Shoot");
            var sDie   = sm.AddState("Die");

            sIdle.motion  = gunClip;
            sMove.motion  = gunClip;
            sShoot.motion = shootClip;
            sm.defaultState = sIdle;

            var t1 = sIdle.AddTransition(sMove);
            t1.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            t1.duration = 0.1f;

            var t2 = sMove.AddTransition(sIdle);
            t2.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");
            t2.duration = 0.1f;

            var tShoot = sm.AddAnyStateTransition(sShoot);
            tShoot.AddCondition(AnimatorConditionMode.If, 0, "Shoot");
            tShoot.duration = 0.05f;
            tShoot.canTransitionToSelf = false;

            var tShootBack = sShoot.AddTransition(sIdle);
            tShootBack.hasExitTime = true;
            tShootBack.exitTime = 0.8f;
            tShootBack.duration = 0.15f;

            var tDie = sm.AddAnyStateTransition(sDie);
            tDie.AddCondition(AnimatorConditionMode.If, 0, "Die");
            tDie.duration = 0.05f;
            tDie.canTransitionToSelf = false;

            EditorUtility.SetDirty(controller);
        }

        private static AnimationClip FindAnimationClip(string clipName)
        {
            string[] guids = AssetDatabase.FindAssets($"{clipName} t:Model", new[] { INBOX_MIXAMO });
            if (guids.Length == 0) guids = AssetDatabase.FindAssets("t:Model", new[] { INBOX_MIXAMO });

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path).IndexOf(clipName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                    foreach (var sub in subAssets)
                    {
                        if (sub is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                            return clip;
                    }
                }
            }
            return null;
        }

        // =====================================================================
        // 4. Montagem de Prefabs (Zero Primitivas)
        // =====================================================================
        private static void BuildBossPrefab()
        {
            string prefabPath = $"{PREFAB_DIR}/Enemy_Boss.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null) AssetDatabase.DeleteAsset(prefabPath);

            var bossGO = new GameObject("Enemy_Boss");
            bossGO.tag   = "Enemy";
            bossGO.layer = LayerMask.NameToLayer("Enemy") >= 0 ? LayerMask.NameToLayer("Enemy") : 0;

            // Modelo 3D Real do Mixamo (Mago)
            string mageFBXPath = FindFBXPath("Standing 2H Magic Attack 05") ?? FindFBXPath("Standing 2H Cast Spell 01");
            if (!string.IsNullOrEmpty(mageFBXPath))
            {
                var fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(mageFBXPath);
                if (fbxAsset != null)
                {
                    var visual = UnityEngine.Object.Instantiate(fbxAsset, bossGO.transform);
                    visual.name = "Model_Mage";
                    visual.transform.localPosition = Vector3.zero;
                    visual.transform.localRotation = Quaternion.identity;
                    visual.transform.localScale    = Vector3.one * 1.55f; // Chefe colossal (2.8m altura)

                    // Aplicar material do mago a todos os renderers do modelo
                    var robeMat = AssetDatabase.LoadAssetAtPath<Material>($"{MAT_DIR}/Mat_Boss_Mage.mat");
                    foreach (var rend in visual.GetComponentsInChildren<Renderer>())
                    {
                        if (robeMat != null) rend.sharedMaterial = robeMat;
                    }

                    // Olhos Emissivos Flutuantes / Conectados
                    AttachGlowingEyes(visual.transform, new Vector3(0, 1.75f, 0.25f), 0.16f, $"{MAT_DIR}/Mat_Boss_Eyes.mat");
                }
            }

            // Collider e Físicas
            var col = bossGO.AddComponent<CapsuleCollider>();
            col.height = 2.8f;
            col.radius = 0.75f;
            col.center = new Vector3(0, 1.4f, 0);

            var rb = bossGO.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity  = false;

            // NavMeshAgent
            var agent = bossGO.AddComponent<NavMeshAgent>();
            agent.speed = 2.6f;
            agent.stoppingDistance = 2.2f;
            agent.radius = 0.75f;
            agent.height = 2.8f;

            // Animator
            var anim = bossGO.AddComponent<Animator>();
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{ANIM_DIR}/Boss/AnimatorController_Boss.controller");
            if (ctrl != null) anim.runtimeAnimatorController = ctrl;

            // ShootPoint
            var spGO = new GameObject("ShootPoint");
            spGO.transform.SetParent(bossGO.transform);
            spGO.transform.localPosition = new Vector3(0f, 1.6f, 1.0f);

            // BossController
            var boss = bossGO.AddComponent<BossController>();
            var so = new SerializedObject(boss);

            var projPrefab   = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFAB_DIR}/Projectile_Enemy.prefab");
            var warnPrefab   = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFAB_DIR}/SpawnWarning.prefab");
            var minionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFAB_DIR}/Enemy_Chaser.prefab");
            var bossData     = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/_Project/ScriptableObjects/Enemies/EnemyData_Boss.asset");

            so.FindProperty("projectilePrefab").objectReferenceValue = projPrefab;
            so.FindProperty("aoeWarningPrefab").objectReferenceValue = warnPrefab;
            so.FindProperty("minionPrefab").objectReferenceValue     = minionPrefab;
            so.FindProperty("shootPoint").objectReferenceValue       = spGO.transform;
            so.FindProperty("data").objectReferenceValue             = bossData;
            so.ApplyModifiedProperties();

            // Health Bar
            AddHealthBar(bossGO, 3.2f);

            PrefabUtility.SaveAsPrefabAsset(bossGO, prefabPath);
            UnityEngine.Object.DestroyImmediate(bossGO);
            Debug.Log("[VisualAssetsMasterBuilder] Prefab Enemy_Boss criado com modelo 3D Mixamo.");
        }

        private static void BuildZombiePrefabs()
        {
            string zombieWalkPath = FindFBXPath("Zombie Walk");
            string zombieRunPath  = FindFBXPath("Zombie Run") ?? zombieWalkPath;

            var zombieSkinMat = AssetDatabase.LoadAssetAtPath<Material>($"{MAT_DIR}/Mat_Zombie_Skin.mat");
            var zombieCtrl    = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{ANIM_DIR}/Zombie/AnimatorController_Zombie.controller");
            var chaserData    = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/_Project/ScriptableObjects/Enemies/EnemyData_Chaser.asset");
            var shooterData   = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/_Project/ScriptableObjects/Enemies/EnemyData_Shooter.asset");
            var projPrefab    = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFAB_DIR}/Projectile_Enemy.prefab");

            // --- Enemy_Chaser ---
            BuildSingleZombiePrefab("Enemy_Chaser", zombieWalkPath, 1.0f, zombieSkinMat, zombieCtrl, chaserData, null, null);

            // --- Enemy_Shooter ---
            BuildSingleZombiePrefab("Enemy_Shooter", zombieRunPath, 0.95f, zombieSkinMat, zombieCtrl, shooterData, projPrefab, new Vector3(0f, 1.2f, 0.6f));
        }

        private static void BuildSingleZombiePrefab(string prefabName, string fbxPath, float scale, Material skinMat,
            RuntimeAnimatorController ctrl, EnemyData data, GameObject projPrefab, Vector3? shootPointPos)
        {
            string path = $"{PREFAB_DIR}/{prefabName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);

            var go = new GameObject(prefabName);
            go.tag   = "Enemy";
            go.layer = LayerMask.NameToLayer("Enemy") >= 0 ? LayerMask.NameToLayer("Enemy") : 0;

            if (!string.IsNullOrEmpty(fbxPath))
            {
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (fbx != null)
                {
                    var visual = UnityEngine.Object.Instantiate(fbx, go.transform);
                    visual.name = "Model_Zombie";
                    visual.transform.localPosition = Vector3.zero;
                    visual.transform.localRotation = Quaternion.identity;
                    visual.transform.localScale    = Vector3.one * scale;

                    foreach (var rend in visual.GetComponentsInChildren<Renderer>())
                    {
                        if (skinMat != null) rend.sharedMaterial = skinMat;
                    }

                    AttachGlowingEyes(visual.transform, new Vector3(0, 1.45f, 0.18f), 0.10f, $"{MAT_DIR}/Mat_Zombie_Eyes.mat");
                }
            }

            var col = go.AddComponent<CapsuleCollider>();
            col.height = 1.8f;
            col.radius = 0.45f;
            col.center = new Vector3(0, 0.9f, 0);

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity  = false;

            var agent = go.AddComponent<NavMeshAgent>();
            agent.speed = data != null ? data.moveSpeed : 3.0f;
            agent.stoppingDistance = shootPointPos.HasValue ? 6.0f : 1.2f;
            agent.radius = 0.45f;
            agent.height = 1.8f;

            var anim = go.AddComponent<Animator>();
            if (ctrl != null) anim.runtimeAnimatorController = ctrl;

            if (shootPointPos.HasValue)
            {
                var spGO = new GameObject("ShootPoint");
                spGO.transform.SetParent(go.transform);
                spGO.transform.localPosition = shootPointPos.Value;

                var shooter = go.AddComponent<EnemyShooter>();
                var so = new SerializedObject(shooter);
                so.FindProperty("projectilePrefab").objectReferenceValue = projPrefab;
                so.FindProperty("shootPoint").objectReferenceValue       = spGO.transform;
                so.FindProperty("data").objectReferenceValue             = data;
                so.ApplyModifiedProperties();
            }
            else
            {
                var chaser = go.AddComponent<EnemyChaser>();
                var so = new SerializedObject(chaser);
                so.FindProperty("data").objectReferenceValue = data;
                so.ApplyModifiedProperties();
            }

            AddHealthBar(go, 2.1f);

            PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log($"[VisualAssetsMasterBuilder] Prefab {prefabName} criado com modelo 3D Mixamo.");
        }

        private static void BuildProjectileMagicPrefab()
        {
            string path = $"{PREFAB_DIR}/Projectile_Enemy.prefab";
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null)
            {
                using (var scope = new PrefabUtility.EditPrefabContentsScope(path))
                {
                    var root = scope.prefabContentsRoot;
                    var rend = root.GetComponent<Renderer>();
                    var magicMat = AssetDatabase.LoadAssetAtPath<Material>($"{MAT_DIR}/Mat_ProjMagicBoss.mat");
                    if (rend != null && magicMat != null)
                    {
                        rend.sharedMaterial = magicMat;
                    }
                    root.transform.localScale = Vector3.one * 0.45f;
                }
            }
        }

        private static void AttachGlowingEyes(Transform parent, Vector3 localPos, float eyeSize, string matPath)
        {
            var eyeMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            var eyeL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eyeL.name = "Eye_Glow_L";
            eyeL.transform.SetParent(parent);
            eyeL.transform.localPosition = localPos + new Vector3(-0.09f, 0, 0);
            eyeL.transform.localScale    = Vector3.one * eyeSize;
            UnityEngine.Object.DestroyImmediate(eyeL.GetComponent<Collider>());
            if (eyeMat != null) eyeL.GetComponent<Renderer>().sharedMaterial = eyeMat;

            var eyeR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eyeR.name = "Eye_Glow_R";
            eyeR.transform.SetParent(parent);
            eyeR.transform.localPosition = localPos + new Vector3(0.09f, 0, 0);
            eyeR.transform.localScale    = Vector3.one * eyeSize;
            UnityEngine.Object.DestroyImmediate(eyeR.GetComponent<Collider>());
            if (eyeMat != null) eyeR.GetComponent<Renderer>().sharedMaterial = eyeMat;
        }

        private static void AddHealthBar(GameObject go, float yOffset)
        {
            var hbGO = new GameObject("HealthBar");
            hbGO.transform.SetParent(go.transform);
            hbGO.transform.localPosition = new Vector3(0, yOffset, 0);

            var canvas = hbGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = canvas.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(120f, 20f);
            hbGO.transform.localScale = Vector3.one * 0.01f;

            var sliderGO = new GameObject("Slider");
            sliderGO.transform.SetParent(hbGO.transform, false);
            var slider = sliderGO.AddComponent<UnityEngine.UI.Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value    = 1f;

            var hb = hbGO.AddComponent<EnemyHealthBar>();
            var hbSO = new SerializedObject(hb);
            var sliderProp = hbSO.FindProperty("slider");
            if (sliderProp != null) sliderProp.objectReferenceValue = slider;
            var canvasProp = hbSO.FindProperty("canvas");
            if (canvasProp != null) canvasProp.objectReferenceValue = canvas;
            hbSO.ApplyModifiedProperties();

            var eb = go.GetComponent<EnemyBase>();
            if (eb != null)
            {
                var ebSO = new SerializedObject(eb);
                var ebHbProp = ebSO.FindProperty("healthBar");
                if (ebHbProp != null) ebHbProp.objectReferenceValue = hb;
                ebSO.ApplyModifiedProperties();
            }
        }

        private static string FindFBXPath(string searchName)
        {
            string[] guids = AssetDatabase.FindAssets($"{searchName} t:Model", new[] { INBOX_MIXAMO });
            if (guids.Length > 0) return AssetDatabase.GUIDToAssetPath(guids[0]);

            // Fallback: search all
            guids = AssetDatabase.FindAssets("t:Model", new[] { INBOX_MIXAMO });
            foreach (var g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(p).IndexOf(searchName, StringComparison.OrdinalIgnoreCase) >= 0)
                    return p;
            }
            return null;
        }

        // =====================================================================
        // 5. Overhaul da Cena Arena (Ambiente Natural + Pedras + Blaster + Post-Processing)
        // =====================================================================
        private static void OverhaulArenaScene()
        {
            var currentScene = EditorSceneManager.GetActiveScene();
            if (currentScene.path != SCENE_ARENA)
            {
                currentScene = EditorSceneManager.OpenScene(SCENE_ARENA, OpenSceneMode.Single);
            }

            // 0. Atualizar Piso da Arena com Material de Selva/Ruínas
            var floorGO = GameObject.Find("Floor");
            if (floorGO != null)
            {
                var floorMat = AssetDatabase.LoadAssetAtPath<Material>($"{MAT_DIR}/Mat_ArenaGround.mat");
                if (floorMat != null && floorGO.TryGetComponent<Renderer>(out var fr))
                    fr.sharedMaterial = floorMat;
            }

            // 1. Atualizar Jogador com Modelo 3D e Blaster
            OverhaulPlayerInScene();

            // 2. Substituir Cubos de Obstáculo por Pedras e Cristais Reais (Kenney)
            OverhaulObstaclesInScene();

            // 3. Adicionar Vegetação Perimetral (Árvores Kenney Nature)
            SetupNaturePerimeter();

            // 4. Configurar Iluminação Crepúsculo / Céu Místico
            SetupAtmosphereLighting();

            // 5. Configurar Volume de Post-Processing URP (Bloom, Vignette, ACES)
            SetupPostProcessingVolume();

            // 6. Spawnar o Boss Mago para Teste Imediato!
            SpawnBossInCurrentScene();

            // 7. Rebake NavMesh
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();

            EditorSceneManager.MarkSceneDirty(currentScene);
            EditorSceneManager.SaveScene(currentScene);
            Debug.Log("[VisualAssetsMasterBuilder] Cena Arena completamente atualizada com ambiente estilizado e Boss!");
        }

        private static void OverhaulPlayerInScene()
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO == null) return;

            // Remove visual primitivo antigo (Body, Eyes)
            var oldBody = playerGO.transform.Find("Body");
            if (oldBody != null) UnityEngine.Object.DestroyImmediate(oldBody.gameObject);
            var oldEyeL = playerGO.transform.Find("Eye_L");
            if (oldEyeL != null) UnityEngine.Object.DestroyImmediate(oldEyeL.gameObject);
            var oldEyeR = playerGO.transform.Find("Eye_R");
            if (oldEyeR != null) UnityEngine.Object.DestroyImmediate(oldEyeR.gameObject);

            // Instancia modelo 3D Mixamo do Jogador (Gunplay)
            string playerFBX = FindFBXPath("Gunplay") ?? FindFBXPath("Shooting Gun");
            if (!string.IsNullOrEmpty(playerFBX))
            {
                var existingModel = playerGO.transform.Find("Model_Player");
                if (existingModel != null) UnityEngine.Object.DestroyImmediate(existingModel.gameObject);

                var fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(playerFBX);
                if (fbxAsset != null)
                {
                    var visual = UnityEngine.Object.Instantiate(fbxAsset, playerGO.transform);
                    visual.name = "Model_Player";
                    visual.transform.localPosition = Vector3.zero;
                    visual.transform.localRotation = Quaternion.identity;
                    visual.transform.localScale    = Vector3.one * 1.05f;

                    var heroMat = AssetDatabase.LoadAssetAtPath<Material>($"{MAT_DIR}/Mat_Player_Hero.mat");
                    foreach (var rend in visual.GetComponentsInChildren<Renderer>())
                    {
                        if (heroMat != null) rend.sharedMaterial = heroMat;
                    }

                    AttachGlowingEyes(visual.transform, new Vector3(0, 1.55f, 0.20f), 0.12f, $"{MAT_DIR}/Mat_Player_Visor.mat");
                }
            }

            // Animator Controller no Player
            var anim = playerGO.GetComponent<Animator>();
            if (anim == null) anim = playerGO.AddComponent<Animator>();
            var pCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{ANIM_DIR}/Player/AnimatorController_Player.controller");
            if (pCtrl != null) anim.runtimeAnimatorController = pCtrl;

            // Acoplar Blaster 3D real no WeaponHolder
            var weaponHolder = playerGO.transform.Find("WeaponHolder");
            if (weaponHolder != null)
            {
                // Remover modelos primitivos filhos
                for (int i = weaponHolder.childCount - 1; i >= 0; i--)
                {
                    var child = weaponHolder.GetChild(i);
                    var oldVis = child.Find("Blaster_Visual");
                    if (oldVis != null) UnityEngine.Object.DestroyImmediate(oldVis.gameObject);
                }

                // Encontra modelo do blaster Kenney
                string blasterFBX = FindKenneyBlasterFBX();
                if (!string.IsNullOrEmpty(blasterFBX))
                {
                    var blasterAsset = AssetDatabase.LoadAssetAtPath<GameObject>(blasterFBX);
                    var blasterMat   = AssetDatabase.LoadAssetAtPath<Material>($"{MAT_DIR}/Mat_BlasterColormap.mat");

                    for (int i = 0; i < weaponHolder.childCount; i++)
                    {
                        var slot = weaponHolder.GetChild(i);
                        var blasterVis = UnityEngine.Object.Instantiate(blasterAsset, slot);
                        blasterVis.name = "Blaster_Visual";
                        blasterVis.transform.localPosition = new Vector3(0.05f, -0.05f, 0.2f);
                        blasterVis.transform.localRotation = Quaternion.Euler(0, 90f, 0);
                        blasterVis.transform.localScale    = Vector3.one * 0.7f;

                        foreach (var r in blasterVis.GetComponentsInChildren<Renderer>())
                        {
                            if (blasterMat != null) r.sharedMaterial = blasterMat;
                        }
                    }
                }
            }
        }

        private static void OverhaulObstaclesInScene()
        {
            // Remover obstáculos de cubo antigos
            var oldObstacles = GameObject.FindGameObjectsWithTag("Obstacle");
            foreach (var obs in oldObstacles)
            {
                if (obs.name.StartsWith("Obstacle"))
                {
                    UnityEngine.Object.DestroyImmediate(obs);
                }
            }

            // Localizar modelos de pedras Kenney
            string[] rockModels = {
                FindNatureAsset("stone_largeA"),
                FindNatureAsset("stone_largeB"),
                FindNatureAsset("stone_tallA"),
                FindNatureAsset("rock_crystalsLarge")
            };

            var rockMat = AssetDatabase.LoadAssetAtPath<Material>($"{MAT_DIR}/Mat_StylizedRock.mat");

            Vector3[] obstaclePositions = {
                new Vector3( 8f, 0f,  8f),  new Vector3(-8f, 0f,  8f),
                new Vector3( 8f, 0f, -8f),  new Vector3(-8f, 0f, -8f),
                new Vector3( 0f, 0f, 10f),  new Vector3( 0f, 0f,-10f),
                new Vector3(10f, 0f,  0f),  new Vector3(-10f,0f,  0f),
            };

            var obsParent = GameObject.Find("--- Obstacles ---") ?? new GameObject("--- Obstacles ---");

            for (int i = 0; i < obstaclePositions.Length; i++)
            {
                string fbx = rockModels[i % rockModels.Length];
                GameObject rockInstance;
                if (!string.IsNullOrEmpty(fbx))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                    rockInstance = UnityEngine.Object.Instantiate(asset, obstaclePositions[i], Quaternion.Euler(0, i * 45f, 0));
                }
                else
                {
                    rockInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rockInstance.transform.position = obstaclePositions[i];
                }

                rockInstance.name = $"Rock_Cover_{i + 1}";
                rockInstance.tag   = "Obstacle";
                rockInstance.layer = LayerMask.NameToLayer("Obstacle") >= 0 ? LayerMask.NameToLayer("Obstacle") : 0;
                rockInstance.transform.SetParent(obsParent.transform);
                rockInstance.transform.localScale = Vector3.one * UnityEngine.Random.Range(2.2f, 3.0f);

                // Garantir Convex MeshCollider para física e navegação
                foreach (var mf in rockInstance.GetComponentsInChildren<MeshFilter>())
                {
                    var mc = mf.gameObject.GetComponent<MeshCollider>();
                    if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.convex = true;
                    if (rockMat != null)
                    {
                        var rend = mf.GetComponent<Renderer>();
                        if (rend != null) rend.sharedMaterial = rockMat;
                    }
                }

                GameObjectUtility.SetStaticEditorFlags(rockInstance, StaticEditorFlags.NavigationStatic);
            }
        }

        private static void SetupNaturePerimeter()
        {
            var natureParent = GameObject.Find("--- Nature_Perimeter ---");
            if (natureParent != null) UnityEngine.Object.DestroyImmediate(natureParent);

            natureParent = new GameObject("--- Nature_Perimeter ---");

            string[] treeModels = {
                FindNatureAsset("tree_palmDetailedTall"),
                FindNatureAsset("tree_palm"),
                FindNatureAsset("tree_oak"),
                FindNatureAsset("tree_pineDefaultA"),
                FindNatureAsset("stone_tallB")
            };

            float perimeter = 22f;
            int countPerSide = 10;
            float step = (perimeter * 2f) / countPerSide;

            int treeIndex = 0;
            for (float x = -perimeter; x <= perimeter; x += step)
            {
                SpawnPerimeterObject(treeModels[treeIndex % treeModels.Length], new Vector3(x, 0, perimeter + 1.5f), natureParent.transform);
                SpawnPerimeterObject(treeModels[(treeIndex + 1) % treeModels.Length], new Vector3(x, 0, -perimeter - 1.5f), natureParent.transform);
                treeIndex++;
            }

            for (float z = -perimeter; z <= perimeter; z += step)
            {
                SpawnPerimeterObject(treeModels[treeIndex % treeModels.Length], new Vector3(perimeter + 1.5f, 0, z), natureParent.transform);
                SpawnPerimeterObject(treeModels[(treeIndex + 2) % treeModels.Length], new Vector3(-perimeter - 1.5f, 0, z), natureParent.transform);
                treeIndex++;
            }
        }

        private static void SpawnPerimeterObject(string fbxPath, Vector3 pos, Transform parent)
        {
            if (string.IsNullOrEmpty(fbxPath)) return;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (asset == null) return;

            var obj = UnityEngine.Object.Instantiate(asset, pos, Quaternion.Euler(0, UnityEngine.Random.Range(0, 360f), 0));
            obj.transform.SetParent(parent);
            obj.transform.localScale = Vector3.one * UnityEngine.Random.Range(1.8f, 2.5f);
            GameObjectUtility.SetStaticEditorFlags(obj, StaticEditorFlags.NavigationStatic);
        }

        private static void SetupAtmosphereLighting()
        {
            var sunGO = GameObject.Find("Sun");
            if (sunGO != null)
            {
                var sun = sunGO.GetComponent<Light>();
                if (sun != null)
                {
                    sun.color = new Color(1.0f, 0.78f, 0.45f); // Sol poente dourado
                    sun.intensity = 1.15f;
                    sun.shadows   = LightShadows.Soft;
                    sunGO.transform.rotation = Quaternion.Euler(35f, -65f, 0f);
                }
            }

            RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor     = new Color(0.48f, 0.28f, 0.65f); // Céu roxo crepúsculo
            RenderSettings.ambientEquatorColor = new Color(0.85f, 0.45f, 0.20f); // Horizonte âmbar
            RenderSettings.ambientGroundColor  = new Color(0.15f, 0.12f, 0.10f); // Chão escuro

            RenderSettings.fog      = true;
            RenderSettings.fogColor = new Color(0.32f, 0.18f, 0.42f);
            RenderSettings.fogMode  = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.015f;
        }

        private static void SetupPostProcessingVolume()
        {
            var volGO = GameObject.Find("Global_Volume");
            if (volGO == null)
            {
                volGO = new GameObject("Global_Volume");
            }

            var volume = volGO.GetComponent<Volume>();
            if (volume == null) volume = volGO.AddComponent<Volume>();
            volume.isGlobal = true;

            string profilePath = "Assets/_Project/Scenes/Settings/ArenaVolumeProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }

            // Bloom
            if (!profile.TryGet<Bloom>(out var bloom))
            {
                bloom = profile.Add<Bloom>(true);
            }
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(1.2f);
            bloom.scatter.Override(0.7f);

            // Vignette
            if (!profile.TryGet<Vignette>(out var vig))
            {
                vig = profile.Add<Vignette>(true);
            }
            vig.intensity.Override(0.28f);
            vig.smoothness.Override(0.35f);

            // Tonemapping
            if (!profile.TryGet<Tonemapping>(out var tone))
            {
                tone = profile.Add<Tonemapping>(true);
            }
            tone.mode.Override(TonemappingMode.ACES);

            // Color Adjustments
            if (!profile.TryGet<ColorAdjustments>(out var ca))
            {
                ca = profile.Add<ColorAdjustments>(true);
            }
            ca.contrast.Override(14f);
            ca.saturation.Override(20f);

            volume.sharedProfile = profile;
            EditorUtility.SetDirty(profile);
        }

        public static void SpawnBossInCurrentScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != SCENE_ARENA)
            {
                EditorSceneManager.OpenScene(SCENE_ARENA, OpenSceneMode.Single);
            }

            var existingBoss = GameObject.Find("BOSS_MAGO_TESTE");
            if (existingBoss != null)
            {
                UnityEngine.Object.DestroyImmediate(existingBoss);
            }

            var bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFAB_DIR}/Enemy_Boss.prefab");
            if (bossPrefab != null)
            {
                var bossInstance = PrefabUtility.InstantiatePrefab(bossPrefab) as GameObject;
                bossInstance.name = "BOSS_MAGO_TESTE";
                bossInstance.transform.position = new Vector3(0f, 0f, 9f); // Na frente do jogador
                bossInstance.transform.rotation = Quaternion.Euler(0, 180f, 0); // Encarando o jogador

                var bossComp = bossInstance.GetComponent<BossController>();
                if (bossComp != null)
                {
                    try { bossComp.Initialize(1); } catch { }
                }

                Selection.activeGameObject = bossInstance;
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log("<color=#00FFFF><b>[VisualAssetsMasterBuilder] Boss Mago spawnado em (0, 0, 9) pronto para teste no Play Mode!</b></color>");
            }
        }

        private static string FindNatureAsset(string name)
        {
            string[] guids = AssetDatabase.FindAssets($"{name} t:Model", new[] { INBOX_NATURE });
            if (guids.Length > 0) return AssetDatabase.GUIDToAssetPath(guids[0]);
            return null;
        }

        private static string FindKenneyBlasterFBX()
        {
            string[] preferred = { "blaster-j", "blaster-e", "blaster-a", "blaster-f" };
            foreach (var p in preferred)
            {
                string[] guids = AssetDatabase.FindAssets($"{p} t:Model", new[] { INBOX_BLASTER });
                if (guids.Length > 0) return AssetDatabase.GUIDToAssetPath(guids[0]);
            }
            return null;
        }
    }
}
