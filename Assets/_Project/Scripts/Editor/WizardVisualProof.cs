#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

namespace ArenaSurvivor.Editor
{
    // Prova visual: instância os 2 bruxos lado a lado, poseia walk/cast e tira
    // close-ups com a textura do pack.
    public static class WizardVisualProof
    {
        const string OUT = "docs/screens/wizards";
        static readonly StringBuilder sb = new StringBuilder();

        [MenuItem("Tools/Arena/Wizards: Visual Proof")]
        public static void Run()
        {
            System.IO.Directory.CreateDirectory(OUT);
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Arena.unity", OpenSceneMode.Single);

            sb.AppendLine("=== WIZARD VISUAL PROOF ===");
            var fire = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/ThirdParty/Wizard_Free/Characters/Fire_Wizard.fbx");
            var air = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/ThirdParty/Wizard_Free/Characters/Air_Wizard.fbx");

            // prefabs reais do jogo
            var boss = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemy_Boss.prefab");
            var sorc = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemy_Sorcerer.prefab");
            sb.AppendLine($"boss={(boss != null)} sorcerer={(sorc != null)}");

            // contagem de ossos sob Model_Mage / Model_Sorcerer dos PREFABS
            foreach (var (nm, pf) in new[] { ("boss", boss), ("sorcerer", sorc) })
            {
                int n = 0; string sample = "";
                foreach (var tr in pf.GetComponentsInChildren<Transform>(true))
                {
                    if (tr.name.Contains("spine") || tr.name.Contains("upper") || tr.name.Contains("_metarig"))
                    {
                        n++;
                        if (sample.Length < 60) sample += tr.name + ";";
                    }
                }
                sb.AppendLine($"[{nm}] bones-prefab={n} sample={sample}");
            }

            var inst = new System.Collections.Generic.List<GameObject>
            {
                Object.Instantiate(boss),
                Object.Instantiate(sorc),
            };
            // desliga qualquer Animator nas INSTÂNCIAS (o bake é por SampleAnimation direto)
            foreach (var g in inst)
                foreach (var a in g.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            inst[0].transform.position = new Vector3(-1.4f, 0, 6f);
            inst[1].transform.position = new Vector3(1.4f, 0, 6f);

            // prova de animação: amostra o Walk em 2 momentos de cada
            var walkF = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Animations/Wizards/Fire_Wizard_Anim/Fire_Wizard_Walk.anim");
            var walkA = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Animations/Wizards/Air_Wizard_Anim/Air_Wizard_Walk.anim");
            var castF = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Animations/Wizards/Fire_Wizard_Anim/Fire_Wizard_Cast.anim");

            System.Action<AnimationClip, GameObject, float> ApplyPose = (clip, go, t) =>
            {
                if (clip == null || go == null) return;
                int miss = 0, hit = 0;
                foreach (var b in AnimationUtility.GetCurveBindings(clip))
                {
                    var tb = AnimationUtility.GetEditorCurve(clip, b);
                    var v = tb.Evaluate(t);
                    var tgo = go.transform.Find(b.path);
                    if (tgo == null) { if (miss == 0) sb.AppendLine($"[MISS] path={b.path}"); miss++; continue; }
                    hit++;
                    if (b.propertyName.StartsWith("m_LocalRotation."))
                    {
                        var sub = b.propertyName.Substring("m_LocalRotation.".Length);
                        var q = tgo.localRotation;
                        // copy direto (sem espelho) — o valor do clipe já é o rest+delta local
                        switch (sub) { case "x": q.x = v; break; case "y": q.y = v; break; case "z": q.z = v; break; case "w": q.w = v; break; }
                        tgo.localRotation = q;
                    }
                    else if (b.propertyName.StartsWith("m_LocalPosition."))
                    {
                        var sub = b.propertyName.Substring("m_LocalPosition.".Length);
                        var pv = tgo.localPosition;
                        switch (sub) { case "x": pv.x = v; break; case "y": pv.y = v; break; case "z": pv.z = v; break; }
                        tgo.localPosition = pv;
                    }
                }
                sb.AppendLine($"[APPLY] clip={clip.name} t={t:0.00} hit={hit} miss={miss}");
            };

            var camGO = new GameObject("_ProofCam");
            var cam = camGO.AddComponent<Camera>();
            var src = Camera.main;
            if (src != null) cam.CopyFrom(src);

            Vector3 mid = new Vector3(0, 0, 6f);
            cam.transform.position = mid + new Vector3(0, 1.8f, -5.5f);
            cam.transform.LookAt(mid + new Vector3(0, 1.0f, 0));
            Capture(cam, "00_dois_bruxos.png");

            // frames de andar (fase oposta da passada)
            ApplyPose(walkF, inst[0], walkF != null ? walkF.length * 0.25f : 0f);
            ApplyPose(walkA, inst[1], walkA != null ? walkA.length * 0.25f : 0f);
            foreach (var (nm, g) in new[] { ("fire", inst[0]), ("air", inst[1]) })
            {
                var b1 = g.transform.Find("Model_" + (nm == "fire" ? "Mage" : "Sorcerer") + "/Fire" + (nm == "fire" ? "_Wizard" : "_Wizard") + "_metarig/spine/spine.001");
                if (b1 == null) { b1 = g.GetComponentInChildren<Transform>(); foreach (var tr in g.GetComponentsInChildren<Transform>(true)) if (tr.name == "spine.001") { b1 = tr; break; } }
                sb.AppendLine($"[POSE] {nm} spine.001 euler={b1.localEulerAngles} rest={b1.localPosition}");
                // compara com o visual do FBX original
                var fbxG = Object.Instantiate(nm == "fire"
                    ? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/ThirdParty/Wizard_Free/Characters/Fire_Wizard.fbx")
                    : AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/ThirdParty/Wizard_Free/Characters/Air_Wizard.fbx"));
                var fBone = fbxG.GetComponentsInChildren<Transform>(true).First(x => x.name == "spine.001");
                sb.AppendLine($"[POSE-FBX] {nm} spine.001 euler={fBone.localEulerAngles}");
                Object.DestroyImmediate(fbxG);
            }
            Capture(cam, "01_andando_fase1.png");
            ApplyPose(walkF, inst[0], walkF != null ? walkF.length * 0.75f : 0f);
            ApplyPose(walkA, inst[1], walkA != null ? walkA.length * 0.75f : 0f);
            Capture(cam, "02_andando_fase2.png");

            // conjurando
            ApplyPose(castF, inst[0], castF != null ? castF.length * 0.5f : 0f);
            Capture(cam, "03_mago_conjurando.png");

            sb.AppendLine("[OK] provas salvas");
            System.IO.File.WriteAllText(OUT + "/proof.txt", sb.ToString(), Encoding.UTF8);
            Debug.Log(sb.ToString());
            Object.DestroyImmediate(camGO);
            EditorApplication.Exit(0);
        }

        static void Capture(Camera cam, string file)
        {
            int w = 1600, h = 900;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes($"{OUT}/{file}", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
        }
    }
}
#endif
