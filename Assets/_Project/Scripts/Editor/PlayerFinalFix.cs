using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using ArenaSurvivor.Player;
using ArenaSurvivor.Weapons;

namespace ArenaSurvivor.Editor
{
    // Corrige rig/avatar, animações, camada de braços, arma na mão e mira do jogador.
    public static class PlayerFinalFix
    {
        public const string PLAYER_FBX = "Assets/_Project/ThirdParty/_INBOX/Mixamo/Gunplay.fbx";
        public const string AIM_FBX    = "Assets/_Project/ThirdParty/_INBOX/Mixamo/Shooting Gun.fbx";
        public const string WALK_FBX   = "Assets/_Project/ThirdParty/_INBOX/Mixamo/Zombie Walk.fbx";
        public const string RUN_FBX    = "Assets/_Project/ThirdParty/_INBOX/Mixamo/Zombie Run.fbx";

        public const string CTRL_PATH  = "Assets/_Project/Animations/Player/AnimatorController_Player.controller";
        public const string MASK_PATH  = "Assets/_Project/Animations/Player/Mask_UpperBody.mask";
        public const string ARENA      = "Assets/_Project/Scenes/Arena.unity";
        public const string LOG_PATH   = "docs/fix_player_log.txt";
        // Instante do clipe Gunplay com pose de mira frontal de dois punhos (0..1)
        public const float  AIM_OFFSET = 0.25f;

        [MenuItem("Tools/Arena/Fix Player Final (Rig + Anim + Arma)", priority = 25)]
        public static void Run() { RunInternal(false); }

        public static void RunBatch() { RunInternal(true); }

        private static void RunInternal(bool isBatch)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== [FIX PLAYER] " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");

            try
            {
                // ---------- 1. IMPORTAÇÃO DO MODELO (com skin) ----------
                ConfigureImporter(PLAYER_FBX, null, true);
                var avatar = AssetDatabase.LoadAllAssetsAtPath(PLAYER_FBX).OfType<Avatar>().FirstOrDefault();
                sb.AppendLine($"Avatar do Player: {(avatar != null ? avatar.name : "NULO")} | isValid={avatar?.isValid} | isHuman={avatar?.isHuman}");

                // ---------- 2. FBX DE ANIMAÇÃO: Humanoid + Copy From Other Avatar ----------
                ConfigureImporter(AIM_FBX,  avatar, true);
                ConfigureImporter(WALK_FBX, avatar, true);
                ConfigureImporter(RUN_FBX,  avatar, true);

                var idleClip = LoadClip(PLAYER_FBX);   // Gunplay: pose de rifle do próprio personagem
                var walkClip = LoadClip(WALK_FBX);
                var runClip  = LoadClip(RUN_FBX);
                sb.AppendLine($"Clipes: Idle/Aim='{idleClip?.name}' ({idleClip?.length:F2}s) | Walk='{walkClip?.name}' ({walkClip?.length:F2}s) | Run='{runClip?.name}' ({runClip?.length:F2}s)");

                if (idleClip == null || walkClip == null)
                {
                    sb.AppendLine("ERRO: clipes essenciais não encontrados.");
                    File.WriteAllText(LOG_PATH, sb.ToString(), Encoding.UTF8);
                    if (isBatch) EditorApplication.Exit(2);
                    return;
                }

                // ---------- 3. CONTROLLER ----------
                var ctrl = BuildController(idleClip, walkClip, runClip);
                sb.AppendLine($"Controller: {CTRL_PATH} | camadas={ctrl.layers.Length} | params={string.Join(",", ctrl.parameters.Select(p => p.name))}");

                // ---------- 4. CENA ARENA ----------
                var scene = EditorSceneManager.OpenScene(ARENA, OpenSceneMode.Single);
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player == null)
                {
                    sb.AppendLine("ERRO: Player não encontrado na Arena.");
                    File.WriteAllText(LOG_PATH, sb.ToString(), Encoding.UTF8);
                    if (isBatch) EditorApplication.Exit(3);
                    return;
                }

                var anim = player.GetComponent<Animator>();
                anim.runtimeAnimatorController = ctrl;
                anim.avatar = avatar;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                // Skin / renderizadores
                var smrs = player.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                sb.AppendLine($"SkinnedMeshRenderers no Player: {smrs.Length}");
                foreach (var smr in smrs)
                {
                    int bones = smr.bones?.Length ?? 0;
                    sb.AppendLine($"  - {smr.name}: verts={smr.sharedMesh?.vertexCount} bones={bones} bindposes={smr.sharedMesh?.bindposes.Length} rootBone={(smr.rootBone != null ? smr.rootBone.name : "NULO")} materiais={smr.sharedMaterials.Length}");
                }

                // Altura visual (bounds em mundo, pose de repouso)
                var bounds = new Bounds(player.transform.position, Vector3.zero);
                bool first = true;
                foreach (var r in player.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
                    if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds);
                }
                sb.AppendLine($"Bounds do Player: altura={bounds.size.y:F2}m largura={bounds.size.x:F2}m (centro {bounds.center})");

                // ---------- 5. ARMA NA MÃO + MUZZLE ----------
                FixWeapon(player, anim, idleClip, sb);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

                // Dados de arma: alcance coerente com ~2s de voo a 40 m/s
                SetWeaponRange("Assets/_Project/ScriptableObjects/Weapons/WeaponData_Pistol.asset", 80f);
                SetWeaponRange("Assets/_Project/ScriptableObjects/Weapons/WeaponData_Shotgun.asset", 60f);
                SetWeaponRange("Assets/_Project/ScriptableObjects/Weapons/WeaponData_Energy.asset", 80f);

                // ---------- 6. EFEITO DE IMPACTO NO PROJÉTIL ----------
                WireImpact();

                AssetDatabase.SaveAssets();
                sb.AppendLine("OK: cena Arena salva e assets persistidos.");
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

        // ------------------------------------------------------------------
        public static void ConfigureImporter(string path, Avatar sourceAvatar, bool loop)
        {
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null) { Debug.LogWarning("Importer não encontrado: " + path); return; }

            mi.animationType = ModelImporterAnimationType.Human;
            mi.optimizeGameObjects = false;
            if (sourceAvatar == null)
            {
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            }
            else
            {
                mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                mi.sourceAvatar = sourceAvatar;
            }

            var clips = mi.clipAnimations;
            if (clips == null || clips.Length == 0) clips = mi.defaultClipAnimations;
            if (clips != null)
            {
                foreach (var c in clips)
                {
                    c.loopTime = loop;
                    c.loopPose = loop;
                    c.lockRootRotation = true;   // Bake Into Pose - Rotation
                    c.lockRootHeightY = true;    // Bake Into Pose - Position Y
                    c.lockRootPositionXZ = true; // Bake Into Pose - Position XZ
                    c.keepOriginalOrientation = true;
                    c.keepOriginalPositionY = true;
                    c.keepOriginalPositionXZ = true;
                }
                mi.clipAnimations = clips;
            }
            mi.SaveAndReimport();
        }

        private static AnimationClip LoadClip(string fbx)
        {
            return AssetDatabase.LoadAllAssetsAtPath(fbx)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }

        private static AnimatorController BuildController(AnimationClip idle, AnimationClip walk, AnimationClip run)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CTRL_PATH);
            if (ctrl == null)
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(CTRL_PATH);

            // Limpa camadas extras e estados antigos
            while (ctrl.layers.Length > 1)
                ctrl.RemoveLayer(ctrl.layers.Length - 1);

            var root = ctrl.layers[0].stateMachine;
            foreach (var st in root.states) root.RemoveState(st.state);
            foreach (var ssm in root.stateMachines) root.RemoveStateMachine(ssm.stateMachine);
            foreach (var tr in root.anyStateTransitions) root.RemoveAnyStateTransition(tr);
            foreach (var tr in root.entryTransitions) root.RemoveEntryTransition(tr);

            // Parâmetros
            ctrl.parameters = new AnimatorControllerParameter[0];
            AddFloat(ctrl, "Speed");
            AddFloat(ctrl, "LegSpeed");
            AddFloat(ctrl, "MoveX");
            AddFloat(ctrl, "MoveZ");
            AddTrigger(ctrl, "Shoot");
            AddTrigger(ctrl, "Die");

            // Camada base: pose de rifle (braços + corpo) sempre ativa
            var baseState = root.AddState("AimIdle", new Vector3(300f, 60f, 0f));
            baseState.motion = idle;
            baseState.speed = 0f;               // congela a pose de mira
            baseState.cycleOffset = AIM_OFFSET;
            baseState.writeDefaultValues = true;
            root.defaultState = baseState;

            var die = root.AddState("Die", new Vector3(600f, 220f, 0f));
            die.speed = 0f;
            var dieTr = root.AddAnyStateTransition(die);
            dieTr.hasExitTime = false;
            dieTr.duration = 0.1f;
            dieTr.AddCondition(AnimatorConditionMode.If, 0f, "Die");

            // Máscara só das pernas: a camada de locomoção sobrescreve apenas as pernas
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MASK_PATH);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, MASK_PATH);
            }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, true);
            EditorUtility.SetDirty(mask);

            var layer = new AnimatorControllerLayer
            {
                name = "Legs",
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Override,
                avatarMask = mask,
                stateMachine = new AnimatorStateMachine()
            };
            ctrl.AddLayer(layer);
            AssetDatabase.AddObjectToAsset(layer.stateMachine, ctrl);

            // Blend Tree de locomoção (pernas)
            var tree = new BlendTree
            {
                name = "LocomotionLegs",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(tree, ctrl);
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, 0.6f);

            var legs = ctrl.layers[1].stateMachine;
            var loco = legs.AddState("Locomotion", new Vector3(300f, 60f, 0f));
            loco.motion = tree;
            loco.speed = 1f;
            loco.speedParameterActive = true;
            loco.speedParameter = "LegSpeed";
            loco.writeDefaultValues = true;
            legs.defaultState = loco;

            Debug.Log($"[FIX] Máscara pernas: LeftLeg={mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg)} RightLeg={mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg)} LeftArm={mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm)}");

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            return ctrl;
        }

        private static void AddFloat(AnimatorController c, string name)
        {
            c.AddParameter(new AnimatorControllerParameter { name = name, type = AnimatorControllerParameterType.Float, defaultFloat = 0f });
        }

        private static void AddTrigger(AnimatorController c, string name)
        {
            c.AddParameter(new AnimatorControllerParameter { name = name, type = AnimatorControllerParameterType.Trigger });
        }

        private static void FixWeapon(GameObject player, Animator anim, AnimationClip poseClip, StringBuilder sb)
        {
            Transform hand = anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (hand == null)
            {
                hand = FindDeep(player.transform, "mixamorig:RightHand");
                sb.AppendLine("AVISO: osso RightHand encontrado por nome.");
            }
            if (hand == null) { sb.AppendLine("ERRO: mão direita não encontrada."); return; }

            var socket = hand.Find("WeaponSocket");
            if (socket == null)
            {
                socket = new GameObject("WeaponSocket").transform;
                socket.SetParent(hand, false);
            }
            socket.localPosition = new Vector3(0.08f, 0.04f, 0.12f);
            socket.localScale = Vector3.one;

            // Garante as 3 armas dentro do socket
            foreach (var wName in new[] { "Weapon_Pistol", "Weapon_Shotgun", "Weapon_Energy" })
            {
                var w = FindDeep(player.transform, wName);
                if (w == null) continue;
                if (w.parent != socket) w.SetParent(socket, false);
                w.localPosition = Vector3.zero;
                w.localRotation = Quaternion.identity;
            }

            // O cano visual da arma precisa apontar para +Z (mesmo eixo do Muzzle)
            foreach (var w in socket.GetComponentsInChildren<Transform>(true))
            {
                if (w.name == "Visual") w.localRotation = Quaternion.identity;
            }

            // Remove holders soltos antigos (fora da mão)
            var stray = player.GetComponentsInChildren<Transform>(true)
                .Where(t => t != socket && (t.name == "WeaponHolder"))
                .ToArray();
            foreach (var t in stray) UnityEngine.Object.DestroyImmediate(t.gameObject);

            // Alinha o cano com a frente do personagem no instante de mira congelado
            Quaternion refWorld = Quaternion.LookRotation(player.transform.forward, player.transform.up);
            poseClip.SampleAnimation(player, poseClip.length * AIM_OFFSET);
            socket.localRotation = Quaternion.Inverse(hand.rotation) * refWorld;

            // Muzzle na ponta do cano
            foreach (var w in socket.GetComponentsInChildren<Transform>(true))
            {
                if (w.name == "ShootPoint") w.name = "Muzzle";
            }

            // Atualiza referências do PlayerShooter
            var shooter = player.GetComponent<PlayerShooter>();
            if (shooter != null)
            {
                var so = new SerializedObject(shooter);
                var wh = so.FindProperty("weaponHolder");
                if (wh != null) wh.objectReferenceValue = socket;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var muzzle = FindDeep(socket, "Muzzle");
            sb.AppendLine($"Arma: socket em '{Path(socket)}' | muzzle='{(muzzle != null ? Path(muzzle) : "NULO")}' | socket localRot={socket.localRotation.eulerAngles}");

            // Sanidade: arma não atravessa o corpo (distância do cano ao eixo do jogador)
            if (muzzle != null)
            {
                Vector3 local = player.transform.InverseTransformPoint(muzzle.position);
                sb.AppendLine($"Muzzle em espaço local do Player: {local} (z>0 = à frente)");
            }
        }

        private static string Path(Transform t)
        {
            var parts = new List<string>();
            while (t != null) { parts.Add(t.name); t = t.parent; }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var f = FindDeep(parent.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }

        private static void SetWeaponRange(string path, float range)
        {
            var so = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            if (so == null) return;
            so.projectileRange = range;
            so.projectileSpeed = 40f;
            EditorUtility.SetDirty(so);
        }

        // Liga o efeito de impacto no prefab do projétil (faíscas + flash curtos).
        private static void WireImpact()
        {
            const string projPath = "Assets/_Project/Prefabs/Projectile_Player.prefab";
            const string impactPath = "Assets/_Project/Prefabs/ImpactEffect.prefab";
            var impact = AssetDatabase.LoadAssetAtPath<GameObject>(impactPath);
            if (impact == null) return;

            var contents = PrefabUtility.LoadPrefabContents(projPath);
            var proj = contents.GetComponent<Projectile>();
            if (proj != null)
            {
                var so = new SerializedObject(proj);
                var p = so.FindProperty("impactPrefab");
                if (p != null) { p.objectReferenceValue = impact; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
            PrefabUtility.SaveAsPrefabAsset(contents, projPath);
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }
}
