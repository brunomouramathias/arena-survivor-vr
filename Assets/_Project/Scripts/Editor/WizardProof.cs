#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using ArenaSurvivor.Enemies;

namespace ArenaSurvivor.Editor
{
    // Prova visual em EDIT MODE: instancia os prefabs reais, posa com o WizardAnim
    // (mesmo componente usado em jogo) em 3 momentos e renderiza PNGs.
    public static class WizardProof
    {
        const string DIR = "docs/screens/wizards";
        const string PREFABS = "Assets/_Project/Prefabs";

        [MenuItem("Tools/Arena/Wizards: Prova Visual (edit mode)")]
        public static void Run()
        {
            Directory.CreateDirectory(DIR);
            var sb = new StringBuilder();
            sb.AppendLine("=== WIZARD PROOF " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");

            var lightGo = new GameObject("_ProofLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            lightGo.transform.rotation = Quaternion.Euler(48f, 150f, 0f);
            var fillGo = new GameObject("_ProofFill");
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.45f;
            fillGo.transform.rotation = Quaternion.Euler(20f, -60f, 0f);

            Try(() => Prove("Enemy_Boss", "Model_Mage", "Fire_Wizard", sb));
            Try(() => Prove("Enemy_Sorcerer", "Model_Sorcerer", "Air_Wizard", sb));

            UnityEngine.Object.DestroyImmediate(lightGo);
            UnityEngine.Object.DestroyImmediate(fillGo);

            File.WriteAllText(DIR + "/proof.txt", sb.ToString(), Encoding.UTF8);
            Debug.Log(sb.ToString());
            EditorApplication.Exit(0);
        }

        static void Try(Action a)
        {
            try { a(); } catch (Exception e) { Debug.LogError(e); }
        }

        static void Prove(string prefabName, string modelName, string wizard, StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("---------- " + prefabName + " (" + wizard + ") ----------");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFABS}/{prefabName}.prefab");
            if (prefab == null) { sb.AppendLine("[ERRO] prefab nao encontrado"); return; }

            var inst = (GameObject)UnityEngine.Object.Instantiate(prefab);
            inst.transform.position = Vector3.zero;
            inst.transform.rotation = Quaternion.identity;

            var animators = inst.GetComponentsInChildren<Animator>(true);
            sb.AppendLine($"[ANIMATOR] componentes={animators.Length} (0 = animacao 100% procedural)");

            var wa = inst.GetComponent<WizardAnim>();
            sb.AppendLine($"[WIZARDANIM] componente={(wa != null)} modelRoot={(wa != null && wa.modelRoot != null ? wa.modelRoot.name : "null")}");
            if (wa == null) { UnityEngine.Object.DestroyImmediate(inst); return; }

            wa.Build();
            var model = inst.transform.Find(modelName);
            float height = Measure(model != null ? model : inst.transform);
            sb.AppendLine($"[ALTURA] {height:0.00} m");

            // ossos encontrados
            var bones = new[] { "spine", "spine.001", "spine.002", "spine.003", "spine.004", "spine.005",
                                "shoulder.L", "upper_arm.L", "forearm.L", "hand.L",
                                "shoulder.R", "upper_arm.R", "forearm.R", "hand.R",
                                "thigh.L", "shin.L", "foot.L", "thigh.R", "shin.R", "foot.R" };
            int found = 0;
            var missing = new List<string>();
            foreach (var b in bones)
            {
                bool ok = model != null && model.GetComponentsInChildren<Transform>(true).Any(t => t.name == b);
                if (ok) found++; else missing.Add(b);
            }
            sb.AppendLine($"[OSSOS] {found}/{bones.Length} encontrados" + (missing.Count > 0 ? " faltando: " + string.Join(",", missing) : ""));

            var camGo = new GameObject("_ProofCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.12f, 0.16f);
            cam.fieldOfView = 42f;

            var cases = new (string name, float phase, float move, float castBlend)[]
            {
                ("Idle",  0.7f, 0.0f, 0.0f),
                ("Walk",  1.6f, 1.0f, 0.0f),
                ("Walk2", 4.3f, 1.0f, 0.0f),
                ("Cast",  2.2f, 0.0f, 1.0f),
                ("Poder", 2.2f, 0.0f, 1.0f),
            };

            var spine = FindDeep(model, "spine");
            int n = 0;
            foreach (var c in cases)
            {
                wa.Pose(c.phase, c.move, c.castBlend);
                wa.PreviewPower(c.name == "Poder" ? 1f : 0f);
                string pose = spine != null ? spine.localEulerAngles.ToString("F1") : "-";
                string armPose = FindDeep(model, "upper_arm.L") is Transform al ? al.localEulerAngles.ToString("F1") : "-";
                string orb = "-";
                var orbGo = FindDeep(inst.transform, "WizardPowerGlow");
                if (orbGo != null)
                    orb = orbGo.gameObject.activeInHierarchy
                        ? "ATIVO escala=" + orbGo.localScale.x.ToString("0.00")
                        : "inativo";
                var hl = FindDeep(inst.transform, "hand.R");
                string hand = "-";
                if (hl != null)
                {
                    var lp = inst.transform.InverseTransformPoint(hl.position);
                    hand = $"({lp.x:0.00},{lp.y:0.00},{lp.z:0.00})";
                }
                sb.AppendLine($"[POSE] {c.name}: spine={pose} upper_arm.L={armPose} maoR={hand} orbe={orb}");

                var dir = new Vector3(0.62f, 0.30f, -1f).normalized;
                cam.transform.position = inst.transform.position + Vector3.up * (height * 0.55f) + dir * (height * 2.0f);
                cam.transform.LookAt(inst.transform.position + Vector3.up * (height * 0.50f));
                Render(cam, $"{DIR}/{wizard}_{n:00}_{c.name}.png");
                n++;
            }

            UnityEngine.Object.DestroyImmediate(camGo);
            UnityEngine.Object.DestroyImmediate(inst);
            sb.AppendLine($"[OK] {n} imagens geradas para {wizard}");
        }

        static float Measure(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
            if (rs.Length == 0) return 0f;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b.size.y;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        static void Render(Camera cam, string path)
        {
            int w = 1024, h = 1024;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}
#endif
