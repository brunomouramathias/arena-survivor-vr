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
    // Integra o personagem SWAT (Quaternius / Poly Pizza) no Player:
    // - Malha + materiais URP Lit a partir das cores do próprio FBX
    // - Clipes nativos (Walk, Run, Run_Back, Run_Left, Run_Right, Idle_Gun, Idle_Gun_Pointing, Idle_Gun_Shoot, Death, Roll)
    // - Blend Tree 2D Freeform Directional (MoveX, MoveZ) — anda em todas as direções
    // - Camada de braços (UpperBody) com máscara por caminho de osso: levanta a mão para mirar/atirar enquanto anda
    // - Arma na mão direita (WeaponSocket) + Muzzle na ponta do cano
    public static class SwatPlayerInstaller
    {
        public const string SWAT_FBX  = "Assets/_Project/ThirdParty/PolyPizza_Swat/Swat.fbx";
        public const string MAT_DIR   = "Assets/_Project/Materials/Swat";
        public const string CTRL_PATH = "Assets/_Project/Animations/Player/AnimatorController_Player.controller";
        public const string MASK_PATH = "Assets/_Project/Animations/Player/Mask_Swat_UpperBody.mask";
        public const string ARENA     = "Assets/_Project/Scenes/Arena.unity";
        public const string LOG_PATH  = "docs/swat_install_log.txt";

        [MenuItem("Tools/Arena/Install SWAT Player", priority = 22)]
        public static void Run() { RunInternal(false); }

        public static void RunBatch() { RunInternal(true); }

        private static void RunInternal(bool isBatch)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== [SWAT INSTALL] " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");

            try
            {
                AssetDatabase.Refresh();
                Directory.CreateDirectory(MAT_DIR);

                // 1) Importar como Generic (zero distorção de retarget: os clipes são nativos deste esqueleto)
                ConfigureSwatImporter(sb);

                // 2) Carregar todos os clipes embutidos
                var clips = LoadSwatClips(sb);

                // 3) Montar Animator Controller (Blend Tree 2D + UpperBody de mira/tiro)
                var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SWAT_FBX);
                if (modelAsset == null)
                {
                    sb.AppendLine("ERRO: GameObject do Swat.fbx não carregou.");
                    File.WriteAllText(LOG_PATH, sb.ToString(), Encoding.UTF8);
                    if (isBatch) EditorApplication.Exit(2);
                    return;
                }

                var ctrl = BuildController(modelAsset, clips, sb);

                // 4) Converter materiais embutidos para URP Lit
                var matMap = BuildUrpMaterials(modelAsset, sb);

                // 5) Substituir o visual do Player na cena Arena preservando as armas
                InstallIntoArena(modelAsset, ctrl, matMap, clips, sb);

                AssetDatabase.SaveAssets();
                sb.AppendLine("OK: SWAT instalado e cena Arena salva.");
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
        private static void ConfigureSwatImporter(StringBuilder sb)
        {
            var mi = AssetImporter.GetAtPath(SWAT_FBX) as ModelImporter;
            if (mi == null) throw new Exception("ModelImporter não encontrado em " + SWAT_FBX);

            mi.animationType = ModelImporterAnimationType.Generic;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.optimizeGameObjects = false;
            mi.importAnimation = true;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;

            // Primeiro reimport para popular defaultClipAnimations
            mi.SaveAndReimport();
            mi = AssetImporter.GetAtPath(SWAT_FBX) as ModelImporter;

            var defs = mi.defaultClipAnimations;
            sb.AppendLine($"Takes encontrados no FBX: {defs.Length}");
            foreach (var c in defs)
            {
                string clean = CleanClipName(c.name);
                c.name = clean;
                bool loop = IsLoopingClip(clean);
                c.loopTime = loop;
                c.loopPose = loop;
                c.lockRootRotation = true;
                c.lockRootHeightY = true;
                c.lockRootPositionXZ = true;
                c.keepOriginalOrientation = true;
                c.keepOriginalPositionY = true;
                c.keepOriginalPositionXZ = true;
                sb.AppendLine($"  - take '{clean}' frames [{c.firstFrame:0}..{c.lastFrame:0}] loop={loop}");
            }
            mi.clipAnimations = defs;
            mi.SaveAndReimport();
        }

        private static string CleanClipName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            int idx = raw.LastIndexOf('|');
            string s = idx >= 0 ? raw.Substring(idx + 1) : raw;
            if (s.EndsWith(".tak", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(0, s.Length - 4);
            return s.Trim();
        }

        private static bool IsLoopingClip(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.ToLowerInvariant();
            if (n.Contains("death") || n.Contains("roll") || n.Contains("hit") ||
                n.Contains("punch") || n.Contains("kick") || n.Contains("slash") || n.Contains("interact"))
                return false;
            return true; // Idle*, Walk, Run*, Gun_Shoot, Idle_Gun*
        }

        private static Dictionary<string, AnimationClip> LoadSwatClips(StringBuilder sb)
        {
            var dict = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(SWAT_FBX))
            {
                if (a is AnimationClip c && !c.name.StartsWith("__preview__"))
                {
                    string key = CleanClipName(c.name);
                    dict[key] = c;
                }
            }
            sb.AppendLine($"Clipes carregados ({dict.Count}): {string.Join(", ", dict.Keys)}");
            return dict;
        }

        private static AnimationClip Pick(Dictionary<string, AnimationClip> d, params string[] names)
        {
            foreach (var n in names)
                if (d.TryGetValue(n, out var c) && c != null) return c;
            return d.Values.FirstOrDefault();
        }

        // ------------------------------------------------------------------
        private static AnimatorController BuildController(
            GameObject modelAsset,
            Dictionary<string, AnimationClip> clips,
            StringBuilder sb)
        {
            var idleGun   = Pick(clips, "Idle_Gun", "Idle_Neutral", "Idle");
            var walk      = Pick(clips, "Walk", "Run");
            var runF      = Pick(clips, "Run", "Walk");
            var runB      = Pick(clips, "Run_Back", "Walk");
            var runL      = Pick(clips, "Run_Left", "Walk");
            var runR      = Pick(clips, "Run_Right", "Walk");
            var aimPoint  = Pick(clips, "Idle_Gun_Pointing", "Idle_Gun_Shoot", "Gun_Shoot", "Idle_Gun");
            var shootClip = Pick(clips, "Idle_Gun_Shoot", "Gun_Shoot", "Idle_Gun_Pointing");
            var death     = Pick(clips, "Death");
            var roll      = Pick(clips, "Roll");

            sb.AppendLine($"Mapeamento de clipes: Idle={idleGun?.name} | Walk={walk?.name} | Run={runF?.name} | Back={runB?.name} | Left={runL?.name} | Right={runR?.name} | Aim={aimPoint?.name} | Shoot={shootClip?.name} | Death={death?.name} | Roll={roll?.name}");

            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CTRL_PATH);
            if (ctrl == null)
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(CTRL_PATH);

            while (ctrl.layers.Length > 1)
                ctrl.RemoveLayer(ctrl.layers.Length - 1);

            var root = ctrl.layers[0].stateMachine;
            foreach (var st in root.states) root.RemoveState(st.state);
            foreach (var ssm in root.stateMachines) root.RemoveStateMachine(ssm.stateMachine);
            foreach (var tr in root.anyStateTransitions) root.RemoveAnyStateTransition(tr);
            foreach (var tr in root.entryTransitions) root.RemoveEntryTransition(tr);

            ctrl.parameters = new AnimatorControllerParameter[0];
            AddFloat(ctrl, "Speed");
            AddFloat(ctrl, "LegSpeed");
            AddFloat(ctrl, "MoveX");
            AddFloat(ctrl, "MoveZ");
            AddBool(ctrl, "IsAiming");
            AddTrigger(ctrl, "Shoot");
            AddTrigger(ctrl, "Dodge");
            AddTrigger(ctrl, "Die");

            // Base Layer: Blend Tree 2D Freeform Directional (anda em todas as direções!)
            var tree = new BlendTree
            {
                name = "Locomotion2D",
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = "MoveX",
                blendParameterY = "MoveZ",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(tree, ctrl);

            tree.AddChild(idleGun, new Vector2(0f, 0f));
            tree.AddChild(walk,    new Vector2(0f, 0.45f));
            tree.AddChild(runF,    new Vector2(0f, 1.0f));
            tree.AddChild(runB,    new Vector2(0f, -1.0f));
            tree.AddChild(runL,    new Vector2(-1.0f, 0f));
            tree.AddChild(runR,    new Vector2(1.0f, 0f));

            var loco = root.AddState("Locomotion", new Vector3(300f, 60f, 0f));
            loco.motion = tree;
            loco.speed = 1f;
            loco.speedParameterActive = true;
            loco.speedParameter = "LegSpeed";
            loco.writeDefaultValues = true;
            root.defaultState = loco;

            if (roll != null)
            {
                var dodge = root.AddState("Dodge", new Vector3(300f, 180f, 0f));
                dodge.motion = roll;
                dodge.speed = 1.3f;
                var toDodge = root.AddAnyStateTransition(dodge);
                toDodge.hasExitTime = false;
                toDodge.duration = 0.06f;
                toDodge.AddCondition(AnimatorConditionMode.If, 0f, "Dodge");

                var back = dodge.AddTransition(loco);
                back.hasExitTime = true;
                back.exitTime = 0.85f;
                back.duration = 0.1f;
            }

            var die = root.AddState("Die", new Vector3(560f, 180f, 0f));
            die.motion = death;
            die.speed = 1f;
            var dieTr = root.AddAnyStateTransition(die);
            dieTr.hasExitTime = false;
            dieTr.duration = 0.08f;
            dieTr.AddCondition(AnimatorConditionMode.If, 0f, "Die");

            // UpperBody Layer: máscara por caminho de transform (tronco + cabeça + braços)
            var mask = BuildUpperBodyTransformMask(modelAsset, sb);

            var upperLayer = new AnimatorControllerLayer
            {
                name = "UpperBody",
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Override,
                avatarMask = mask,
                stateMachine = new AnimatorStateMachine()
            };
            ctrl.AddLayer(upperLayer);
            AssetDatabase.AddObjectToAsset(upperLayer.stateMachine, ctrl);

            var uSM = ctrl.layers[1].stateMachine;
            var aimState = uSM.AddState("AimPointing", new Vector3(280f, 60f, 0f));
            aimState.motion = aimPoint;
            aimState.speed = 1f;
            aimState.writeDefaultValues = true;
            uSM.defaultState = aimState;

            var fireState = uSM.AddState("Fire", new Vector3(540f, 60f, 0f));
            fireState.motion = shootClip;
            fireState.speed = 1.4f;
            fireState.writeDefaultValues = true;

            var toFire = aimState.AddTransition(fireState);
            toFire.hasExitTime = false;
            toFire.duration = 0.04f;
            toFire.AddCondition(AnimatorConditionMode.If, 0f, "Shoot");

            var toAim = fireState.AddTransition(aimState);
            toAim.hasExitTime = true;
            toAim.exitTime = 0.75f;
            toAim.duration = 0.08f;

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            return ctrl;
        }

        private static AvatarMask BuildUpperBodyTransformMask(GameObject modelAsset, StringBuilder sb)
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MASK_PATH);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, MASK_PATH);
            }

            // Importa a hierarquia completa do modelo
            mask.AddTransformPath(modelAsset.transform, true);

            // Ativa apenas tronco superior / ombros / braços / mãos / cabeça; desativa quadril e pernas
            int activeCount = 0;
            for (int i = 0; i < mask.transformCount; i++)
            {
                string path = mask.GetTransformPath(i);
                bool upper = IsUpperBodyPath(path);
                mask.SetTransformActive(i, upper);
                if (upper) activeCount++;
            }
            sb.AppendLine($"AvatarMask de braços: {activeCount}/{mask.transformCount} transforms ativos.");
            EditorUtility.SetDirty(mask);
            return mask;
        }

        private static bool IsUpperBodyPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            // Qualquer caminho que passe por perna/pé/quadril fica fora da máscara superior
            string p = path.ToLowerInvariant();
            if (p.Contains("upperleg") || p.Contains("lowerleg") || p.Contains("foot") || p.Contains("toe"))
                return false;
            // Ativa do Spine/Chest/Neck/Head/Shoulder/Arm/Hand para baixo
            if (p.Contains("spine") || p.Contains("chest") || p.Contains("neck") || p.Contains("head") ||
                p.Contains("shoulder") || p.Contains("upperarm") || p.Contains("lowerarm") || p.Contains("hand") ||
                p.Contains("finger") || p.Contains("thumb") || p.Contains("index") || p.Contains("wrist"))
                return true;
            return false;
        }

        private static void AddFloat(AnimatorController c, string name)
            => c.AddParameter(new AnimatorControllerParameter { name = name, type = AnimatorControllerParameterType.Float, defaultFloat = 0f });
        private static void AddBool(AnimatorController c, string name)
            => c.AddParameter(new AnimatorControllerParameter { name = name, type = AnimatorControllerParameterType.Bool, defaultBool = true });
        private static void AddTrigger(AnimatorController c, string name)
            => c.AddParameter(new AnimatorControllerParameter { name = name, type = AnimatorControllerParameterType.Trigger });

        // ------------------------------------------------------------------
        private static Dictionary<string, Material> BuildUrpMaterials(GameObject modelAsset, StringBuilder sb)
        {
            var map = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
            var shader = Shader.Find("Universal Render Pipeline/Lit");

            foreach (var r in modelAsset.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var src in r.sharedMaterials)
                {
                    if (src == null || map.ContainsKey(src.name)) continue;

                    Color col = Color.gray;
                    if (src.HasProperty("_BaseColor")) col = src.GetColor("_BaseColor");
                    else if (src.HasProperty("_Color")) col = src.GetColor("_Color");

                    string matPath = $"{MAT_DIR}/Mat_Swat_{Sanitize(src.name)}.mat";
                    var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (m == null)
                    {
                        m = new Material(shader) { name = "Mat_Swat_" + src.name };
                        AssetDatabase.CreateAsset(m, matPath);
                    }
                    m.shader = shader;
                    m.SetColor("_BaseColor", col);
                    m.SetFloat("_Smoothness", 0.25f);
                    m.SetFloat("_Metallic", 0.08f);
                    EditorUtility.SetDirty(m);

                    map[src.name] = m;
                    sb.AppendLine($"  Material '{src.name}' -> URP Lit cor={col}");
                }
            }
            return map;
        }

        private static string Sanitize(string s)
        {
            foreach (var ch in System.IO.Path.GetInvalidFileNameChars()) s = s.Replace(ch, '_');
            return s.Replace(' ', '_');
        }

        // ------------------------------------------------------------------
        private static void InstallIntoArena(
            GameObject modelAsset,
            AnimatorController ctrl,
            Dictionary<string, Material> matMap,
            Dictionary<string, AnimationClip> clips,
            StringBuilder sb)
        {
            var scene = EditorSceneManager.OpenScene(ARENA, OpenSceneMode.Single);
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) throw new Exception("Player não encontrado na cena Arena.");

            // Resgata as 3 armas antes de remover o modelo antigo
            var savedWeapons = new List<Transform>();
            foreach (var wName in new[] { "Weapon_Pistol", "Weapon_Shotgun", "Weapon_Energy" })
            {
                var w = FindDeep(player.transform, wName);
                if (w != null)
                {
                    w.SetParent(player.transform, false);
                    savedWeapons.Add(w);
                }
            }
            sb.AppendLine($"Armas preservadas: {savedWeapons.Count}");

            // Remove modelos antigos (Model_Player / Beta_*)
            for (int i = player.transform.childCount - 1; i >= 0; i--)
            {
                var ch = player.transform.GetChild(i);
                if (savedWeapons.Contains(ch)) continue;
                UnityEngine.Object.DestroyImmediate(ch.gameObject);
            }

            // Instancia o SWAT como filho de Player e desempacota na raiz do Player
            // para que os caminhos de osso do Animator no Player batam 1:1 com os clipes do FBX
            var swatInst = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, scene);
            PrefabUtility.UnpackPrefabInstance(swatInst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            swatInst.transform.SetParent(player.transform, false);
            swatInst.transform.localPosition = Vector3.zero;
            swatInst.transform.localRotation = Quaternion.identity;
            swatInst.transform.localScale = Vector3.one;

            // Move filhos diretos de swatInst para debaixo de Player (CharacterArmature + malhas ficam direto sob Player)
            var directChildren = new List<Transform>();
            for (int i = 0; i < swatInst.transform.childCount; i++)
                directChildren.Add(swatInst.transform.GetChild(i));
            foreach (var c in directChildren)
                c.SetParent(player.transform, false);
            UnityEngine.Object.DestroyImmediate(swatInst);

            // Aplica materiais URP Lit
            foreach (var r in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && matMap.TryGetValue(mats[i].name, out var urp))
                        mats[i] = urp;
                }
                r.sharedMaterials = mats;
                r.updateWhenOffscreen = true;
            }

            // Mede altura real e ajusta escala uniforme para ~1.80 m se necessário
            var aimClip = Pick(clips, "Idle_Gun_Pointing", "Idle_Gun");
            if (aimClip != null) aimClip.SampleAnimation(player, 0f);
            float h = MeasureHeight(player);
            sb.AppendLine($"Altura bruta do SWAT: {h:F2}m");
            if (h > 0.05f && (h < 1.35f || h > 2.35f))
            {
                float scale = 1.80f / h;
                var mi = AssetImporter.GetAtPath(SWAT_FBX) as ModelImporter;
                if (mi != null)
                {
                    mi.globalScale = mi.globalScale * scale;
                    mi.SaveAndReimport();
                    sb.AppendLine($"globalScale ajustado para altura ~1.80m (fator {scale:F2}).");
                }
            }

            // Configura o Animator do Player
            var avatar = AssetDatabase.LoadAllAssetsAtPath(SWAT_FBX).OfType<Avatar>().FirstOrDefault();
            var anim = player.GetComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.avatar = avatar;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Anexa WeaponSocket ao osso da mão direita (Hand.R / Wrist.R / LowerArm.R)
            Transform hand = FindDeepContains(player.transform, "Hand.R", "Wrist.R", "RightHand", "Palm.R", "LowerArm.R");
            if (hand == null) throw new Exception("Osso da mão direita não encontrado no SWAT.");
            sb.AppendLine($"Osso da mão direita: {Path(hand)}");

            var socket = hand.Find("WeaponSocket");
            if (socket == null)
            {
                socket = new GameObject("WeaponSocket").transform;
                socket.SetParent(hand, false);
            }
            // O esqueleto do SWAT vem em centímetros (escala 100): compensa na arma
            socket.localPosition = Vector3.zero;   // o osso já está na palma; escala do esqueleto é 100x
            socket.localScale = Vector3.one * 0.01f;

            // Amostra pose de apontar arma para alinhar o socket com a frente do personagem
            if (aimClip != null) aimClip.SampleAnimation(player, 0.1f);
            socket.rotation = Quaternion.LookRotation(player.transform.forward, player.transform.up);

            foreach (var w in savedWeapons)
            {
                w.SetParent(socket, false);
                w.localPosition = Vector3.zero;
                w.localRotation = Quaternion.identity;
                w.localScale = Vector3.one;
                foreach (var t in w.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Visual") t.localRotation = Quaternion.identity;
                    if (t.name == "ShootPoint") t.name = "Muzzle";
                }
            }

            // Liga weaponHolder no PlayerShooter
            var shooter = player.GetComponent<PlayerShooter>();
            if (socket.childCount == 0)
            {
                SetupWeaponsFallback(socket, shooter, sb);
            }

            if (shooter != null)
            {
                var so = new SerializedObject(shooter);
                var wh = so.FindProperty("weaponHolder");
                if (wh != null) wh.objectReferenceValue = socket;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            CaptureEvidenceShots(player, sb);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void SetupWeaponsFallback(Transform socket, PlayerShooter shooter, StringBuilder sb)
        {
            sb.AppendLine("Instanciando armas na mão do SWAT...");
            string blasterPath = "Assets/_Project/ThirdParty/_INBOX/Kenney_BlasterKit/blaster-g.fbx";
            if (!File.Exists(blasterPath)) blasterPath = "Assets/_Project/ThirdParty/_INBOX/Kenney_BlasterKit/blaster-e.fbx";
            var blasterAsset = AssetDatabase.LoadAssetAtPath<GameObject>(blasterPath);
            var blasterMat   = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Mat_BlasterColormap.mat");

            var pistolSO  = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_Project/ScriptableObjects/Weapons/WeaponData_Pistol.asset");
            var shotgunSO = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_Project/ScriptableObjects/Weapons/WeaponData_Shotgun.asset");
            var energySO  = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_Project/ScriptableObjects/Weapons/WeaponData_Energy.asset");

            Weapon wPistol  = CreateWeaponChild(socket, "Weapon_Pistol",  pistolSO,  blasterAsset, blasterMat, true);
            Weapon wShotgun = CreateWeaponChild(socket, "Weapon_Shotgun", shotgunSO, blasterAsset, blasterMat, false);
            Weapon wEnergy  = CreateWeaponChild(socket, "Weapon_Energy",  energySO,  blasterAsset, blasterMat, false);

            if (shooter != null)
            {
                var so = new SerializedObject(shooter);
                var slots = so.FindProperty("weaponSlots");
                slots.arraySize = 3;
                slots.GetArrayElementAtIndex(0).objectReferenceValue = wPistol;
                slots.GetArrayElementAtIndex(1).objectReferenceValue = wShotgun;
                slots.GetArrayElementAtIndex(2).objectReferenceValue = wEnergy;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            sb.AppendLine("Armas vinculadas com sucesso no SWAT!");
        }

        private static Weapon CreateWeaponChild(Transform socket, string name, WeaponData data, GameObject model, Material mat, bool active)
        {
            var go = new GameObject(name);
            go.transform.SetParent(socket, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var sp = new GameObject("Muzzle");
            sp.transform.SetParent(go.transform, false);
            sp.transform.localPosition = new Vector3(0f, 0.05f, 0.45f);

            if (model != null)
            {
                var v = UnityEngine.Object.Instantiate(model, go.transform);
                v.name = "Visual";
                v.transform.localPosition = Vector3.zero;
                v.transform.localRotation = Quaternion.identity;
                v.transform.localScale = Vector3.one * 0.55f;
                if (mat != null)
                {
                    foreach (var r in v.GetComponentsInChildren<Renderer>())
                        r.sharedMaterial = mat;
                }
            }

            var w = go.AddComponent<Weapon>();
            var so = new SerializedObject(w);
            so.FindProperty("data").objectReferenceValue = data;
            so.FindProperty("muzzlePoint").objectReferenceValue = sp.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            go.SetActive(active);
            return w;
        }

        private static void CaptureEvidenceShots(GameObject player, StringBuilder sb)
        {
            Directory.CreateDirectory("docs/screens/swat");
            var camGO = new GameObject("_CaptureCam");
            var cam = camGO.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.15f);

            // 1. Frente corpo inteiro (mão apontando com arma)
            camGO.transform.position = player.transform.position + new Vector3(0f, 1.4f, 2.8f);
            camGO.transform.LookAt(player.transform.position + Vector3.up * 1.0f);
            CapturePNG(cam, "docs/screens/swat/swat_arena_idle.png");

            // 2. Perfil close da arma na mão
            camGO.transform.position = player.transform.position + new Vector3(1.8f, 1.3f, 0.8f);
            camGO.transform.LookAt(player.transform.position + Vector3.up * 1.1f);
            CapturePNG(cam, "docs/screens/swat/swat_arena_aim.png");

            // 3. Câmera do jogo (topo inclinada)
            camGO.transform.position = player.transform.position + new Vector3(0f, 5.5f, -4.2f);
            camGO.transform.rotation = Quaternion.Euler(53f, 0f, 0f);
            CapturePNG(cam, "docs/screens/swat/swat_game_view.png");

            UnityEngine.Object.DestroyImmediate(camGO);
            sb.AppendLine("Capturas de prova salvas em docs/screens/swat/.");
        }

        private static void CapturePNG(Camera cam, string path)
        {
            int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = prev;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(rt);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static float MeasureHeight(GameObject root)
        {
            Bounds b = new Bounds(root.transform.position, Vector3.zero);
            bool first = true;
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            return b.size.y;
        }

        private static Transform FindDeep(Transform p, string exact)
        {
            if (p.name == exact) return p;
            for (int i = 0; i < p.childCount; i++)
            {
                var f = FindDeep(p.GetChild(i), exact);
                if (f != null) return f;
            }
            return null;
        }

        private static Transform FindDeepContains(Transform p, params string[] tokens)
        {
            foreach (var tk in tokens)
            {
                var found = FindByToken(p, tk);
                if (found != null) return found;
            }
            return null;
        }

        private static Transform FindByToken(Transform p, string token)
        {
            if (p.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0 &&
                !p.name.EndsWith("_end", StringComparison.OrdinalIgnoreCase))
                return p;
            for (int i = 0; i < p.childCount; i++)
            {
                var f = FindByToken(p.GetChild(i), token);
                if (f != null) return f;
            }
            return null;
        }

        private static string Path(Transform t)
        {
            var list = new List<string>();
            while (t != null) { list.Add(t.name); t = t.parent; }
            list.Reverse();
            return string.Join("/", list);
        }
    }
}
