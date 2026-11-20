using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

namespace ArenaSurvivor.Editor
{
    public static class PlayerDiagnosticTool
    {
        private const string SCENE_ARENA = "Assets/_Project/Scenes/Arena.unity";
        private const string DIAGNOSTIC_PATH = "docs/diagnostico_player.txt";

        [MenuItem("Tools/Arena/Diagnose Player", priority = 20)]
        public static void DiagnosePlayer()
        {
            RunInternal(false);
        }

        public static void RunDiagnosticBatch()
        {
            RunInternal(true);
        }

        private static void RunInternal(bool isBatch)
        {
            var sb = new StringBuilder();
            sb.AppendLine("==================================================");
            sb.AppendLine("       DIAGNÓSTICO COMPLETO DO PERSONAGEM         ");
            sb.AppendLine("==================================================");
            sb.AppendLine($"Data/Hora: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

            var scene = EditorSceneManager.OpenScene(SCENE_ARENA, OpenSceneMode.Single);
            var playerGO = GameObject.FindGameObjectWithTag("Player");

            if (playerGO == null)
            {
                sb.AppendLine("[ERRO CRÍTICO] Objeto 'Player' não encontrado na cena!");
                File.WriteAllText(DIAGNOSTIC_PATH, sb.ToString(), Encoding.UTF8);
                if (isBatch) EditorApplication.Exit(1);
                return;
            }

            // ==================== A) SKIN ====================
            sb.AppendLine("--------------------------------------------------");
            sb.AppendLine("A) SKIN (MALHA DEFORMÁVEL POR ESQUELETO):");
            sb.AppendLine("--------------------------------------------------");
            var smrs = playerGO.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var staticRenderers = playerGO.GetComponentsInChildren<MeshFilter>(true);

            bool hasSkin = smrs.Length > 0;
            sb.AppendLine($"TEM SKIN: {(hasSkin ? "SIM" : "NÃO")}");
            sb.AppendLine($"Quantidade de SkinnedMeshRenderers: {smrs.Length}");
            sb.AppendLine($"Quantidade de MeshRenderers estáticos no Player: {staticRenderers.Length}");

            int totalVertices = 0;
            foreach (var smr in smrs)
            {
                var mesh = smr.sharedMesh;
                int vertCount = mesh != null ? mesh.vertexCount : 0;
                totalVertices += vertCount;
                int boneCount = smr.bones != null ? smr.bones.Length : 0;
                int bindposeCount = mesh != null ? mesh.bindposes.Length : 0;
                int boneWeightCount = mesh != null ? mesh.boneWeights.Length : 0;

                sb.AppendLine($"  - SMR: '{smr.name}' (GameObject: '{smr.gameObject.name}')");
                sb.AppendLine($"    Vértices: {vertCount} | Ossos conectados: {boneCount} | Bindposes: {bindposeCount}");
                sb.AppendLine($"    BoneWeights definidos: {(boneWeightCount > 0 ? $"SIM ({boneWeightCount})" : "NÃO (0)")}");
                sb.AppendLine($"    RootBone: {(smr.rootBone != null ? smr.rootBone.name : "NULO")}");

                sb.AppendLine("    Materiais e Texturas:");
                foreach (var mat in smr.sharedMaterials)
                {
                    if (mat == null)
                    {
                        sb.AppendLine("      * Material: NULO");
                        continue;
                    }
                    var mainTex = mat.mainTexture;
                    sb.AppendLine($"      * Material: '{mat.name}' (Shader: {mat.shader.name})");
                    sb.AppendLine($"        Textura Principal: {(mainTex != null ? $"{mainTex.name} (Existe: SIM)" : "NENHUMA (ou cor pura/shader procedural)")}");
                }
            }

            // Verificar se FBX original contém malha (With Skin)
            string playerFBXPath = "Assets/_Project/ThirdParty/_INBOX/Mixamo/Gunplay.fbx";
            if (!File.Exists(playerFBXPath)) playerFBXPath = "Assets/_Project/ThirdParty/_INBOX/Gunplay.fbx";
            if (File.Exists(playerFBXPath))
            {
                var fbxSubAssets = AssetDatabase.LoadAllAssetsAtPath(playerFBXPath);
                int meshCount = 0;
                foreach (var a in fbxSubAssets) if (a is Mesh) meshCount++;
                sb.AppendLine($"\n  Checagem FBX Original ('{playerFBXPath}'):");
                sb.AppendLine($"    Tipo do FBX: {(meshCount > 0 ? $"WITH SKIN (Contém {meshCount} meshes na importação)" : "WITHOUT SKIN (Apenas animação)")}");
            }

            // ==================== B) RIG E AVATAR ====================
            sb.AppendLine("\n--------------------------------------------------");
            sb.AppendLine("B) RIG E AVATAR:");
            sb.AppendLine("--------------------------------------------------");
            var anim = playerGO.GetComponent<Animator>();
            if (anim != null)
            {
                var avatar = anim.avatar;
                sb.AppendLine($"Animator no Player: Presente");
                sb.AppendLine($"Avatar Atribuído: {(avatar != null ? avatar.name : "NULO")}");
                sb.AppendLine($"Avatar.isValid: {(avatar != null ? avatar.isValid.ToString() : "N/A")}");
                sb.AppendLine($"Avatar.isHuman: {(avatar != null ? avatar.isHuman.ToString() : "N/A")}");
            }
            else
            {
                sb.AppendLine($"[AVISO] Nenhum componente Animator no root do Player!");
            }

            if (File.Exists(playerFBXPath))
            {
                var imp = AssetImporter.GetAtPath(playerFBXPath) as ModelImporter;
                if (imp != null)
                {
                    sb.AppendLine($"ModelImporter do Player FBX:");
                    sb.AppendLine($"  animationType: {imp.animationType}");
                    sb.AppendLine($"  avatarSetup: {imp.avatarSetup}");
                    sb.AppendLine($"  globalScale: {imp.globalScale}");
                    sb.AppendLine($"  useFileUnits: {imp.useFileUnits}");
                }
            }

            // ==================== C) ANIMATOR ====================
            sb.AppendLine("\n--------------------------------------------------");
            sb.AppendLine("C) ANIMATOR E HIERARQUIA:");
            sb.AppendLine("--------------------------------------------------");
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                var rac = anim.runtimeAnimatorController;
                sb.AppendLine($"RuntimeAnimatorController: '{rac.name}'");
                sb.AppendLine($"Apply Root Motion: {anim.applyRootMotion}");

                var ac = rac as AnimatorController;
                if (ac != null)
                {
                    sb.AppendLine($"Camadas ({ac.layers.Length}):");
                    for (int l = 0; l < ac.layers.Length; l++)
                    {
                        var layer = ac.layers[l];
                        sb.AppendLine($"  - Camada {l}: '{layer.name}' (Máscara: {(layer.avatarMask != null ? layer.avatarMask.name : "NENHUMA")})");
                        var sm = layer.stateMachine;
                        sb.AppendLine($"    Estados ({sm.states.Length}):");
                        foreach (var cs in sm.states)
                        {
                            var state = cs.state;
                            string motionName = state.motion != null ? state.motion.name : "NENHUM";
                            string motionPath = state.motion != null ? AssetDatabase.GetAssetPath(state.motion) : "N/A";
                            sb.AppendLine($"      * Estado '{state.name}' -> Motion: '{motionName}' (Arquivo: {motionPath})");
                        }
                    }

                    sb.AppendLine($"Parâmetros ({ac.parameters.Length}):");
                    foreach (var p in ac.parameters)
                    {
                        sb.AppendLine($"  - {p.name} ({p.type})");
                    }
                }
            }
            else
            {
                sb.AppendLine("Animator Controller: NULO");
            }

            sb.AppendLine("\nEscala (localScale) dos Transforms na hierarquia do Player:");
            CheckScalesRecursive(playerGO.transform, "", sb);

            // ==================== D) INVENTÁRIO DE CLIPES ====================
            sb.AppendLine("\n--------------------------------------------------");
            sb.AppendLine("D) INVENTÁRIO DE CLIPES DISPONÍVEIS:");
            sb.AppendLine("--------------------------------------------------");
            string[] fbxFiles = Directory.GetFiles("Assets/_Project/ThirdParty/_INBOX", "*.fbx", SearchOption.AllDirectories);
            var clipsFound = new List<AnimationClip>();

            foreach (var fbx in fbxFiles)
            {
                string normPath = fbx.Replace("\\", "/");
                var assets = AssetDatabase.LoadAllAssetsAtPath(normPath);
                foreach (var a in assets)
                {
                    if (a is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    {
                        clipsFound.Add(clip);
                        string cat = ClassifyClip(clip.name, Path.GetFileNameWithoutExtension(fbx));
                        sb.AppendLine($"  - Clipa: '{clip.name}' no FBX '{Path.GetFileName(fbx)}' | Classificação: {cat}");
                    }
                }
            }

            // ==================== E) TESTE DE ESTICAMENTO ====================
            sb.AppendLine("\n--------------------------------------------------");
            sb.AppendLine("E) TESTE DE ESTICAMENTO (RELAÇÃO OSSO-PAI):");
            sb.AppendLine("--------------------------------------------------");

            RunStretchingTest(playerGO, anim, sb);

            sb.AppendLine("\n==================================================");
            sb.AppendLine("               FIM DO DIAGNÓSTICO                 ");
            sb.AppendLine("==================================================");

            Directory.CreateDirectory("docs");
            File.WriteAllText(DIAGNOSTIC_PATH, sb.ToString(), Encoding.UTF8);
            Debug.Log($"[PlayerDiagnosticTool] Diagnóstico salvo com sucesso em {DIAGNOSTIC_PATH}");

            if (isBatch) EditorApplication.Exit(0);
        }

        private static void CheckScalesRecursive(Transform t, string indent, StringBuilder sb)
        {
            bool scaleDiff = t.localScale != Vector3.one;
            if (scaleDiff)
            {
                sb.AppendLine($"{indent}* '{t.name}' localScale = {t.localScale} [ALERTA: DIFERENTE DE 1]");
            }
            for (int i = 0; i < t.childCount; i++)
            {
                CheckScalesRecursive(t.GetChild(i), indent + "  ", sb);
            }
        }

        private static string ClassifyClip(string clipName, string fbxName)
        {
            string combined = $"{clipName}_{fbxName}".ToLower();
            if (combined.Contains("idle")) return "IDLE";
            if (combined.Contains("walk") || combined.Contains("caminha")) return "WALK";
            if (combined.Contains("run") || combined.Contains("correr")) return "RUN";
            if (combined.Contains("strafe")) return "STRAFE";
            if (combined.Contains("back")) return "BACKWARD";
            if (combined.Contains("shoot") || combined.Contains("gunplay") || combined.Contains("fire")) return "SHOOT / COMBAT";
            if (combined.Contains("die") || combined.Contains("death") || combined.Contains("dying")) return "DEATH";
            if (combined.Contains("attack")) return "ATTACK";
            if (combined.Contains("cast") || combined.Contains("spell")) return "MAGIC CAST";
            return "OUTRO";
        }

        private static void RunStretchingTest(GameObject playerGO, Animator anim, StringBuilder sb)
        {
            if (anim == null || !anim.isHuman)
            {
                sb.AppendLine("[AVISO] Teste de esticamento requer Animator com Rig Humanoid válido.");
                return;
            }

            var bonePairs = new (HumanBodyBones child, HumanBodyBones parent)[]
            {
                (HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftUpperArm),
                (HumanBodyBones.LeftHand, HumanBodyBones.LeftLowerArm),
                (HumanBodyBones.RightLowerArm, HumanBodyBones.RightUpperArm),
                (HumanBodyBones.RightHand, HumanBodyBones.RightLowerArm),
                (HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftUpperLeg),
                (HumanBodyBones.LeftFoot, HumanBodyBones.LeftLowerLeg),
                (HumanBodyBones.RightLowerLeg, HumanBodyBones.RightUpperLeg),
                (HumanBodyBones.RightFoot, HumanBodyBones.RightLowerLeg),
                (HumanBodyBones.Head, HumanBodyBones.Neck),
                (HumanBodyBones.Spine, HumanBodyBones.Hips)
            };

            // Testar os clipes usados no AnimatorController
            var rac = anim.runtimeAnimatorController as AnimatorController;
            if (rac == null) return;

            var clipsTested = new HashSet<AnimationClip>();
            foreach (var layer in rac.layers)
            {
                foreach (var cs in layer.stateMachine.states)
                {
                    if (cs.state.motion is AnimationClip clip)
                        clipsTested.Add(clip);
                }
            }

            sb.AppendLine($"Clipes a testar: {clipsTested.Count}");
            sb.AppendLine("Limite de tolerância estipulado: 3.00%");
            sb.AppendLine("-------------------------------------------------------------------------------------");
            sb.AppendLine(string.Format("{0,-20} | {1,-28} | {2,-15} | {3,-10}", "CLIPE", "PIOR OSSO", "VARIAÇÃO MÁX", "STATUS"));
            sb.AppendLine("-------------------------------------------------------------------------------------");

            foreach (var clip in clipsTested)
            {
                float worstVarOverall = 0f;
                string worstBonePair = "Nenhum";

                foreach (var pair in bonePairs)
                {
                    var childT = anim.GetBoneTransform(pair.child);
                    var parentT = anim.GetBoneTransform(pair.parent);
                    if (childT == null || parentT == null) continue;

                    float initialDist = 0f;
                    float maxVar = 0f;

                    for (int i = 0; i < 12; i++)
                    {
                        float sampleTime = (i / 11.0f) * clip.length;
                        clip.SampleAnimation(playerGO, sampleTime);

                        float currentDist = Vector3.Distance(childT.position, parentT.position);
                        if (i == 0)
                        {
                            initialDist = currentDist;
                        }
                        else if (initialDist > 0.0001f)
                        {
                            float varPct = Mathf.Abs(currentDist - initialDist) / initialDist * 100f;
                            if (varPct > maxVar) maxVar = varPct;
                        }
                    }

                    if (maxVar > worstVarOverall)
                    {
                        worstVarOverall = maxVar;
                        worstBonePair = $"{pair.child}->{pair.parent}";
                    }
                }

                bool pass = worstVarOverall <= 3.0f;
                sb.AppendLine(string.Format("{0,-20} | {1,-28} | {2,13:F2}% | {3,-10}",
                    clip.name, worstBonePair, worstVarOverall, pass ? "PASS" : "FAIL (>3%)"));
            }
            sb.AppendLine("-------------------------------------------------------------------------------------");
        }
    }
}

