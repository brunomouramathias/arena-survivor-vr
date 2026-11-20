using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using ArenaSurvivor.UI;

namespace ArenaSurvivor.Editor
{
    public static class HUDBuilder
    {
        private const string SCENE_ARENA = "Assets/_Project/Scenes/Arena.unity";

        [MenuItem("Tools/Arena/Build HUD", priority = 20)]
        public static void BuildHUD()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != SCENE_ARENA)
            {
                if (File.Exists(SCENE_ARENA))
                    scene = EditorSceneManager.OpenScene(SCENE_ARENA, OpenSceneMode.Single);
            }

            var canvasGO = GameObject.Find("Canvas_HUD");
            if (canvasGO == null)
            {
                canvasGO = new GameObject("Canvas_HUD", typeof(RectTransform));
            }
            else if (canvasGO.GetComponent<RectTransform>() == null)
            {
                canvasGO.AddComponent<RectTransform>();
            }

            var canvas = canvasGO.GetComponent<Canvas>() ?? canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasGO.GetComponent<CanvasScaler>() ?? canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            if (canvasGO.GetComponent<GraphicRaycaster>() == null)
                canvasGO.AddComponent<GraphicRaycaster>();

            var uiManager = canvasGO.GetComponent<UIManager>() ?? canvasGO.AddComponent<UIManager>();
            var hudCtrl   = canvasGO.GetComponent<HUDController>() ?? canvasGO.AddComponent<HUDController>();

            // 1. Painel HUD
            var hudPanel = GetOrCreateChild(canvasGO.transform, "HUD_Panel");
            var hudRT = hudPanel.GetComponent<RectTransform>();
            SetFullStretch(hudRT);

            // Barra de vida
            var hpSliderGO = GetOrCreateChild(hudPanel.transform, "Health_Slider");
            var hpSliderRT = hpSliderGO.GetComponent<RectTransform>();
            hpSliderRT.anchorMin = new Vector2(0, 0);
            hpSliderRT.anchorMax = new Vector2(0, 0);
            hpSliderRT.pivot = new Vector2(0, 0);
            hpSliderRT.anchoredPosition = new Vector2(40, 40);
            hpSliderRT.sizeDelta = new Vector2(300, 30);

            var hpSlider = hpSliderGO.GetComponent<Slider>() ?? hpSliderGO.AddComponent<Slider>();
            hpSlider.minValue = 0f;
            hpSlider.maxValue = 1f;
            hpSlider.value = 1f;

            var hpFillGO = GetOrCreateChild(hpSliderGO.transform, "Fill");
            var hpFillImage = hpFillGO.GetComponent<Image>() ?? hpFillGO.AddComponent<Image>();
            hpFillImage.color = Color.green;
            SetFullStretch(hpFillGO.GetComponent<RectTransform>());
            hpSlider.fillRect = hpFillGO.GetComponent<RectTransform>();

            var hpTextGO = GetOrCreateChild(hpSliderGO.transform, "Health_Text");
            var hpText = hpTextGO.GetComponent<TextMeshProUGUI>() ?? hpTextGO.AddComponent<TextMeshProUGUI>();
            hpText.text = "100 / 100";
            hpText.fontSize = 18;
            hpText.alignment = TextAlignmentOptions.Center;
            SetFullStretch(hpTextGO.GetComponent<RectTransform>());

            // Munição
            var ammoGO = GetOrCreateChild(hudPanel.transform, "Ammo_Text");
            var ammoRT = ammoGO.GetComponent<RectTransform>();
            ammoRT.anchorMin = new Vector2(1, 0);
            ammoRT.anchorMax = new Vector2(1, 0);
            ammoRT.pivot = new Vector2(1, 0);
            ammoRT.anchoredPosition = new Vector2(-40, 40);
            ammoRT.sizeDelta = new Vector2(250, 40);

            var ammoText = ammoGO.GetComponent<TextMeshProUGUI>() ?? ammoGO.AddComponent<TextMeshProUGUI>();
            ammoText.text = "12 / ∞";
            ammoText.fontSize = 28;
            ammoText.alignment = TextAlignmentOptions.Right;

            // Onda e Pontuação (Topo Centro e Topo Direito)
            var waveGO = GetOrCreateChild(hudPanel.transform, "Wave_Text");
            var waveRT = waveGO.GetComponent<RectTransform>();
            waveRT.anchorMin = new Vector2(0.5f, 1);
            waveRT.anchorMax = new Vector2(0.5f, 1);
            waveRT.pivot = new Vector2(0.5f, 1);
            waveRT.anchoredPosition = new Vector2(0, -30);
            waveRT.sizeDelta = new Vector2(400, 50);

            var waveText = waveGO.GetComponent<TextMeshProUGUI>() ?? waveGO.AddComponent<TextMeshProUGUI>();
            waveText.text = "ONDA 1";
            waveText.fontSize = 32;
            waveText.alignment = TextAlignmentOptions.Center;

            var scoreGO = GetOrCreateChild(hudPanel.transform, "Score_Text");
            var scoreRT = scoreGO.GetComponent<RectTransform>();
            scoreRT.anchorMin = new Vector2(1, 1);
            scoreRT.anchorMax = new Vector2(1, 1);
            scoreRT.pivot = new Vector2(1, 1);
            scoreRT.anchoredPosition = new Vector2(-40, -30);
            scoreRT.sizeDelta = new Vector2(250, 40);

            var scoreText = scoreGO.GetComponent<TextMeshProUGUI>() ?? scoreGO.AddComponent<TextMeshProUGUI>();
            scoreText.text = "Pontos: 0";
            scoreText.fontSize = 24;
            scoreText.alignment = TextAlignmentOptions.Right;

            // Esquiva
            var dodgeGO = GetOrCreateChild(hudPanel.transform, "Dodge_Fill");
            var dodgeRT = dodgeGO.GetComponent<RectTransform>();
            dodgeRT.anchorMin = new Vector2(0, 0);
            dodgeRT.anchorMax = new Vector2(0, 0);
            dodgeRT.pivot = new Vector2(0, 0);
            dodgeRT.anchoredPosition = new Vector2(40, 80);
            dodgeRT.sizeDelta = new Vector2(120, 10);

            var dodgeFill = dodgeGO.GetComponent<Image>() ?? dodgeGO.AddComponent<Image>();
            dodgeFill.color = new Color(0.2f, 0.7f, 1f, 0.8f);

            // Vinheta de dano
            var dmgGO = GetOrCreateChild(hudPanel.transform, "Damage_Vignette");
            var dmgImg = dmgGO.GetComponent<Image>() ?? dmgGO.AddComponent<Image>();
            dmgImg.color = new Color(1f, 0f, 0f, 0f);
            dmgImg.raycastTarget = false;
            SetFullStretch(dmgGO.GetComponent<RectTransform>());

            // Mira Reticle
            var reticleGO = GetOrCreateChild(hudPanel.transform, "Reticle");
            var reticleRT = reticleGO.GetComponent<RectTransform>();
            reticleRT.sizeDelta = new Vector2(16, 16);
            var reticleImg = reticleGO.GetComponent<Image>() ?? reticleGO.AddComponent<Image>();
            reticleImg.color = Color.white;
            reticleImg.raycastTarget = false;

            // 2. Outros Painéis
            var shopPanel   = GetOrCreateChild(canvasGO.transform, "Shop_Panel");
            var pausePanel  = GetOrCreateChild(canvasGO.transform, "Pause_Panel");
            var goPanel     = GetOrCreateChild(canvasGO.transform, "GameOver_Panel");
            var vicPanel    = GetOrCreateChild(canvasGO.transform, "Victory_Panel");

            shopPanel.SetActive(false);
            pausePanel.SetActive(false);
            goPanel.SetActive(false);
            vicPanel.SetActive(false);

            // 3. Serializar no UIManager
            var uiSO = new SerializedObject(uiManager);
            uiSO.FindProperty("hudPanel").objectReferenceValue      = hudPanel;
            uiSO.FindProperty("shopPanel").objectReferenceValue     = shopPanel;
            uiSO.FindProperty("pausePanel").objectReferenceValue    = pausePanel;
            uiSO.FindProperty("gameOverPanel").objectReferenceValue = goPanel;
            uiSO.FindProperty("victoryPanel").objectReferenceValue  = vicPanel;
            uiSO.ApplyModifiedProperties();

            // 4. Serializar no HUDController
            var hudSO = new SerializedObject(hudCtrl);
            hudSO.FindProperty("healthSlider").objectReferenceValue     = hpSlider;
            hudSO.FindProperty("healthFill").objectReferenceValue       = hpFillImage;
            hudSO.FindProperty("healthText").objectReferenceValue       = hpText;
            hudSO.FindProperty("ammoText").objectReferenceValue         = ammoText;
            hudSO.FindProperty("waveText").objectReferenceValue         = waveText;
            hudSO.FindProperty("scoreText").objectReferenceValue        = scoreText;
            hudSO.FindProperty("dodgeCooldownFill").objectReferenceValue = dodgeFill;
            hudSO.FindProperty("damageVignette").objectReferenceValue   = dmgImg;
            hudSO.FindProperty("reticle").objectReferenceValue          = reticleRT;
            hudSO.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[HUDBuilder] Canvas_HUD e UIManager construídos e associados com sucesso!");
        }

        private static GameObject GetOrCreateChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null)
            {
                var rt = child.GetComponent<RectTransform>();
                if (rt == null)
                    child.gameObject.AddComponent<RectTransform>();
                return child.gameObject;
            }

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void SetFullStretch(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}

