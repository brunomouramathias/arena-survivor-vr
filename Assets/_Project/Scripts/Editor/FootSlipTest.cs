using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ArenaSurvivor.Utils;

namespace ArenaSurvivor.Editor
{
    public static class FootSlipTest
    {
        [MenuItem("Tools/Arena/Test Foot Slip")]
        public static void Run() { Prepare(); }

        public static void RunBatch() { Prepare(); }

        private static void Prepare()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGO = new GameObject("Sun");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightGO.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(10f, 1f, 10f);

            var camGO = new GameObject("Cam");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.fieldOfView = 45f;
            camGO.transform.position = new Vector3(0f, 2f, -4f);
            camGO.transform.LookAt(new Vector3(0f, 1f, 0f));

            var arena = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Arena.unity", OpenSceneMode.Additive);
            var src = GameObject.FindGameObjectWithTag("Player");
            if (src == null) { EditorApplication.Exit(3); return; }

            var clone = Object.Instantiate(src);
            clone.transform.position = Vector3.zero;
            clone.transform.rotation = Quaternion.identity;
            EditorSceneManager.MoveGameObjectToScene(clone, scene);
            EditorSceneManager.CloseScene(arena, true);

            var runner = new GameObject("FootSlipRunner");
            runner.AddComponent<FootSlipRunner>();
            UnityEditor.SessionState.SetBool("FootSlip.AutoRun", true);

            EditorApplication.EnterPlaymode();
        }
    }
}
