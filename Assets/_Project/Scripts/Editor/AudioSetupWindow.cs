using UnityEngine;
using UnityEditor;
using ArenaSurvivor.Audio;
using ArenaSurvivor.Enemies;

namespace ArenaSurvivor.Editor
{
    public class AudioSetupWindow : EditorWindow
    {
        private AudioManager audioManager;

        [MenuItem("ArenaSurvivor/Setup Audio")]
        public static void ShowWindow()
        {
            GetWindow<AudioSetupWindow>("Audio Setup");
        }

        private void OnGUI()
        {
            GUILayout.Label("Configuração de Áudio", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (GUILayout.Button("Encontrar AudioManager na Cena", GUILayout.Height(30)))
            {
                audioManager = FindAnyObjectByType<AudioManager>();
                if (audioManager == null)
                {
                    var go = new GameObject("AudioManager");
                    audioManager = go.AddComponent<AudioManager>();
                }
                Debug.Log("AudioManager encontrado/criado.");
            }

            EditorGUILayout.Space();
            GUILayout.Label("Dicas para sons profissionais:", EditorStyles.boldLabel);
            GUILayout.Label("• Use sons de impacto secos e realistas (não 'cartoon')");
            GUILayout.Label("• Sons de morte: gemido baixo + som de corpo caindo");
            GUILayout.Label("• Sons de hit: impacto surdo + leve grunhido");
            GUILayout.Label("• Sons idle: respiração pesada, passos arrastados");
            GUILayout.Label("• Música: tensão crescente, sem melodia muito marcante");
            GUILayout.Label("");
            GUILayout.Label("Recursos gratuitos recomendados:");
            GUILayout.Label("• Freesound.org (busque por 'zombie', 'monster', 'impact')");
            GUILayout.Label("• Kenney.nl (assets de áudio gratuitos)");
            GUILayout.Label("• Unity Asset Store > Free > Audio");
            GUILayout.Label("");
            GUILayout.Label("Formato: .wav ou .ogg, 44.1kHz, 16-bit");
        }

        private void OnEnable()
        {
            audioManager = FindAnyObjectByType<AudioManager>();
        }
    }
}