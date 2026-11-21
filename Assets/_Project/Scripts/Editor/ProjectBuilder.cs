using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.IO;
using System.Collections.Generic;
using ArenaSurvivor.Core;
using ArenaSurvivor.Player;
using ArenaSurvivor.Enemies;
using ArenaSurvivor.Weapons;
using ArenaSurvivor.Waves;
using ArenaSurvivor.Items;
using ArenaSurvivor.Audio;
using ArenaSurvivor.UI;
using ArenaSurvivor.Shop;

namespace ArenaSurvivor.Editor
{
    // Ferramentas do menu Tools > Arena
    public static class ProjectBuilder
    {
        private const string ProjectPath = "Assets/_Project";
        private const string ScenesPath  = "Assets/_Project/Scenes";
        private const string PrefabsPath = "Assets/_Project/Prefabs";

        // =====================================================================
        // MENU: Tools > Arena > Build Everything
        // =====================================================================
        [MenuItem("Tools/Arena/Build Everything")]
        public static void BuildEverything()
        {
            Debug.Log("=== ArenaSurvivor: Build Everything iniciado ===");

            SetupLayersAndTags();
            SetupPhysicsMatrix();
            CreateMaterials();
            CreatePrefabs();
            CreateScriptableObjects();
            CreateMainMenuScene();
            CreateArenaScene();
            CreateSandboxScene();
            SetupBuildSettings();
            EditorSceneManager.OpenScene($"{ScenesPath}/Arena.unity");

            Debug.Log("=== Build Everything concluído! Abra Arena.unity e pressione Play. ===");
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("ArenaSurvivor", "Build Everything concluído!\n\nAbra Assets/_Project/Scenes/Arena.unity e pressione Play.", "OK");
        }

        // =====================================================================
        // MENU: Tools > Arena > Setup Layers & Tags
        // =====================================================================
        [MenuItem("Tools/Arena/Setup Layers & Tags")]
        public static void SetupLayersAndTags()
        {
            string[] tags = { "Enemy", "Obstacle", "Wall", "Pickup", "Ground", "EnemyProjectile" };
            foreach (var tag in tags)
                AddTag(tag);

            string[] layers = { "Player", "Enemy", "PlayerProjectile", "EnemyProjectile", "Obstacle", "Pickup", "Ground" };
            int layerStart = 8;
            for (int i = 0; i < layers.Length; i++)
                AddLayer(layers[i], layerStart + i);

            AssetDatabase.SaveAssets();
            Debug.Log("Layers e Tags configurados.");
        }

        // =====================================================================
        // MENU: Tools > Arena > Setup Physics Matrix
        // =====================================================================
        [MenuItem("Tools/Arena/Setup Physics Matrix")]
        public static void SetupPhysicsMatrix()
        {
            int player     = 8;
            int enemy      = 9;
            int playerProj = 10;
            int enemyProj  = 11;
            int obstacle   = 12;

            for (int i = 0; i < 32; i++)
                Physics.IgnoreLayerCollision(playerProj, i, true);

            Physics.IgnoreLayerCollision(playerProj, enemy,    false);
            Physics.IgnoreLayerCollision(playerProj, obstacle, false);

            for (int i = 0; i < 32; i++)
                Physics.IgnoreLayerCollision(enemyProj, i, true);

            Physics.IgnoreLayerCollision(enemyProj, player,   false);
            Physics.IgnoreLayerCollision(enemyProj, obstacle, false);

            Debug.Log("Matriz de colisão configurada.");
        }

        // =====================================================================
        // Criação de Materiais
        // =====================================================================
        private static void CreateMaterials()
        {
            string path = $"{ProjectPath}/Materials";
            Directory.CreateDirectory(path);

            CreateLitMaterial($"{path}/Mat_Floor.mat",       new Color(0.22f, 0.20f, 0.18f));
            CreateLitMaterial($"{path}/Mat_Wall.mat",        new Color(0.35f, 0.28f, 0.22f));
            CreateLitMaterial($"{path}/Mat_Obstacle.mat",    new Color(0.42f, 0.35f, 0.28f));
            CreateLitMaterial($"{path}/Mat_Player.mat",      new Color(0.20f, 0.55f, 0.95f));
            CreateLitMaterial($"{path}/Mat_EnemyChaser.mat",new Color(0.75f, 0.18f, 0.15f));
            CreateLitMaterial($"{path}/Mat_EnemyShooter.mat",new Color(0.90f, 0.45f, 0.10f));
            CreateLitMaterial($"{path}/Mat_EnemyBoss.mat",   new Color(0.45f, 0.10f, 0.65f));
            CreateLitMaterial($"{path}/Mat_EyeGlow.mat",     new Color(1.0f, 0.9f, 0.2f));
            CreateLitMaterial($"{path}/Mat_ProjPlayer.mat",  new Color(1.0f, 0.85f, 0.1f));
            CreateLitMaterial($"{path}/Mat_ProjEnemy.mat",   new Color(1.0f, 0.2f, 0.2f));
            CreateLitMaterial($"{path}/Mat_PickupHealth.mat",new Color(0.1f, 0.9f, 0.3f));
            CreateLitMaterial($"{path}/Mat_PickupAmmo.mat",  new Color(1.0f, 0.8f, 0.1f));
            CreateLitMaterial($"{path}/Mat_Warning.mat",     new Color(1.0f, 0.2f, 0.1f, 0.4f));

            AssetDatabase.SaveAssets();
        }

        private static Material CreateLitMaterial(string assetPath, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);

            AssetDatabase.CreateAsset(mat, assetPath);
            return mat;
        }

        // =====================================================================
        // Criação de Prefabs
        // =====================================================================
        [MenuItem("Tools/Arena/Create Prefabs")]
        public static void CreatePrefabs()
        {
            Directory.CreateDirectory(PrefabsPath);

            CreatePlayerProjectilePrefab();
            CreateEnemyProjectilePrefab();
            CreateWarningPrefab();
            CreateChaserPrefab();
            CreateShooterPrefab();
            CreateBossPrefab();
            CreateHealthPickupPrefab();
            CreateAmmoPickupPrefab();

            AssetDatabase.SaveAssets();
            Debug.Log("Prefabs criados com sucesso.");
        }

        private static void CreatePlayerProjectilePrefab()
        {
            string path = $"{PrefabsPath}/Projectile_Player.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile_Player";
            go.transform.localScale = new Vector3(0.25f, 0.25f, 0.5f);
            go.layer = LayerMask.NameToLayer("PlayerProjectile") >= 0 ? LayerMask.NameToLayer("PlayerProjectile") : 0;
            go.tag   = "Untagged";

            var col = go.GetComponent<SphereCollider>();
            col.isTrigger = true;

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity  = false;

            go.AddComponent<Projectile>();

            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{ProjectPath}/Materials/Mat_ProjPlayer.mat");
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;

            PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void CreateEnemyProjectilePrefab()
        {
            string path = $"{PrefabsPath}/Projectile_Enemy.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile_Enemy";
            go.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
            go.layer = LayerMask.NameToLayer("EnemyProjectile") >= 0 ? LayerMask.NameToLayer("EnemyProjectile") : 0;
            go.tag   = "EnemyProjectile";

            var col = go.GetComponent<SphereCollider>();
            col.isTrigger = true;

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity  = false;

            go.AddComponent<Projectile>();

            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{ProjectPath}/Materials/Mat_ProjEnemy.mat");
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;

            PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void CreateWarningPrefab()
        {
            string path = $"{PrefabsPath}/SpawnWarning.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "SpawnWarning";
            go.transform.localScale = new Vector3(2f, 0.02f, 2f);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());

            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{ProjectPath}/Materials/Mat_Warning.mat");
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;

            PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void CreateChaserPrefab()
        {
            string path = $"{PrefabsPath}/Enemy_Chaser.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

            var go = new GameObject("Enemy_Chaser");
            go.tag   = "Enemy";
            go.layer = LayerMask.NameToLayer("Enemy") >= 0 ? LayerMask.NameToLayer("Enemy") : 0;

            // Visual
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(go.transform);
            body.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            body.transform.localScale    = new Vector3(0.9f, 0.8f, 0.9f);
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());

            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{ProjectPath}/Materials/Mat_EnemyChaser.mat");
            if (mat != null) body.GetComponent<Renderer>().sharedMaterial = mat;

            // Glowing Eyes
            CreateEyes(go.transform, 1.2f, 0.4f);

            var col = go.AddComponent<CapsuleCollider>();
            col.height = 1.6f;
            col.radius = 0.45f;
            col.center = new Vector3(0f, 0.8f, 0f);

            var agent = go.AddComponent<NavMeshAgent>();
            agent.speed = 3.2f;
            agent.stoppingDistance = 1.2f;
            agent.radius = 0.45f;
            agent.height = 1.6f;

            go.AddComponent<EnemyChaser>();

            PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void CreateShooterPrefab()
        {
            string path = $"{PrefabsPath}/Enemy_Shooter.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

            var go = new GameObject("Enemy_Shooter");
            go.tag   = "Enemy";
            go.layer = LayerMask.NameToLayer("Enemy") >= 0 ? LayerMask.NameToLayer("Enemy") : 0;

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(go.transform);
            body.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            body.transform.localScale    = new Vector3(0.8f, 0.75f, 0.8f);
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());

            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{ProjectPath}/Materials/Mat_EnemyShooter.mat");
            if (mat != null) body.GetComponent<Renderer>().sharedMaterial = mat;

            CreateEyes(go.transform, 1.1f, 0.35f);

            var col = go.AddComponent<CapsuleCollider>();
            col.height = 1.5f;
            col.radius = 0.4f;
            col.center = new Vector3(0f, 0.75f, 0f);

            var agent = go.AddComponent<NavMeshAgent>();
            agent.speed = 2.4f;
            agent.stoppingDistance = 6.0f;
            agent.radius = 0.4f;
            agent.height = 1.5f;

            var shooter = go.AddComponent<EnemyShooter>();

            // Shoot Point
            var spGO = new GameObject("ShootPoint");
            spGO.transform.SetParent(go.transform);
            spGO.transform.localPosition = new Vector3(0f, 0.9f, 0.5f);

            // Vincula referências via SerializedObject
            var so = new SerializedObject(shooter);
            var projPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/Projectile_Enemy.prefab");
            so.FindProperty("projectilePrefab").objectReferenceValue = projPrefab;
            so.FindProperty("shootPoint").objectReferenceValue        = spGO.transform;
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void CreateBossPrefab()
        {
            string path = $"{PrefabsPath}/Enemy_Boss.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

            var go = new GameObject("Enemy_Boss");
            go.tag   = "Enemy";
            go.layer = LayerMask.NameToLayer("Enemy") >= 0 ? LayerMask.NameToLayer("Enemy") : 0;

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(go.transform);
            body.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            body.transform.localScale    = new Vector3(1.8f, 1.8f, 1.8f);
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());

            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{ProjectPath}/Materials/Mat_EnemyBoss.mat");
            if (mat != null) body.GetComponent<Renderer>().sharedMaterial = mat;

            CreateEyes(go.transform, 2.6f, 0.9f);

            var col = go.AddComponent<CapsuleCollider>();
            col.height = 3.6f;
            col.radius = 1.0f;
            col.center = new Vector3(0f, 1.8f, 0f);

            var agent = go.AddComponent<NavMeshAgent>();
            agent.speed = 2.5f;
            agent.stoppingDistance = 2.0f;
            agent.radius = 1.0f;
            agent.height = 3.6f;

            var boss = go.AddComponent<BossController>();

            var spGO = new GameObject("ShootPoint");
            spGO.transform.SetParent(go.transform);
            spGO.transform.localPosition = new Vector3(0f, 2.0f, 1.2f);

            var so = new SerializedObject(boss);
            var projPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/Projectile_Enemy.prefab");
            var warnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/SpawnWarning.prefab");
            var minionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/Enemy_Chaser.prefab");

            so.FindProperty("projectilePrefab").objectReferenceValue = projPrefab;
            so.FindProperty("aoeWarningPrefab").objectReferenceValue = warnPrefab;
            so.FindProperty("minionPrefab").objectReferenceValue     = minionPrefab;
            so.FindProperty("shootPoint").objectReferenceValue       = spGO.transform;
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void CreateEyes(Transform parent, float yPos, float zPos)
        {
            var eyeMat = AssetDatabase.LoadAssetAtPath<Material>($"{ProjectPath}/Materials/Mat_EyeGlow.mat");

            var eyeL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eyeL.name = "Eye_L";
            eyeL.transform.SetParent(parent);
            eyeL.transform.localPosition = new Vector3(-0.18f, yPos, zPos);
            eyeL.transform.localScale    = Vector3.one * 0.15f;
            UnityEngine.Object.DestroyImmediate(eyeL.GetComponent<Collider>());
            if (eyeMat != null) eyeL.GetComponent<Renderer>().sharedMaterial = eyeMat;

            var eyeR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eyeR.name = "Eye_R";
            eyeR.transform.SetParent(parent);
            eyeR.transform.localPosition = new Vector3(0.18f, yPos, zPos);
            eyeR.transform.localScale    = Vector3.one * 0.15f;
            UnityEngine.Object.DestroyImmediate(eyeR.GetComponent<Collider>());
            if (eyeMat != null) eyeR.GetComponent<Renderer>().sharedMaterial = eyeMat;
        }

        private static void CreateHealthPickupPrefab()
        {
            string path = $"{PrefabsPath}/Pickup_Health.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Pickup_Health";
            go.transform.localScale = Vector3.one * 0.5f;
            go.tag   = "Pickup";
            go.layer = LayerMask.NameToLayer("Pickup") >= 0 ? LayerMask.NameToLayer("Pickup") : 0;

            var col = go.GetComponent<BoxCollider>();
            col.isTrigger = true;

            go.AddComponent<Pickup>();

            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{ProjectPath}/Materials/Mat_PickupHealth.mat");
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;

            PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void CreateAmmoPickupPrefab()
        {
            string path = $"{PrefabsPath}/Pickup_Ammo.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Pickup_Ammo";
            go.transform.localScale = new Vector3(0.5f, 0.3f, 0.5f);
            go.tag   = "Pickup";
            go.layer = LayerMask.NameToLayer("Pickup") >= 0 ? LayerMask.NameToLayer("Pickup") : 0;

            var col = go.GetComponent<BoxCollider>();
            col.isTrigger = true;

            var pickup = go.AddComponent<Pickup>();
            var so = new SerializedObject(pickup);
            so.FindProperty("type").enumValueIndex = (int)PickupType.Ammo;
            so.ApplyModifiedProperties();

            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{ProjectPath}/Materials/Mat_PickupAmmo.mat");
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;

            PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
        }

        // =====================================================================
        // MENU: Tools > Arena > Create ScriptableObjects
        // =====================================================================
        [MenuItem("Tools/Arena/Create ScriptableObjects")]
        public static void CreateScriptableObjects()
        {
            CreateWeaponSOs();
            CreateEnemySOs();
            CreateWaveConfigSO();
            CreateUpgradeSOs();
            AssetDatabase.SaveAssets();
            Debug.Log("ScriptableObjects criados.");
        }

        private static void CreateWeaponSOs()
        {
            string path = $"{ProjectPath}/ScriptableObjects/Weapons";
            Directory.CreateDirectory(path);

            var projPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/Projectile_Player.prefab");

            // Pistola
            var pistola = ScriptableObject.CreateInstance<WeaponData>();
            pistola.weaponName     = "Pistola";
            pistola.damage         = 18f;
            pistola.fireRate       = 4f;
            pistola.pellets        = 1;
            pistola.spread         = 0f;
            pistola.clipSize       = 12;
            pistola.reserveAmmo    = -1;
            pistola.reloadTime     = 1.1f;
            pistola.unlockCost     = 0;
            pistola.startUnlocked  = true;
            pistola.projectilePrefab = projPrefab;
            pistola.shakeMagnitude = 0.08f;
            SaveSO(pistola, $"{path}/WeaponData_Pistol.asset");

            // Escopeta
            var shotgun = ScriptableObject.CreateInstance<WeaponData>();
            shotgun.weaponName     = "Escopeta";
            shotgun.damage         = 9f;
            shotgun.fireRate       = 1.2f;
            shotgun.pellets        = 7;
            shotgun.spread         = 12f;
            shotgun.clipSize       = 6;
            shotgun.reserveAmmo    = 36;
            shotgun.reloadTime     = 1.8f;
            shotgun.unlockCost     = 150;
            shotgun.startUnlocked  = false;
            shotgun.projectilePrefab = projPrefab;
            shotgun.shakeMagnitude = 0.25f;
            SaveSO(shotgun, $"{path}/WeaponData_Shotgun.asset");

            // Arma de Energia
            var energy = ScriptableObject.CreateInstance<WeaponData>();
            energy.weaponName     = "Arma de Energia";
            energy.damage         = 26f;
            energy.fireRate       = 6f;
            energy.pellets        = 1;
            energy.spread         = 2f;
            energy.piercing       = true;
            energy.clipSize       = 30;
            energy.reserveAmmo    = 120;
            energy.reloadTime     = 1.6f;
            energy.unlockCost     = 350;
            energy.startUnlocked  = false;
            energy.projectilePrefab = projPrefab;
            energy.shakeMagnitude = 0.12f;
            SaveSO(energy, $"{path}/WeaponData_Energy.asset");
        }

        private static void CreateEnemySOs()
        {
            string path = $"{ProjectPath}/ScriptableObjects/Enemies";
            Directory.CreateDirectory(path);

            var chaser  = ScriptableObject.CreateInstance<EnemyData>();
            chaser.enemyName        = "Inimigo Comum";
            chaser.baseHealth       = 40;
            chaser.moveSpeed        = 3.2f;
            chaser.contactDamage    = 10;
            chaser.attackCooldown   = 0.9f;
            chaser.scoreValue       = 10;
            chaser.healthDropChance = 0.15f;
            chaser.ammoDropChance   = 0.20f;
            SaveSO(chaser, $"{path}/EnemyData_Chaser.asset");

            var shooter = ScriptableObject.CreateInstance<EnemyData>();
            shooter.enemyName        = "Inimigo Atirador";
            shooter.baseHealth       = 30;
            shooter.moveSpeed        = 2.4f;
            shooter.contactDamage    = 5;
            shooter.attackCooldown   = 1.6f;
            shooter.scoreValue       = 20;
            shooter.healthDropChance = 0.20f;
            shooter.ammoDropChance   = 0.25f;
            SaveSO(shooter, $"{path}/EnemyData_Shooter.asset");

            var boss = ScriptableObject.CreateInstance<EnemyData>();
            boss.enemyName        = "Chefe da Arena";
            boss.baseHealth       = 800;
            boss.moveSpeed        = 2.5f;
            boss.contactDamage    = 25;
            boss.attackCooldown   = 2.0f;
            boss.scoreValue       = 200;
            boss.healthDropChance = 1.0f;
            boss.ammoDropChance   = 1.0f;
            SaveSO(boss, $"{path}/EnemyData_Boss.asset");
        }

        private static void CreateWaveConfigSO()
        {
            string path = $"{ProjectPath}/ScriptableObjects/Waves";
            Directory.CreateDirectory(path);

            var waveConfig = ScriptableObject.CreateInstance<WaveConfig>();
            waveConfig.baseEnemyCount       = 6;
            waveConfig.enemyCountFactor     = 2.5f;
            waveConfig.maxSimultaneous      = 30;
            waveConfig.spawnInterval        = 0.8f;
            waveConfig.restDuration         = 8f;
            waveConfig.shooterRatioPerWave  = 0.03f;
            waveConfig.maxShooterRatio      = 0.40f;
            waveConfig.bossBossWaveInterval = 5;
            waveConfig.bossExtraHealth      = 150f;
            SaveSO(waveConfig, $"{path}/WaveConfig_Default.asset");
        }

        private static void CreateUpgradeSOs()
        {
            string path = $"{ProjectPath}/ScriptableObjects/Upgrades";
            Directory.CreateDirectory(path);

            CreateUpgradeSO(path, "Upgrade_Damage",    "Dano +15%",       "Aumenta o dano em 15%", UpgradeType.Damage);
            CreateUpgradeSO(path, "Upgrade_Reload",    "Recarga -12%",    "Reduz o tempo de recarga em 12%", UpgradeType.ReloadSpeed);
            CreateUpgradeSO(path, "Upgrade_MaxHealth", "Vida Máxima +20", "Aumenta a vida máxima em 20", UpgradeType.MaxHealth);
        }

        private static void CreateUpgradeSO(string path, string assetName, string uName, string desc, UpgradeType type)
        {
            var so = ScriptableObject.CreateInstance<UpgradeData>();
            so.upgradeName = uName;
            so.description = desc;
            so.type        = type;
            so.baseCost    = 80;
            so.costMultiplier = 1.5f;
            so.maxLevel    = 5;
            SaveSO(so, $"{path}/{assetName}.asset");
        }

        // =====================================================================
        // Criação das cenas
        // =====================================================================
        [MenuItem("Tools/Arena/Create Main Menu Scene")]
        public static void CreateMainMenuScene()
        {
            string scenePath = $"{ScenesPath}/MainMenu.unity";
            Directory.CreateDirectory(ScenesPath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGO = new GameObject("MainCamera");
            var cam   = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.05f, 0.12f);
            camGO.tag = "MainCamera";

            var gmGO = new GameObject("GameManager");
            gmGO.AddComponent<GameManager>();

            // tela de abertura procedural (arte + modos + como jogar)
            var canvasGO = new GameObject("UI_MainMenu");
            var canvas   = canvasGO.AddComponent<Canvas>();
            canvas.renderMode    = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera   = cam;
            canvas.planeDistance = 1f;
            canvas.sortingOrder  = 100;
            var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode        = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            canvasGO.AddComponent<ArenaSurvivor.UI.MainMenuScreen>();

            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();
            Debug.Log($"Cena MainMenu criada em: {scenePath}");
        }

        [MenuItem("Tools/Arena/Create Arena Scene")]
        public static void CreateArenaScene()
        {
            string scenePath = $"{ScenesPath}/Arena.unity";
            Directory.CreateDirectory(ScenesPath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- Iluminação ----
            var sunGO = new GameObject("Sun");
            var sun   = sunGO.AddComponent<Light>();
            sun.type      = LightType.Directional;
            sun.color     = new Color(1f, 0.85f, 0.6f);
            sun.intensity = 0.9f;
            sun.shadows   = LightShadows.Soft;
            sunGO.transform.rotation = Quaternion.Euler(40f, -55f, 0f);

            RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor     = new Color(0.45f, 0.32f, 0.65f);
            RenderSettings.ambientEquatorColor = new Color(0.70f, 0.45f, 0.25f);
            RenderSettings.ambientGroundColor  = new Color(0.12f, 0.09f, 0.06f);
            RenderSettings.fog      = true;
            RenderSettings.fogColor = new Color(0.35f, 0.22f, 0.45f);
            RenderSettings.fogMode  = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.02f;

            // ---- Arena (piso + muros) ----
            BuildArena();

            // ---- Jogador ----
            var playerGO = CreatePlayerPlaceholder();

            // ---- Câmera ----
            var camGO = new GameObject("MainCamera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            var camCtrl = camGO.AddComponent<CameraController>();
            camCtrl.SetTarget(playerGO.transform);

            // ---- Sistemas ----
            var systemsGO = new GameObject("--- Systems ---");
            systemsGO.AddComponent<ArenaSurvivor.Utils.DebugOverlay>();

            var inputGO = new GameObject("InputService");
            inputGO.transform.SetParent(systemsGO.transform);
            inputGO.AddComponent<InputService>();

            var poolGO = new GameObject("PoolService");
            poolGO.transform.SetParent(systemsGO.transform);
            poolGO.AddComponent<PoolService>();

            var scoreGO = new GameObject("ScoreService");
            scoreGO.transform.SetParent(systemsGO.transform);
            scoreGO.AddComponent<ScoreService>();

            var audioGO = new GameObject("AudioManager");
            audioGO.transform.SetParent(systemsGO.transform);
            audioGO.AddComponent<AudioManager>();

            var dropGO = new GameObject("DropManager");
            dropGO.transform.SetParent(systemsGO.transform);
            var dropMgr = dropGO.AddComponent<DropManager>();
            var dropSO = new SerializedObject(dropMgr);
            dropSO.FindProperty("healthPickupPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/Pickup_Health.prefab");
            dropSO.FindProperty("ammoPickupPrefab").objectReferenceValue   = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/Pickup_Ammo.prefab");
            dropSO.ApplyModifiedProperties();

            // WaveManager + Spawner
            var waveGO    = new GameObject("WaveManager");
            waveGO.transform.SetParent(systemsGO.transform);
            var waveMgr   = waveGO.AddComponent<WaveManager>();
            var waveSpawn = waveGO.AddComponent<WaveSpawner>();

            var waveConfig = AssetDatabase.LoadAssetAtPath<WaveConfig>($"{ProjectPath}/ScriptableObjects/Waves/WaveConfig_Default.asset");
            var waveMgrSO = new SerializedObject(waveMgr);
            waveMgrSO.FindProperty("config").objectReferenceValue = waveConfig;
            waveMgrSO.FindProperty("spawner").objectReferenceValue = waveSpawn;
            waveMgrSO.ApplyModifiedProperties();

            var waveSpawnSO = new SerializedObject(waveSpawn);
            waveSpawnSO.FindProperty("config").objectReferenceValue             = waveConfig;
            waveSpawnSO.FindProperty("chaserPrefab").objectReferenceValue       = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/Enemy_Chaser.prefab");
            waveSpawnSO.FindProperty("shooterPrefab").objectReferenceValue      = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/Enemy_Shooter.prefab");
            waveSpawnSO.FindProperty("bossPrefab").objectReferenceValue         = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/Enemy_Boss.prefab");
            waveSpawnSO.FindProperty("spawnWarningPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/SpawnWarning.prefab");
            waveSpawnSO.ApplyModifiedProperties();

            // Shop
            var shopGO = new GameObject("ShopManager");
            shopGO.transform.SetParent(systemsGO.transform);
            var shopMgr = shopGO.AddComponent<ShopManager>();
            var shopSO = new SerializedObject(shopMgr);
            var upgradeProp = shopSO.FindProperty("upgrades");
            upgradeProp.arraySize = 3;
            upgradeProp.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<UpgradeData>($"{ProjectPath}/ScriptableObjects/Upgrades/Upgrade_Damage.asset");
            upgradeProp.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<UpgradeData>($"{ProjectPath}/ScriptableObjects/Upgrades/Upgrade_Reload.asset");
            upgradeProp.GetArrayElementAtIndex(2).objectReferenceValue = AssetDatabase.LoadAssetAtPath<UpgradeData>($"{ProjectPath}/ScriptableObjects/Upgrades/Upgrade_MaxHealth.asset");
            shopSO.ApplyModifiedProperties();

            // ---- Canvas da Arena ----
            var canvasGO = new GameObject("Canvas_HUD");
            var canvas   = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode         = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution  = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight   = 0.5f;
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            canvasGO.AddComponent<HUDController>();
            canvasGO.AddComponent<UIManager>();

            // EventSystem
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            // GameManager
            var gmGO = new GameObject("GameManager");
            gmGO.AddComponent<GameManager>();

            // Bake NavMesh
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();

            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();
            Debug.Log($"Cena Arena criada e NavMesh baked em: {scenePath}");
        }

        private static void BuildArena()
        {
            float arenaSize = 22f;
            float wallH     = 3f;
            float wallT     = 1f;

            var floorMat = AssetDatabase.LoadAssetAtPath<Material>($"{ProjectPath}/Materials/Mat_Floor.mat");
            var wallMat  = AssetDatabase.LoadAssetAtPath<Material>($"{ProjectPath}/Materials/Mat_Wall.mat");
            var obsMat   = AssetDatabase.LoadAssetAtPath<Material>($"{ProjectPath}/Materials/Mat_Obstacle.mat");

            // Piso (Static para NavMesh)
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(arenaSize * 2f, 0.5f, arenaSize * 2f);
            floor.transform.position   = new Vector3(0f, -0.25f, 0f);
            floor.tag   = "Ground";
            floor.layer = LayerMask.NameToLayer("Ground") >= 0 ? LayerMask.NameToLayer("Ground") : 0;
            GameObjectUtility.SetStaticEditorFlags(floor, StaticEditorFlags.NavigationStatic);
            if (floor.TryGetComponent<Renderer>(out var fr)) fr.sharedMaterial = floorMat;

            // Muros (Static para NavMesh)
            CreateWall("Wall_N", new Vector3(0f, wallH * 0.5f,  arenaSize),  new Vector3(arenaSize * 2f + wallT * 2f, wallH, wallT), wallMat);
            CreateWall("Wall_S", new Vector3(0f, wallH * 0.5f, -arenaSize),  new Vector3(arenaSize * 2f + wallT * 2f, wallH, wallT), wallMat);
            CreateWall("Wall_E", new Vector3( arenaSize, wallH * 0.5f, 0f),  new Vector3(wallT, wallH, arenaSize * 2f), wallMat);
            CreateWall("Wall_W", new Vector3(-arenaSize, wallH * 0.5f, 0f),  new Vector3(wallT, wallH, arenaSize * 2f), wallMat);

            // Obstáculos de cobertura
            Vector3[] obstaclePositions = {
                new Vector3( 8f, 0.75f,  8f),  new Vector3(-8f, 0.75f,  8f),
                new Vector3( 8f, 0.75f, -8f),  new Vector3(-8f, 0.75f, -8f),
                new Vector3( 0f, 0.75f, 10f),  new Vector3( 0f, 0.75f,-10f),
                new Vector3(10f, 0.75f,  0f),  new Vector3(-10f,0.75f,  0f),
            };

            foreach (var pos in obstaclePositions)
            {
                var obs = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obs.name  = "Obstacle";
                obs.tag   = "Obstacle";
                obs.layer = LayerMask.NameToLayer("Obstacle") >= 0 ? LayerMask.NameToLayer("Obstacle") : 0;
                obs.transform.position   = pos;
                obs.transform.localScale = new Vector3(2f, 1.5f, 2f);
                GameObjectUtility.SetStaticEditorFlags(obs, StaticEditorFlags.NavigationStatic);
                if (obs.TryGetComponent<Renderer>(out var or)) or.sharedMaterial = obsMat;
            }

            // Luzes pontuais
            CreateColorLight("Light_Purple1", new Vector3( 8f, 2f,  8f), new Color(0.7f, 0.2f, 1.0f), 8f, 14f);
            CreateColorLight("Light_Cyan1",   new Vector3(-8f, 2f, -8f), new Color(0.2f, 0.8f, 1.0f), 8f, 14f);
            CreateColorLight("Light_Orange1", new Vector3( 0f, 2f, 10f), new Color(1.0f, 0.6f, 0.1f), 8f, 12f);
            CreateColorLight("Light_Orange2", new Vector3( 0f, 2f,-10f), new Color(1.0f, 0.6f, 0.1f), 8f, 12f);
        }

        private static GameObject CreatePlayerPlaceholder()
        {
            var playerGO = new GameObject("Player");
            playerGO.tag = "Player";
            playerGO.layer = LayerMask.NameToLayer("Player") >= 0 ? LayerMask.NameToLayer("Player") : 0;
            playerGO.transform.position = Vector3.zero;

            // Corpo
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(playerGO.transform);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            body.transform.localScale    = Vector3.one;
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());

            var playerMat = AssetDatabase.LoadAssetAtPath<Material>($"{ProjectPath}/Materials/Mat_Player.mat");
            if (playerMat != null) body.GetComponent<Renderer>().sharedMaterial = playerMat;

            // Olhos
            CreateEyes(playerGO.transform, 1.5f, 0.45f);

            // CharacterController
            var cc = playerGO.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.4f;
            cc.center = Vector3.up;

            // Componentes
            playerGO.AddComponent<PlayerController>();
            playerGO.AddComponent<PlayerHealth>();
            playerGO.AddComponent<PlayerStats>();
            var shooter = playerGO.AddComponent<PlayerShooter>();
            playerGO.AddComponent<PlayerAnimation>();

            // Cria slots de armas
            var weaponHolder = new GameObject("WeaponHolder");
            weaponHolder.transform.SetParent(playerGO.transform);
            weaponHolder.transform.localPosition = new Vector3(0.4f, 1.0f, 0.5f);

            var pistolWpn  = CreateWeaponInstance(weaponHolder.transform, "Weapon_Pistol",  "WeaponData_Pistol");
            var shotgunWpn = CreateWeaponInstance(weaponHolder.transform, "Weapon_Shotgun", "WeaponData_Shotgun");
            var energyWpn  = CreateWeaponInstance(weaponHolder.transform, "Weapon_Energy",  "WeaponData_Energy");

            // Configura slots no PlayerShooter
            var shooterSO = new SerializedObject(shooter);
            var slotsProp = shooterSO.FindProperty("weaponSlots");
            slotsProp.arraySize = 3;
            slotsProp.GetArrayElementAtIndex(0).objectReferenceValue = pistolWpn;
            slotsProp.GetArrayElementAtIndex(1).objectReferenceValue = shotgunWpn;
            slotsProp.GetArrayElementAtIndex(2).objectReferenceValue = energyWpn;
            shooterSO.FindProperty("weaponHolder").objectReferenceValue = weaponHolder.transform;
            shooterSO.ApplyModifiedProperties();

            return playerGO;
        }

        private static Weapon CreateWeaponInstance(Transform parent, string goName, string dataName)
        {
            var wpnGO = new GameObject(goName);
            wpnGO.transform.SetParent(parent, false);

            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(wpnGO.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0f, 0.5f);

            var audio = wpnGO.AddComponent<AudioSource>();
            audio.playOnAwake = false;

            var wpn = wpnGO.AddComponent<Weapon>();
            var so = new SerializedObject(wpn);
            var dataAsset = AssetDatabase.LoadAssetAtPath<WeaponData>($"{ProjectPath}/ScriptableObjects/Weapons/{dataName}.asset");
            so.FindProperty("data").objectReferenceValue        = dataAsset;
            so.FindProperty("muzzlePoint").objectReferenceValue = muzzle.transform;
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.ApplyModifiedProperties();

            return wpn;
        }

        [MenuItem("Tools/Arena/Create Sandbox Scene")]
        public static void CreateSandboxScene()
        {
            string scenePath = $"{ScenesPath}/Sandbox.unity";
            Directory.CreateDirectory(ScenesPath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var camGO = GameObject.Find("Main Camera") ?? new GameObject("MainCamera");
            camGO.tag = "MainCamera";
            camGO.transform.position = new Vector3(0f, 8f, -10f);
            camGO.transform.rotation = Quaternion.Euler(40f, 0f, 0f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "SandboxFloor";
            floor.transform.localScale = Vector3.one * 3f;

            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();
            Debug.Log($"Cena Sandbox criada em: {scenePath}");
        }

        [MenuItem("Tools/Arena/Setup Build Settings")]
        public static void SetupBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene($"{ScenesPath}/MainMenu.unity", true),
                new EditorBuildSettingsScene($"{ScenesPath}/Arena.unity",    true),
                new EditorBuildSettingsScene($"{ScenesPath}/Sandbox.unity",  true),
            };
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("Build Settings configurado.");
        }

        [MenuItem("Tools/Arena/Validate Project")]
        public static void ValidateProject()
        {
            int issues = 0;

            var materials = AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project" });
            foreach (var guid in materials)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null && mat.shader.name == "Hidden/InternalErrorShader")
                {
                    Debug.LogWarning($"[Validate] Material com shader rosa: {path}");
                    issues++;
                }
            }

            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" });
            foreach (var guid in prefabs)
            {
                string path   = AssetDatabase.GUIDToAssetPath(guid);
                var    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    if (prefab.name.Contains("Warning") || prefab.name.Contains("VFX") || prefab.name.Contains("Flash"))
                        continue;

                    if (prefab.GetComponentInChildren<Collider>() == null)
                    {
                        Debug.LogWarning($"[Validate] Prefab sem Collider: {path}");
                        issues++;
                    }
                }
            }

            if (issues == 0)
                Debug.Log("[Validate] Projeto validado com sucesso! Nenhum problema encontrado.");
            else
                Debug.LogWarning($"[Validate] {issues} problema(s) encontrado(s). Verifique os warnings acima.");

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Validação do Projeto",
                    issues == 0 ? "Projeto OK! Nenhum problema." : $"{issues} problema(s) encontrado(s). Veja o Console.",
                    "OK");
            }
        }

        [MenuItem("Tools/Arena/Fix Imported Assets")]
        public static void FixImportedAssets()
        {
            var materials = AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project/ThirdParty" });
            int fixed_count = 0;
            foreach (var guid in materials)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                if (mat.shader.name == "Standard" || mat.shader.name == "Hidden/InternalErrorShader")
                {
                    mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                    EditorUtility.SetDirty(mat);
                    fixed_count++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[FixAssets] {fixed_count} materiais corrigidos para URP/Lit.");
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("Fix Imported Assets", $"{fixed_count} materiais corrigidos.", "OK");
        }

        private static void CreateWall(string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name  = name;
            wall.tag   = "Wall";
            wall.layer = LayerMask.NameToLayer("Obstacle") >= 0 ? LayerMask.NameToLayer("Obstacle") : 0;
            wall.transform.position   = pos;
            wall.transform.localScale = scale;
            GameObjectUtility.SetStaticEditorFlags(wall, StaticEditorFlags.NavigationStatic);
            if (wall.TryGetComponent<Renderer>(out var r)) r.sharedMaterial = mat;
        }

        private static void CreateColorLight(string name, Vector3 pos, Color color, float intensity, float range)
        {
            var go    = new GameObject(name);
            go.transform.position = pos;
            var light = go.AddComponent<Light>();
            light.type      = LightType.Point;
            light.color     = color;
            light.intensity = intensity;
            light.range     = range;
        }

        private static void AddTag(string tag)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var tagsProperty = tagManager.FindProperty("tags");

            for (int i = 0; i < tagsProperty.arraySize; i++)
                if (tagsProperty.GetArrayElementAtIndex(i).stringValue == tag) return;

            tagsProperty.arraySize++;
            tagsProperty.GetArrayElementAtIndex(tagsProperty.arraySize - 1).stringValue = tag;
            tagManager.ApplyModifiedProperties();
        }

        private static void AddLayer(string layerName, int index)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layersProperty = tagManager.FindProperty("layers");

            if (index >= layersProperty.arraySize) return;
            var layerProp = layersProperty.GetArrayElementAtIndex(index);
            if (!string.IsNullOrEmpty(layerProp.stringValue)) return;

            layerProp.stringValue = layerName;
            tagManager.ApplyModifiedProperties();
        }

        private static void SaveSO(ScriptableObject so, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) != null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(so, path);
        }
    }
}
