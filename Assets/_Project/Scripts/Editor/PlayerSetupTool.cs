using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using ArenaSurvivor.Player;
using ArenaSurvivor.Weapons;

namespace ArenaSurvivor.Editor
{
    public static class PlayerSetupTool
    {
        private const string SCENE_ARENA   = "Assets/_Project/Scenes/Arena.unity";
        private const string INBOX_MIXAMO  = "Assets/_Project/ThirdParty/_INBOX/Mixamo";
        private const string INBOX_BLASTER = "Assets/_Project/ThirdParty/_INBOX/Kenney_BlasterKit";
        private const string MAT_DIR       = "Assets/_Project/Materials";
        private const string ANIM_DIR      = "Assets/_Project/Animations/Player";

        [MenuItem("Tools/Arena/Setup Player (Tarefa 2)", priority = 10)]
        public static void SetupPlayer()
        {
            SetupPlayerInternal(false);
        }

        public static void SetupPlayerBatch()
        {
            SetupPlayerInternal(true);
        }

        private static void SetupPlayerInternal(bool isBatch)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != SCENE_ARENA)
            {
                if (File.Exists(SCENE_ARENA))
                    scene = EditorSceneManager.OpenScene(SCENE_ARENA, OpenSceneMode.Single);
            }

            // 1. Configurar ModelImporter do FBX do Jogador para escala de ~1.8m
            string playerFBXPath = FindFBXPath("Gunplay") ?? FindFBXPath("Shooting Gun");
            if (string.IsNullOrEmpty(playerFBXPath))
            {
                Debug.LogError("[PlayerSetupTool] FBX do jogador não encontrado!");
                return;
            }

            FixPlayerFBXImporter(playerFBXPath);

            // 2. Criar ou atualizar Animator Controller com BlendTree / Animações de movimento
            var animCtrl = SetupPlayerAnimatorController();

            // 3. Montar o Jogador na cena Arena
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO == null)
            {
                playerGO = new GameObject("Player");
                playerGO.tag = "Player";
                playerGO.layer = LayerMask.NameToLayer("Player") >= 0 ? LayerMask.NameToLayer("Player") : 0;
            }

            playerGO.transform.position = Vector3.zero;
            playerGO.transform.rotation = Quaternion.identity;
            playerGO.transform.localScale = Vector3.one;

            // Limpar visuais antigos / primitivas
            for (int i = playerGO.transform.childCount - 1; i >= 0; i--)
            {
                var child = playerGO.transform.GetChild(i);
                if (child.name.StartsWith("Body") || child.name.StartsWith("Eye_") || child.name.StartsWith("Model_Player"))
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }

            // Instanciar o modelo 3D do Jogador
            var fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(playerFBXPath);
            var modelInstance = UnityEngine.Object.Instantiate(fbxAsset, playerGO.transform);
            modelInstance.name = "Model_Player";
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one;

            // Aplicar Materiais estilizados (Hero suit + Visor/Joints glow)
            var heroMat  = AssetDatabase.LoadAssetAtPath<Material>($"{MAT_DIR}/Mat_Player_Hero.mat");
            var visorMat = AssetDatabase.LoadAssetAtPath<Material>($"{MAT_DIR}/Mat_Player_Visor.mat");

            foreach (var r in modelInstance.GetComponentsInChildren<Renderer>())
            {
                if (r.gameObject.name.IndexOf("Joints", StringComparison.OrdinalIgnoreCase) >= 0 && visorMat != null)
                    r.sharedMaterial = visorMat;
                else if (heroMat != null)
                    r.sharedMaterial = heroMat;

                if (r is SkinnedMeshRenderer smr)
                    smr.updateWhenOffscreen = true;
            }

            // Medir Bounds do modelo instanciado
            var renderers = modelInstance.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            float measuredHeight = bounds.size.y;
            Debug.Log($"[PlayerSetupTool] Altura medida inicial do modelo: {measuredHeight:F3}m");

            // Se a altura não estiver próxima de 1.8m (entre 1.6m e 1.9m), ajustar
            if (measuredHeight < 1.0f || measuredHeight > 2.5f)
            {
                float targetScale = 1.80f / Mathf.Max(0.01f, measuredHeight);
                modelInstance.transform.localScale = Vector3.one * targetScale;
                Debug.Log($"[PlayerSetupTool] Escala normalizada do Model_Player para {targetScale:F2} (altura alvo ~1.80m)");
            }

            // CharacterController
            var cc = playerGO.GetComponent<CharacterController>() ?? playerGO.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0, 0.9f, 0);
            cc.stepOffset = 0.3f;
            cc.skinWidth = 0.05f;

            // Componentes de Lógica
            var pc = playerGO.GetComponent<PlayerController>() ?? playerGO.AddComponent<PlayerController>();
            var ph = playerGO.GetComponent<PlayerHealth>() ?? playerGO.AddComponent<PlayerHealth>();
            var ps = playerGO.GetComponent<PlayerStats>() ?? playerGO.AddComponent<PlayerStats>();
            var shooter = playerGO.GetComponent<PlayerShooter>() ?? playerGO.AddComponent<PlayerShooter>();
            var pAnim = playerGO.GetComponent<PlayerAnimation>() ?? playerGO.AddComponent<PlayerAnimation>();

            // Animator no root ou no modelo
            var anim = playerGO.GetComponent<Animator>() ?? playerGO.AddComponent<Animator>();
            anim.runtimeAnimatorController = animCtrl;

            // Também garantir que o animator no modelInstance compartilha ou desativa duplicado
            var childAnim = modelInstance.GetComponent<Animator>();
            if (childAnim != null && childAnim != anim)
            {
                // Deixa o Animator principal no root com o Avatar do modelo
                anim.avatar = childAnim.avatar;
                UnityEngine.Object.DestroyImmediate(childAnim);
            }

            // WeaponHolder e Arma 3D
            SetupPlayerWeapons(playerGO, shooter);

            // 4. Configurar Câmera (inclinação ~55°, altura e distância para cobrir 8-12% da tela)
            SetupCamera(playerGO.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[PlayerSetupTool] Jogador configurado com sucesso! Altura final: {GetPlayerHeight(playerGO):F3}m");
            if (isBatch) EditorApplication.Exit(0);
        }

        private static void FixPlayerFBXImporter(string fbxPath)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) return;

            bool dirty = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                dirty = true;
            }

            // Mixamo com useFileUnits converte para metros. Se globalScale estava 0.01, corrigimos para 1.0
            if (importer.globalScale < 0.5f)
            {
                importer.globalScale = 1.0f;
                dirty = true;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
                Debug.Log($"[PlayerSetupTool] Reimportado {fbxPath} com globalScale: {importer.globalScale}");
            }
        }

        private static RuntimeAnimatorController SetupPlayerAnimatorController()
        {
            string path = $"{ANIM_DIR}/AnimatorController_Player.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                Directory.CreateDirectory(ANIM_DIR);
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            }

            // Parâmetros
            controller.parameters = new AnimatorControllerParameter[0];
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
            controller.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die",   AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;
            sm.states = new ChildAnimatorState[0];
            sm.anyStateTransitions = new AnimatorStateTransition[0];

            var gunClip   = FindAnimationClip("Gunplay");
            var shootClip = FindAnimationClip("Shooting Gun") ?? FindAnimationClip("Shooting") ?? gunClip;

            // Idle State
            var sIdle = sm.AddState("Idle");
            sIdle.motion = gunClip;
            sm.defaultState = sIdle;

            // Move State (BlendTree ou animação de locomoção)
            var sMove = sm.AddState("Move");
            sMove.motion = gunClip; // Loop de corrida/tiro Mixamo

            // Shoot State
            var sShoot = sm.AddState("Shoot");
            sShoot.motion = shootClip;

            // Die State
            var sDie = sm.AddState("Die");

            // Transições Idle <-> Move
            var t1 = sIdle.AddTransition(sMove);
            t1.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            t1.duration = 0.15f;

            var t2 = sMove.AddTransition(sIdle);
            t2.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");
            t2.duration = 0.15f;

            // AnyState -> Shoot
            var tShoot = sm.AddAnyStateTransition(sShoot);
            tShoot.AddCondition(AnimatorConditionMode.If, 0, "Shoot");
            tShoot.duration = 0.05f;
            tShoot.canTransitionToSelf = false;

            var tShootBack = sShoot.AddTransition(sIdle);
            tShootBack.hasExitTime = true;
            tShootBack.exitTime = 0.75f;
            tShootBack.duration = 0.15f;

            // AnyState -> Die
            var tDie = sm.AddAnyStateTransition(sDie);
            tDie.AddCondition(AnimatorConditionMode.If, 0, "Die");
            tDie.duration = 0.05f;
            tDie.canTransitionToSelf = false;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void SetupPlayerWeapons(GameObject playerGO, PlayerShooter shooter)
        {
            var weaponHolder = playerGO.transform.Find("WeaponHolder");
            if (weaponHolder == null)
            {
                var whGO = new GameObject("WeaponHolder");
                whGO.transform.SetParent(playerGO.transform);
                whGO.transform.localPosition = new Vector3(0.35f, 1.0f, 0.45f);
                weaponHolder = whGO.transform;
            }

            // Localizar mão direita se rig Humanoid estiver ativo
            var anim = playerGO.GetComponent<Animator>();
            if (anim != null && anim.isHuman)
            {
                var rightHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
                if (rightHand != null)
                {
                    weaponHolder.SetParent(rightHand);
                    weaponHolder.localPosition = new Vector3(0.08f, 0.05f, 0.12f);
                    weaponHolder.localRotation = Quaternion.Euler(0, 90f, 0);
                }
            }

            // Encontrar modelo Blaster da Kenney
            string blasterFBX = FindKenneyBlasterFBX();
            var blasterAsset  = !string.IsNullOrEmpty(blasterFBX) ? AssetDatabase.LoadAssetAtPath<GameObject>(blasterFBX) : null;
            var blasterMat    = AssetDatabase.LoadAssetAtPath<Material>($"{MAT_DIR}/Mat_BlasterColormap.mat");

            // Criar os 3 slots de arma
            var pistolSO  = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_Project/ScriptableObjects/Weapons/WeaponData_Pistol.asset");
            var shotgunSO = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_Project/ScriptableObjects/Weapons/WeaponData_Shotgun.asset");
            var energySO  = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_Project/ScriptableObjects/Weapons/WeaponData_Energy.asset");

            var slotPistol  = SetupWeaponSlot(weaponHolder, "Weapon_Pistol",  pistolSO,  blasterAsset, blasterMat, true);
            var slotShotgun = SetupWeaponSlot(weaponHolder, "Weapon_Shotgun", shotgunSO, blasterAsset, blasterMat, false);
            var slotEnergy  = SetupWeaponSlot(weaponHolder, "Weapon_Energy",  energySO,  blasterAsset, blasterMat, false);

            // Atribuir slots no PlayerShooter
            var shooterSO = new SerializedObject(shooter);
            var slotsProp = shooterSO.FindProperty("weaponSlots");
            slotsProp.arraySize = 3;
            slotsProp.GetArrayElementAtIndex(0).objectReferenceValue = slotPistol;
            slotsProp.GetArrayElementAtIndex(1).objectReferenceValue = slotShotgun;
            slotsProp.GetArrayElementAtIndex(2).objectReferenceValue = slotEnergy;
            var holderProp = shooterSO.FindProperty("weaponHolder");
            if (holderProp != null) holderProp.objectReferenceValue = weaponHolder;
            shooterSO.ApplyModifiedProperties();
        }

        private static Weapon SetupWeaponSlot(Transform holder, string slotName, WeaponData data, GameObject blasterModel, Material mat, bool startActive)
        {
            var child = holder.Find(slotName);
            GameObject slotGO;
            if (child != null)
            {
                slotGO = child.gameObject;
            }
            else
            {
                slotGO = new GameObject(slotName);
                slotGO.transform.SetParent(holder, false);
            }

            slotGO.transform.localPosition = Vector3.zero;
            slotGO.transform.localRotation = Quaternion.identity;

            // ShootPoint
            var sp = slotGO.transform.Find("ShootPoint");
            if (sp == null)
            {
                var spGO = new GameObject("ShootPoint");
                spGO.transform.SetParent(slotGO.transform, false);
                spGO.transform.localPosition = new Vector3(0, 0, 0.45f);
            }

            // Modelo visual do Blaster
            var vis = slotGO.transform.Find("Visual");
            if (vis == null && blasterModel != null)
            {
                var vGO = UnityEngine.Object.Instantiate(blasterModel, slotGO.transform);
                vGO.name = "Visual";
                vGO.transform.localPosition = Vector3.zero;
                vGO.transform.localRotation = Quaternion.Euler(0, 90f, 0);
                vGO.transform.localScale = Vector3.one * 0.55f;

                if (mat != null)
                {
                    foreach (var r in vGO.GetComponentsInChildren<Renderer>())
                        r.sharedMaterial = mat;
                }
            }

            var weapon = slotGO.GetComponent<Weapon>() ?? slotGO.AddComponent<Weapon>();
            var so = new SerializedObject(weapon);
            var dataProp = so.FindProperty("data");
            if (dataProp != null) dataProp.objectReferenceValue = data;
            var muzzleProp = so.FindProperty("muzzlePoint");
            if (muzzleProp != null) muzzleProp.objectReferenceValue = slotGO.transform.Find("ShootPoint");
            so.ApplyModifiedProperties();

            slotGO.SetActive(startActive);
            return weapon;
        }

        private static void SetupCamera(Transform playerTarget)
        {
            var camGO = GameObject.FindGameObjectWithTag("MainCamera");
            if (camGO == null)
            {
                camGO = new GameObject("MainCamera");
                camGO.tag = "MainCamera";
                camGO.AddComponent<Camera>();
            }

            var cam = camGO.GetComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 150f;

            var camCtrl = camGO.GetComponent<CameraController>() ?? camGO.AddComponent<CameraController>();
            var camSO = new SerializedObject(camCtrl);
            camSO.FindProperty("target").objectReferenceValue = playerTarget;
            camSO.FindProperty("height").floatValue           = 11.0f;
            camSO.FindProperty("tilt").floatValue             = 52.0f;
            camSO.FindProperty("smoothTime").floatValue       = 0.1f;
            camSO.ApplyModifiedProperties();

            // Posição inicial correta
            Vector3 offset = Quaternion.Euler(55f, 0f, 0f) * Vector3.back * 16.5f;
            camGO.transform.position = playerTarget.position + offset;
            camGO.transform.LookAt(playerTarget.position);
        }

        public static float GetPlayerHeight(GameObject playerGO)
        {
            if (playerGO == null) return 0f;
            var rends = playerGO.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return 0f;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b.size.y;
        }

        private static string FindFBXPath(string searchName)
        {
            string[] guids = AssetDatabase.FindAssets($"{searchName} t:Model", new[] { INBOX_MIXAMO });
            if (guids.Length > 0) return AssetDatabase.GUIDToAssetPath(guids[0]);
            return null;
        }

        private static string FindKenneyBlasterFBX()
        {
            string[] preferred = { "blaster-j", "blaster-e", "blaster-a" };
            foreach (var p in preferred)
            {
                string[] guids = AssetDatabase.FindAssets($"{p} t:Model", new[] { INBOX_BLASTER });
                if (guids.Length > 0) return AssetDatabase.GUIDToAssetPath(guids[0]);
            }
            return null;
        }

        private static AnimationClip FindAnimationClip(string clipName)
        {
            string[] guids = AssetDatabase.FindAssets($"{clipName} t:Model", new[] { INBOX_MIXAMO });
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
    }
}
