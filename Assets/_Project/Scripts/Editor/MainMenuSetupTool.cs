#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using ArenaSurvivor.UI;

namespace ArenaSurvivor.Editor
{
    // Monta a cena MainMenu: canvas procedural com a tela de abertura (MainMenuScreen)
    // e aplica as configurações de importação da arte de fundo.
    public static class MainMenuSetupTool
    {
        private const string ScenePath   = "Assets/_Project/Scenes/MainMenu.unity";
        private const string BackdropPath = "Assets/_Project/Resources/Menu_Backdrop.png";

        [MenuItem("Tools/Arena/Rebuild Menu Principal")]
        public static void Run() { Rebuild(); }

        public static void RunBatch() { Rebuild(); }

        public static void Rebuild()
        {
            var sb = new StringBuilder();

            ConfigureBackdrop(sb);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // remove o canvas legado (vazio) e qualquer canvas novo de execução anterior
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == "Canvas_MainMenu" || go.name == "UI_MainMenu")
                {
                    sb.AppendLine("removido: " + go.name);
                    Object.DestroyImmediate(go);
                }
            }

            var ui = new GameObject("UI_MainMenu", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = ui.GetComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 100;

            var scaler = ui.GetComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;

            ui.AddComponent<MainMenuScreen>();
            sb.AppendLine("criado: UI_MainMenu (Canvas + CanvasScaler + GraphicRaycaster + MainMenuScreen)");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            sb.AppendLine("cena salva: " + ScenePath);
            sb.AppendLine("hierarquia:");
            foreach (var go in scene.GetRootGameObjects())
                sb.AppendLine("  - " + go.name);

            Debug.Log(sb.ToString());
        }

        private static void ConfigureBackdrop(StringBuilder sb)
        {
            var importer = AssetImporter.GetAtPath(BackdropPath) as TextureImporter;
            if (importer == null)
            {
                sb.AppendLine("arte de fundo não encontrada em " + BackdropPath);
                return;
            }

            importer.textureType         = TextureImporterType.Sprite;
            importer.spriteImportMode    = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled       = false;
            importer.alphaIsTransparency = false;
            importer.maxTextureSize      = 2048;
            importer.textureCompression  = TextureImporterCompression.CompressedHQ;
            importer.sRGBTexture         = true;
            importer.SaveAndReimport();
            sb.AppendLine("arte de fundo importada como Sprite: " + BackdropPath);
        }
    }
}
#endif
