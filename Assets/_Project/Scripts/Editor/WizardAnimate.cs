#if UNITY_EDITOR
using System;
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
using Object = UnityEngine.Object;

namespace ArenaSurvivor.Editor
{
    // Anima os 2 bruxos (Fire/Air) do Wizard_Free: as animaÃ§Ãµes Mixamo sÃ£o
    // queimadas no esqueleto do bruxo via SampleMode (amostra + bake em
    // curvas locais), que aceita qualquer esqueleto. Depois refaz os prefabs
    // com a textura do pack e liga o Enemy_Sorcerer no spawner.
    public static class WizardAnimate
    {
        const string ANIM = "Assets/_Project/Animations/Wizards";
        const string INBOX = "Assets/_Project/ThirdParty/_INBOX/Mixamo";
        const string PREFABS = "Assets/_Project/Prefabs";
        const string MAT = "Assets/_Project/Materials";
        static readonly StringBuilder sb = new StringBuilder();

        [MenuItem("Tools/Arena/Wizards: Animate + Rebuild")]
        public static void Run()
        {
            sb.AppendLine("=== WIZARDS: ANIMATE + REBUILD ===");
            Directory.CreateDirectory(ANIM);

            // os FBX do zumbi são Humanoid; SampleAnimation neles exige que o
            // importador não reative o Humanoid a cada instância — reimporta
            // garantindo tipo Human e clipes no "modo bone local"
            foreach (var zn in new[] { "Zombie Walk", "Zombie Run", "Zombie Attack",
                "Standing 2H Magic Attack 05", "Standing 2H Cast Spell 01",
                "Standing 1H Cast Spell 01", "Standing 1H Magic Attack 02" })
            {
                string zp = $"{INBOX}/{zn}.fbx";
                var mi = AssetImporter.GetAtPath(zp) as ModelImporter;
                if (mi == null) continue;
                bool changed = false;
                if (mi.animationType != ModelImporterAnimationType.Human) { mi.animationType = ModelImporterAnimationType.Human; changed = true; }
                if (mi.motionNodeName != "") { mi.motionNodeName = ""; changed = true; }
                // força clipes como curvas de transform (não músculos humanoides)
                var cia = mi.clipAnimations;
                if (cia != null && cia.Length > 0) { mi.clipAnimations = cia; changed = true; }
                if (changed) mi.SaveAndReimport();
            }

            var fire = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/ThirdParty/Wizard_Free/Characters/Fire_Wizard.fbx");
            var air = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/ThirdParty/Wizard_Free/Characters/Air_Wizard.fbx");

            var fireClips = BakeMixamoOnto(fire, "Fire_Wizard", "Model_Mage", new[] { "Zombie Walk", "Zombie Run", "Zombie Attack" });
            var airClips = BakeMixamoOnto(air, "Air_Wizard", "Model_Sorcerer", new[] { "Zombie Walk", "Zombie Run", "Zombie Attack" });

            BuildController("Fire_Wizard", fireClips);
            BuildController("Air_Wizard", airClips);

            var projPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFABS}/Projectile_Enemy.prefab");
            var warnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFABS}/SpawnWarning.prefab");
            var minionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFABS}/Enemy_Chaser.prefab");

            RebuildBoss(fire, projPrefab, warnPrefab, minionPrefab);
            RebuildSorcerer(air, projPrefab);
            WireScene();
            AssetDatabase.SaveAssets();

            File.WriteAllText("docs/screens/diag/wizard_animate.txt", sb.ToString(), Encoding.UTF8);
            Debug.Log(sb.ToString());
            EditorApplication.Exit(0);
        }

        // Queima o clipe Mixamo no esqueleto do bruxo:
        // instancia os dois no mesmo espaÃ§o, copia a pose quadro a quadro e
        // grava curvas LOCAIS (position/rotation) por osso do bruxo.
        static Dictionary<string, AnimationClip> BakeMixamoOnto(GameObject wizardModel, string wizardName, string pathPrefix, string[] mixamoFbxNames)
        {
            var result = new Dictionary<string, AnimationClip>();

            // ordem de preferencia por saida; se a amostragem sair constante, tenta o proximo
            var candidates = new Dictionary<string, string[]>
            {
                ["Cast"]  = new[] { "Zombie Attack", "Zombie Run", "Standing 2H Cast Spell 01", "Standing 1H Cast Spell 01", "Zombie Walk", "Standing 2H Magic Attack 05" },
                ["Cast2"] = new[] { "Zombie Run", "Standing 1H Magic Attack 02", "Standing 2H Magic Attack 05", "Zombie Attack", "Zombie Walk" },
                ["Walk"]  = new[] { "Zombie Walk", "Zombie Run", "Zombie Attack", "Standing 2H Magic Attack 05" },
            };

            string outDir = $"{ANIM}/{wizardName}_Anim";
            Directory.CreateDirectory(outDir);

            // mapa do esqueleto do bruxo (nome Blender -> transform)
            var wizardBones = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            CollectBones(wizardModel.transform, wizardBones);

            foreach (var kv in srcNamesKeyOrder(candidates))
            {
                AnimationClip chosen = null;
                string chosenSrc = null;
                foreach (var cand in candidates[kv])
                {
                    string srcPath = $"{INBOX}/{cand}.fbx";
                    if (!File.Exists(srcPath)) { sb.AppendLine($"[WARN] nao existe {srcPath}"); continue; }
                    var srcClips = AssetDatabase.LoadAllAssetsAtPath(srcPath).OfType<AnimationClip>()
                        .Where(c => !c.name.StartsWith("__")).ToList();
                    if (srcClips.Count == 0) { sb.AppendLine($"[WARN] sem clipes em {srcPath}"); continue; }
                    var src = srcClips[0];

                // instancia o zumbi (mesma cena temporÃ¡ria) para posar
                var zombieFbx = AssetDatabase.LoadAssetAtPath<GameObject>(srcPath);
                var zGo = (GameObject)Object.Instantiate(zombieFbx);
                var wGo = (GameObject)Object.Instantiate(wizardModel);
                var zBones = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
                CollectBones(zGo.transform, zBones);

                // correspondÃªncia de ossos Mixamo -> Blender
                var pairs = new List<(Transform z, Transform w)>();
                var map = MixamoToBlender();
                foreach (var (mixamoName, blenderName) in map)
                {
                    if (zBones.TryGetValue(mixamoName, out var zb) && wizardBones.TryGetValue(blenderName, out var wb))
                        pairs.Add((zb, wb));
                }
                sb.AppendLine($"[RIG] {wizardName} <- {kv}/{cand}: {pairs.Count} pares de ossos");

                    float fps = 24f;
                    int frames = Mathf.CeilToInt(src.length * fps) + 1;
                    var dst = new AnimationClip { frameRate = fps };
                    dst.name = $"{wizardName}_{kv}";

                var curves = new Dictionary<string, List<Keyframe>>[2]; // rot, pos
                // paths de destino (relativos Ã  raiz do bruxo)
                var pathByBoneName = new Dictionary<string, string>();
                CollectPaths(wGo.transform, "", pathByBoneName);

// REST LOCAL de ambos os rigs (antes de qualquer amostragem)
                var zRestWorld = new Dictionary<Transform, Vector3>();
                var zRest = new Dictionary<Transform, Quaternion>();
                var wRest = new Dictionary<Transform, Quaternion>();
                var wRestPos = new Dictionary<Transform, Vector3>();
                var rotX = new Dictionary<string, List<Keyframe>>();
                var rotY = new Dictionary<string, List<Keyframe>>();
                var rotZ = new Dictionary<string, List<Keyframe>>();
                var rotW = new Dictionary<string, List<Keyframe>>();
                var posX = new Dictionary<string, List<Keyframe>>();
                var posY = new Dictionary<string, List<Keyframe>>();
                var posZ = new Dictionary<string, List<Keyframe>>();
                var isHips = new Dictionary<string, bool>();
                foreach (var (zb, wb) in pairs)
                {
                    zRest[zb] = zb.localRotation;
                    zRestWorld[zb] = zb.position;
                    wRest[wb] = wb.localRotation;
                    wRestPos[wb] = wb.localPosition;
                    rotX[wb.name] = new List<Keyframe>();
                    rotY[wb.name] = new List<Keyframe>();
                    rotZ[wb.name] = new List<Keyframe>();
                    rotW[wb.name] = new List<Keyframe>();
                    posX[wb.name] = new List<Keyframe>();
                    posY[wb.name] = new List<Keyframe>();
                    posZ[wb.name] = new List<Keyframe>();
                    isHips[wb.name] = wb.name.ToLower().Contains("spine") && !wb.name.Contains(".");
                }

                    for (int f = 0; f < frames; f++)
                    {
                        float t = f / fps;
                        if (t > src.length) t = src.length;
                        src.SampleAnimation(zGo, t);

                    foreach (var (zb, wb) in pairs)
                    {
                        Quaternion deltaLocal = zb.localRotation * Quaternion.Inverse(zRest[zb]);
                        Quaternion localRot = deltaLocal * wRest[wb];
                        rotX[wb.name].Add(new Keyframe(t, localRot.x));
                        rotY[wb.name].Add(new Keyframe(t, localRot.y));
                        rotZ[wb.name].Add(new Keyframe(t, localRot.z));
                        rotW[wb.name].Add(new Keyframe(t, localRot.w));

                        if (isHips[wb.name])
                        {
                            Vector3 dp = zb.position - zRestWorld[zb];
                            Vector3 lp = wRestPos[wb] + new Vector3(dp.x, dp.y, -dp.z) * 0.9f;
                            posX[wb.name].Add(new Keyframe(t, lp.x));
                            posY[wb.name].Add(new Keyframe(t, lp.y));
                            posZ[wb.name].Add(new Keyframe(t, lp.z));
                        }
                    }
                    }

                    // verificação: a amostragem realmente variou a pose?
                    int varied = 0;
                    foreach (var (zb, wb) in pairs)
                    {
                        var ux = new HashSet<float>();
                        foreach (var k in rotX[wb.name]) ux.Add(k.value);
                        if (ux.Count > 2) varied++;
                    }
                    if (varied < 4)
                    {
                        sb.AppendLine($"[WARN] {kv} <- {cand}: pose constante ({varied} ossos variaram), tentando proximo clipe");
                        Object.DestroyImmediate(zGo);
                        Object.DestroyImmediate(wGo);
                        continue;
                    }
                    chosen = dst; chosenSrc = cand;
                    if (chosen != null)
                    {
                        foreach (var (zb, wb) in pairs)
                        {
                            if (!pathByBoneName.TryGetValue(wb.name, out var pp)) continue;
                            string stripped = pp.Contains("/") ? pp.Substring(pp.IndexOf('/') + 1) : pp;
                            string p = $"{pathPrefix}/{stripped}".Replace(':', '_');
                            dst.SetCurve(p, typeof(Transform), "m_LocalRotation.x", new AnimationCurve(rotX[wb.name].ToArray()));
                            dst.SetCurve(p, typeof(Transform), "m_LocalRotation.y", new AnimationCurve(rotY[wb.name].ToArray()));
                            dst.SetCurve(p, typeof(Transform), "m_LocalRotation.z", new AnimationCurve(rotZ[wb.name].ToArray()));
                            dst.SetCurve(p, typeof(Transform), "m_LocalRotation.w", new AnimationCurve(rotW[wb.name].ToArray()));
                            if (isHips[wb.name] && posX[wb.name].Count > 1)
                            {
                                dst.SetCurve(p, typeof(Transform), "m_LocalPosition.x", new AnimationCurve(posX[wb.name].ToArray()));
                                dst.SetCurve(p, typeof(Transform), "m_LocalPosition.y", new AnimationCurve(posY[wb.name].ToArray()));
                                dst.SetCurve(p, typeof(Transform), "m_LocalPosition.z", new AnimationCurve(posZ[wb.name].ToArray()));
                            }
                        }

                        var outPath = $"{outDir}/{dst.name}.anim";
                        AssetDatabase.DeleteAsset(outPath);
                        AssetDatabase.CreateAsset(dst, outPath);
                        result[kv] = dst;
                        sb.AppendLine($"[CLIP] {dst.name}: src={chosenSrc} dur={dst.length:0.00}s frames={frames} bones={pairs.Count} variados={varied}");
                    }

                    Object.DestroyImmediate(zGo);
                    Object.DestroyImmediate(wGo);
                    if (chosen != null) break;
                }
                if (chosen == null) sb.AppendLine($"[ERRO] {kv}: nenhum clipe de origem variou!");
            }
            return result;
        }

        static IEnumerable<string> srcNamesKeyOrder(Dictionary<string, string[]> candidates)
        {
            yield return "Cast"; yield return "Cast2"; yield return "Walk";
        }

        static Dictionary<string, string> MixamoToBlender()
        {
            return new Dictionary<string, string>
            {
                ["mixamorig:Hips"] = "spine",
                ["mixamorig:Spine"] = "spine.001",
                ["mixamorig:Spine1"] = "spine.002",
                ["mixamorig:Spine2"] = "spine.003",
                ["mixamorig:Neck"] = "spine.004",
                ["mixamorig:Head"] = "spine.005",
                ["mixamorig:LeftShoulder"] = "shoulder.L",
                ["mixamorig:RightShoulder"] = "shoulder.R",
                ["mixamorig:LeftArm"] = "upper_arm.L",
                ["mixamorig:RightArm"] = "upper_arm.R",
                ["mixamorig:LeftForeArm"] = "forearm.L",
                ["mixamorig:RightForeArm"] = "forearm.R",
                ["mixamorig:LeftHand"] = "hand.L",
                ["mixamorig:RightHand"] = "hand.R",
                ["mixamorig:LeftUpLeg"] = "thigh.L",
                ["mixamorig:RightUpLeg"] = "thigh.R",
                ["mixamorig:LeftLeg"] = "shin.L",
                ["mixamorig:RightLeg"] = "shin.R",
                ["mixamorig:LeftFoot"] = "foot.L",
                ["mixamorig:RightFoot"] = "foot.R",
                ["mixamorig:LeftToeBase"] = "toe.L",
                ["mixamorig:RightToeBase"] = "toe.R",
            };
        }


        static void CollectBones(Transform t, Dictionary<string, Transform> map)
        {
            string n = t.name.StartsWith("mixamorig:") ? t.name.Substring(10) : t.name;
            map[t.name] = t;
            map[n] = t;
            foreach (Transform c in t) CollectBones(c, map);
        }

        static void CollectPaths(Transform t, string path, Dictionary<string, string> map)
        {
            string p = string.IsNullOrEmpty(path) ? t.name : path + "/" + t.name;
            map[t.name] = p.Replace(':', '_');
            foreach (Transform c in t) CollectPaths(c, p, map);
        }

        static void BuildController(string wizardName, Dictionary<string, AnimationClip> clips)
        {
            string path = $"{ANIM}/AnimatorController_{wizardName}.controller";
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

            clips.TryGetValue("Cast", out var castClip);
            clips.TryGetValue("Cast2", out var cast2Clip);
            clips.TryGetValue("Walk", out var walkClip);
            var idleClip = walkClip ?? castClip ?? cast2Clip; // idle = clipe que varía (evita pose estática)

            var sIdle = sm.AddState("Idle");
            var sWalk = sm.AddState("Walk");
            var sAttack = sm.AddState("Attack");
            var sCast = sm.AddState("CastSpell");
            sIdle.motion = idleClip;
            sIdle.speed = 0.4f; // idle lento = respirar/andar no lugar
            sWalk.motion = walkClip ?? idleClip;
            sAttack.motion = cast2Clip ?? castClip;
            sCast.motion = castClip;
            if (walkClip != null) sWalk.speed = 1.15f;
            sm.defaultState = sIdle;

            var t1 = sIdle.AddTransition(sWalk);
            t1.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            t1.duration = 0.2f;
            var t2 = sWalk.AddTransition(sIdle);
            t2.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");
            t2.duration = 0.2f;
            var t3 = sm.AddAnyStateTransition(sAttack);
            t3.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            t3.duration = 0.1f; t3.canTransitionToSelf = false;
            var t4 = sm.AddAnyStateTransition(sCast);
            t4.AddCondition(AnimatorConditionMode.If, 0, "CastSpell");
            t4.duration = 0.1f; t4.canTransitionToSelf = false;
            var back = sAttack.AddTransition(sIdle);
            back.hasExitTime = true; back.exitTime = 0.85f; back.duration = 0.2f;

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            sb.AppendLine($"[CTRL] {wizardName}: idle={idleClip?.name} walk={walkClip?.name} cast={castClip?.name}");
        }

        static void RebuildBoss(GameObject model, GameObject projPrefab, GameObject warnPrefab, GameObject minionPrefab)
        {
            string path = $"{PREFABS}/Enemy_Boss.prefab";
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MAT}/Mat_Wizard_Fire.mat");
            using (var scope = new PrefabUtility.EditPrefabContentsScope(path))
            {
                var root = scope.prefabContentsRoot;
                var old = root.transform.Find("Model_Mage") ?? root.transform.Find("Model_Zombie");
                if (old != null) Object.DestroyImmediate(old.gameObject);

                var visual = (GameObject)Object.Instantiate(model, root.transform);
                visual.name = "Model_Mage";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.Euler(0, 180f, 0);
                visual.transform.localScale = Vector3.one * 1.35f;
                foreach (var r in visual.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;

                float h = 0f;
                foreach (var r in visual.GetComponentsInChildren<Renderer>())
                    h = Mathf.Max(h, r.bounds.size.y / 1.35f);
                h0 = h;
                AttachEyes(visual.transform, h * 0.82f, 0.07f, $"{MAT}/Mat_Boss_Eyes.mat");

                var col = root.GetComponent<CapsuleCollider>();
                if (col != null) { col.height = h * 1.35f + 0.3f; col.center = new Vector3(0, col.height / 2f, 0); col.radius = 0.6f; }
                var agent = root.GetComponent<NavMeshAgent>();
                if (agent != null) { agent.height = col.height; agent.radius = 0.6f; agent.acceleration = 14f; agent.angularSpeed = 480f; }

                // Animação é procedural (WizardAnim): nenhum Animator é adicionado.
                // O retarget Mixamo em T-pose deformava a pose, por isso foi descartado.
                var legacyAnim = root.GetComponent<Animator>();
                if (legacyAnim != null) Object.DestroyImmediate(legacyAnim, true);
                var waBoss = root.GetComponent<WizardAnim>();
                if (waBoss == null) waBoss = root.AddComponent<WizardAnim>();
                waBoss.modelRoot = visual.transform;

                var boss = root.GetComponent<BossController>();
                var so = new SerializedObject(boss);
                so.FindProperty("projectilePrefab").objectReferenceValue = projPrefab;
                so.FindProperty("aoeWarningPrefab").objectReferenceValue = warnPrefab;
                so.FindProperty("minionPrefab").objectReferenceValue = minionPrefab;
                var sp = FindDeep(root.transform, "ShootPoint");
                if (sp != null) so.FindProperty("shootPoint").objectReferenceValue = sp;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            sb.AppendLine($"[BOSS] rebuild com Fire_Wizard (altura visual {h0:0.00}m)");
        }
        static float h0;

        static void RebuildSorcerer(GameObject model, GameObject projPrefab)
        {
            string path = $"{PREFABS}/Enemy_Sorcerer.prefab";
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MAT}/Mat_Wizard_Air.mat");
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/_Project/ScriptableObjects/Enemies/EnemyData_Shooter.asset");

            using (var scope = new PrefabUtility.EditPrefabContentsScope(path))
            {
                var root = scope.prefabContentsRoot;
                var old = root.transform.Find("Model_Sorcerer");
                if (old != null) Object.DestroyImmediate(old.gameObject);

                var visual = (GameObject)Object.Instantiate(model, root.transform);
                visual.name = "Model_Sorcerer";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.Euler(0, 180f, 0);
                visual.transform.localScale = Vector3.one;
                foreach (var r in visual.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;

                float h = 0f;
                foreach (var r in visual.GetComponentsInChildren<Renderer>())
                    h = Mathf.Max(h, r.bounds.size.y);
                h0 = h;
                AttachEyes(visual.transform, h * 0.82f, 0.05f, $"{MAT}/Mat_Shooter_Eyes.mat");

                var col = root.GetComponent<CapsuleCollider>();
                if (col != null) { col.height = h + 0.25f; col.center = new Vector3(0, col.height / 2f, 0); col.radius = 0.4f; }
                var agent = root.GetComponent<NavMeshAgent>();
                if (agent != null) { agent.height = col.height; agent.acceleration = 14f; agent.angularSpeed = 480f; }

                var legacyAnim = root.GetComponent<Animator>();
                if (legacyAnim != null) Object.DestroyImmediate(legacyAnim, true);
                var waSorc = root.GetComponent<WizardAnim>();
                if (waSorc == null) waSorc = root.AddComponent<WizardAnim>();
                waSorc.modelRoot = visual.transform;

                var sp = root.transform.Find("ShootPoint");
                if (sp != null) sp.localPosition = new Vector3(0, h * 0.7f, 0.45f);
                var shooter = root.GetComponent<EnemyShooter>();
                var so = new SerializedObject(shooter);
                so.FindProperty("projectilePrefab").objectReferenceValue = projPrefab;
                if (sp != null) so.FindProperty("shootPoint").objectReferenceValue = sp;
                so.FindProperty("data").objectReferenceValue = data;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            sb.AppendLine($"[SORCERER] rebuild com Air_Wizard (altura visual {h0:0.00}m)");
        }

        static void AttachEyes(Transform parent, float y, float size, string matPath)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            foreach (var side in new[] { -1, 1 })
            {
                var e = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(e.GetComponent<Collider>());
                e.name = side < 0 ? "Eye_Glow_L" : "Eye_Glow_R";
                e.transform.SetParent(parent, false);
                e.transform.localPosition = new Vector3(side * 0.05f, y, 0.06f);
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

        static void WireScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Arena.unity", OpenSceneMode.Single);
            var spawner = Object.FindAnyObjectByType<ArenaSurvivor.Waves.WaveSpawner>();
            if (spawner != null)
            {
                var so = new SerializedObject(spawner);
                var sorc = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFABS}/Enemy_Sorcerer.prefab");
                var p = so.FindProperty("shooterPrefab");
                if (p != null && sorc != null) p.objectReferenceValue = sorc;
                so.ApplyModifiedPropertiesWithoutUndo();
                sb.AppendLine("[SCENE] shooterPrefab = Enemy_Sorcerer");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
#endif
