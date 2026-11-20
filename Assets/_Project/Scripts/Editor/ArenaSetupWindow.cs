using UnityEditor;
using UnityEngine;

namespace ArenaSurvivor.Editor
{
    // Janela de configuração rápida do projeto
    public class ArenaSetupWindow : EditorWindow
    {
        [MenuItem("Tools/Arena/Open Setup Window")]
        public static void ShowWindow()
        {
            GetWindow<ArenaSetupWindow>("Arena Setup");
        }

        private void OnGUI()
        {
            GUILayout.Label("ArenaSurvivor — Configuração Rápida", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (GUILayout.Button("1. Build Everything (Setup Completo)", GUILayout.Height(40)))
                ProjectBuilder.BuildEverything();

            EditorGUILayout.Space();
            GUILayout.Label("Passos individuais:", EditorStyles.boldLabel);

            if (GUILayout.Button("Setup Layers & Tags"))    ProjectBuilder.SetupLayersAndTags();
            if (GUILayout.Button("Setup Physics Matrix"))   ProjectBuilder.SetupPhysicsMatrix();
            if (GUILayout.Button("Create ScriptableObjects")) ProjectBuilder.CreateScriptableObjects();
            if (GUILayout.Button("Create Arena Scene"))     ProjectBuilder.CreateArenaScene();
            if (GUILayout.Button("Create MainMenu Scene"))  ProjectBuilder.CreateMainMenuScene();
            if (GUILayout.Button("Create Sandbox Scene"))   ProjectBuilder.CreateSandboxScene();
            if (GUILayout.Button("Setup Build Settings"))   ProjectBuilder.SetupBuildSettings();

            EditorGUILayout.Space();
            GUILayout.Label("Diagnóstico e Testes:", EditorStyles.boldLabel);
            if (GUILayout.Button("Inventory INBOX"))               AssetInventoryTool.InventoryInbox();
            if (GUILayout.Button("Run Player Movement Test"))      PlayerMovementTest.RunMovementTest();

            EditorGUILayout.Space();
            GUILayout.Label("Manutenção:", EditorStyles.boldLabel);
            if (GUILayout.Button("Fix Imported Assets (Materiais URP)")) ProjectBuilder.FixImportedAssets();
            if (GUILayout.Button("Validate Project"))       ProjectBuilder.ValidateProject();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Execute 'Build Everything' uma vez para criar toda a estrutura do projeto.\n" +
                "Depois abra Assets/_Project/Scenes/Arena.unity e pressione Play.",
                MessageType.Info
            );
        }
    }
}
