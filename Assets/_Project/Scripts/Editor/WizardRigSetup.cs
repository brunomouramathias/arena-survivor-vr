#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using ArenaSurvivor.Enemies;

namespace ArenaSurvivor.Editor
{
    // Aplica a animação procedural (WizardAnim) nos prefabs dos bruxos,
    // remove o Animator (que deixava o corpo em T-pose) e normaliza a altura.
    public static class WizardRigSetup
    {
        const string PREFABS = "Assets/_Project/Prefabs";
        const string DIR = "docs/screens/wizards";

        [MenuItem("Tools/Arena/Wizards: Configurar Rig Procedural")]
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== WIZARD RIG SETUP " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");
            Setup("Enemy_Boss", "Model_Mage", 2.05f, sb);
            Setup("Enemy_Sorcerer", "Model_Sorcerer", 1.85f, sb);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Directory.CreateDirectory(DIR);
            File.WriteAllText(DIR + "/rig_setup.txt", sb.ToString(), Encoding.UTF8);
            Debug.Log(sb.ToString());
            EditorApplication.Exit(0);
        }

        static void Setup(string prefabName, string modelName, float targetHeight, StringBuilder sb)
        {
            string path = $"{PREFABS}/{prefabName}.prefab";
            using (var scope = new PrefabUtility.EditPrefabContentsScope(path))
            {
                var root = scope.prefabContentsRoot;
                sb.AppendLine();
                sb.AppendLine("---------- " + prefabName + " ----------");

                // 1) remove Animators (o FBX está em T-pose e o retarget deformava a pose)
                var animators = root.GetComponentsInChildren<Animator>(true);
                foreach (var a in animators)
                {
                    sb.AppendLine($"[REMOVE] Animator em '{a.gameObject.name}' (controller={(a.runtimeAnimatorController != null ? a.runtimeAnimatorController.name : "null")})");
                    Object.DestroyImmediate(a, true);
                }
                // restos de avatar/anim de import
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var comps = t.GetComponents<Component>();
                    foreach (var c in comps)
                        if (c == null) Object.DestroyImmediate(c, true);
                }
                sb.AppendLine($"[ANIMATOR] restantes={root.GetComponentsInChildren<Animator>(true).Length}");

                // 2) normaliza altura do modelo
                var model = root.transform.Find(modelName);
                if (model == null) { sb.AppendLine($"[ERRO] '{modelName}' não encontrado"); return; }

                // zera a escala para medir o tamanho natural
                var originalScale = model.localScale;
                model.localScale = Vector3.one;
                var rs = model.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
                float raw = 0f;
                if (rs.Length > 0)
                {
                    var b = rs[0].bounds;
                    foreach (var r in rs) b.Encapsulate(r.bounds);
                    raw = b.size.y;
                }
                float factor = raw > 0.01f ? targetHeight / raw : 1f;
                model.localScale = Vector3.one * factor;
                sb.AppendLine($"[ESCALA] {modelName}: alturaNatural={raw:0.00}m escala={factor:0.000} -> alvo {targetHeight:0.00}m (antes {originalScale.x:0.000})");

                var col = root.GetComponent<CapsuleCollider>();
                if (col != null)
                {
                    col.height = targetHeight + 0.2f;
                    col.radius = 0.42f;
                    col.center = new Vector3(0f, (targetHeight + 0.2f) * 0.5f, 0f);
                    sb.AppendLine($"[COLISOR] height={col.height:0.00} radius={col.radius:0.00}");
                }
                var agent = root.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null)
                {
                    agent.height = targetHeight;
                    agent.radius = 0.42f;
                    agent.baseOffset = 0f;
                    sb.AppendLine($"[NAVMESH] height={agent.height:0.00} radius={agent.radius:0.00}");
                }

                // 3) garante o componente de animação procedural
                var wa = root.GetComponent<WizardAnim>();
                if (wa == null) wa = root.AddComponent<WizardAnim>();
                wa.modelRoot = model;
                wa.speed = 0f; wa.cast = 0f; wa.attack = 0f;
                wa.glowColor = prefabName == "Enemy_Boss"
                    ? new Color(1f, 0.42f, 0.06f, 1f)   // fogo
                    : new Color(0.25f, 0.75f, 1f, 1f);   // ar/raio
                wa.glowSize = prefabName == "Enemy_Boss" ? 0.22f : 0.16f;
                wa.castLiftX = -90f; wa.castOpenZ = -90f; wa.castElbow = -55f;
                sb.AppendLine($"[WIZARDANIM] componente presente, modelRoot={modelName}, cor do poder={(prefabName == "Enemy_Boss" ? "laranja (fogo)" : "ciano (ar)")}");

                // 4) olhos emissivos visíveis (marcador de "poder")
                int eyes = 0;
                foreach (var t in model.GetComponentsInChildren<Transform>(true))
                    if (t.name.ToLower().Contains("eye")) eyes++;
                sb.AppendLine($"[OLHOS] transforms com 'eye' = {eyes}");
            }
            AssetDatabase.SaveAssets();
            sb.AppendLine("[OK] " + prefabName + " salvo");
        }
    }
}
#endif
