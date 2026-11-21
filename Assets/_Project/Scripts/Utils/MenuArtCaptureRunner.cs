#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using ArenaSurvivor.Enemies;
using ArenaSurvivor.UI;

namespace ArenaSurvivor.Utils
{
    // Captura a arte de fundo (key art) da tela de abertura a partir da arena REAL,
    // com um "diorama" montado na mão: jogador + monstros + bruxos em volta das torres,
    // sem HUD e com o mesmo post-processing do jogo.
    public class MenuArtCaptureRunner : MonoBehaviour
    {
        public static bool AutoRun => SessionState.GetBool("MenuArt.AutoRun", false);

        readonly StringBuilder sb = new StringBuilder();
        readonly List<GameObject> spawned = new List<GameObject>();

        private IEnumerator Start()
        {
            if (!AutoRun) { enabled = false; yield break; }

            yield return new WaitForSecondsRealtime(3f);

            var wm = FindAnyObjectByType<ArenaSurvivor.Waves.WaveManager>();
            wm?.StopAllCoroutines();                       // congela as ondas

            // limpa inimigos que já nasceram
            foreach (var e in FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
                Destroy(e.gameObject);
            yield return null;

            HideUi();
            BuildDiorama();

            yield return new WaitForSecondsRealtime(1.5f);  // deixa animações/partículas assentarem

            Directory.CreateDirectory("docs/screens/menu");
            string ts = System.DateTime.Now.ToString("HHmmss");

            Shot($"arte_1_{ts}.png", new Vector3(6.0f, 2.6f, -11f),  new Vector3(0.5f, 2.0f, 2f), 55f);
            Shot($"arte_2_{ts}.png", new Vector3(11f, 4.6f, -12f),   new Vector3(-1f, 1.4f, 1f), 48f);
            Shot($"arte_3_{ts}.png", new Vector3(-8.5f, 3.2f, -10f), new Vector3(1f, 1.8f, 2f),  50f);
            Shot($"arte_4_{ts}.png", new Vector3(0f, 6.5f, -14f),    new Vector3(0f, 1.2f, 0.5f), 44f);
            Shot($"arte_5_{ts}.png", new Vector3(4.2f, 1.9f, -7.5f), new Vector3(0f, 2.4f, 4f),  60f);

            Debug.Log(sb.ToString());
            SessionState.SetBool("MenuArt.AutoRun", false);
            EditorApplication.Exit(0);
        }

        private void HideUi()
        {
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                canvas.gameObject.SetActive(false);
            var hud = FindAnyObjectByType<GameHUD>();
            if (hud != null) hud.gameObject.SetActive(false);
        }

        // Esconde tudo que é "HUD 3D" (barras de vida, avisos de spawn) — chamado a cada captura
        private void HideWorldUi()
        {
            foreach (var bar in FindObjectsByType<EnemyHealthBar>(FindObjectsSortMode.None))
            {
                bar.SetVisible(false);
                for (int i = 0; i < bar.transform.childCount; i++)
                {
                    var child = bar.transform.GetChild(i);
                    if (child.name.Contains("EnemyHealthBar") || child.name.Contains("BG") || child.name.Contains("Fill"))
                        child.gameObject.SetActive(false);
                }
            }
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                var n = r.gameObject.name.ToLowerInvariant();
                if (n.Contains("warning") || n.Contains("aviso"))
                    r.gameObject.SetActive(false);
            }

            // Barras de vida das torres (TowerHealthBar) e dos inimigos
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t == null) continue;
                var n = t.name.ToLowerInvariant();
                if (n.Contains("healthbar"))
                    t.gameObject.SetActive(false);
            }
        }

        private void BuildDiorama()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            Vector3 center = player != null ? player.transform.position : Vector3.zero;

            Spawn("Assets/_Project/Prefabs/Enemy_Chaser.prefab",   center + new Vector3(4.6f, 0f, 3.4f),  center);
            Spawn("Assets/_Project/Prefabs/Enemy_Chaser.prefab",   center + new Vector3(7.2f, 0f, 1.2f),  center);
            Spawn("Assets/_Project/Prefabs/Enemy_Stalker.prefab",  center + new Vector3(-5.4f, 0f, 3.0f), center);
            Spawn("Assets/_Project/Prefabs/Enemy_Sorcerer.prefab", center + new Vector3(-3.2f, 0f, 5.2f), center);
            Spawn("Assets/_Project/Prefabs/Enemy_Boss.prefab",     center + new Vector3(1.4f, 0f, 7.4f),  center);

            if (player != null)
                player.transform.rotation = Quaternion.Euler(0f, 200f, 0f);   // de frente para os inimigos
        }

        private void Spawn(string path, Vector3 pos, Vector3 lookAt)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { sb.AppendLine("prefab não encontrado: " + path); return; }

            var go = Instantiate(prefab, pos, Quaternion.identity);
            spawned.Add(go);

            var enemy = go.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                enemy.Initialize(1);
                var agent = go.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null) agent.enabled = false;      // fica parado no lugar do diorama
            }

            var dir = lookAt - pos; dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f) go.transform.rotation = Quaternion.LookRotation(dir);
        }

        private void Shot(string file, Vector3 pos, Vector3 lookAt, float fov)
        {
            int w = 1920, h = 1080;

            var go = new GameObject("_ArtCam");
            var cam = go.AddComponent<Camera>();
            cam.CopyFrom(Camera.main);
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation((lookAt - pos).normalized, Vector3.up);
            cam.fieldOfView = fov;
            cam.clearFlags = CameraClearFlags.Skybox;

            // post-processing igual ao do jogo (tonemapping, bloom das tochas, vinheta)
            var urpData = cam.GetUniversalAdditionalCameraData();
            urpData.renderPostProcessing = true;
            urpData.renderShadows        = true;
            urpData.antialiasing         = AntialiasingMode.FastApproximateAntialiasing;
            urpData.antialiasingQuality  = AntialiasingQuality.High;

            HideWorldUi();

            if (file.Contains("arte_1"))
            {
                foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    string n = r.gameObject.name.ToLowerInvariant();
                    if (!(n.Contains("bar") || n.Contains("fill") || n.Contains("bg"))) continue;
                    var chain = r.transform.parent != null ? r.transform.parent.parent != null ? r.transform.parent.parent.name : "?" : "-";
                    sb.AppendLine($"BAR? '{r.gameObject.name}' ativo={r.gameObject.activeInHierarchy} pos={r.transform.position} dono='{chain}'");
                }
            }

            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;

            File.WriteAllBytes("docs/screens/menu/" + file, tex.EncodeToPNG());
            Destroy(tex);
            Destroy(rt);
            Destroy(go);

            sb.AppendLine($"{file}: pos={pos} look={lookAt} fov={fov}");
        }
    }
}
#endif
