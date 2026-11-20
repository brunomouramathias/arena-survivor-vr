using UnityEngine;

namespace ArenaSurvivor.Core
{
    // Gerencia a iluminação da cena para evitar flickering indesejado
    public class LightManager : MonoBehaviour
    {
        public static LightManager Instance { get; private set; }

        [Header("Configurações de Luz")]
        [SerializeField] private Light mainDirectionalLight;
        [SerializeField] private bool enableTimeOfDay = false;
        [SerializeField] private float dayCycleDuration = 600f; // 10 minutos para ciclo completo
        [SerializeField] private float minIntensity = 0.5f;
        [SerializeField] private float maxIntensity = 1.5f;
        [SerializeField] private Color dayColor = Color.white;
        [SerializeField] private Color nightColor = new Color(0.3f, 0.3f, 0.5f);

        [Header("Luzes Adicionais")]
        [SerializeField] private Light[] additionalLights;
        [SerializeField] private bool stabilizeAdditionalLights = true;

        private float timeOfDay = 0.25f; // Começa ao meio-dia
        private bool isInitialized = false;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (mainDirectionalLight == null)
                mainDirectionalLight = FindAnyObjectByType<Light>();

            // Desabilita scripts de iluminação que possam causar flickering
            DisableConflictingLightScripts();
        }

        private void Start()
        {
            StabilizeLights();
            isInitialized = true;
        }

        private void Update()
        {
            if (!isInitialized) return;

            if (enableTimeOfDay)
            {
                UpdateTimeOfDay();
            }
        }

        private void DisableConflictingLightScripts()
        {
            // Procura e desabilita componentes que podem causar flickering
            var lightProbes = FindObjectsByType<LightProbeGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var probe in lightProbes)
            {
                // LightProbeGroups podem causar flickering se atualizados frequentemente
            }

            var reflectionProbes = FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var probe in reflectionProbes)
            {
                if (probe.mode == UnityEngine.Rendering.ReflectionProbeMode.Realtime)
                {
                    probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked;
                }
            }
        }

        private void StabilizeLights()
        {
            if (mainDirectionalLight != null)
            {
                // Configura a luz direcional principal para ser estável
                mainDirectionalLight.shadows = LightShadows.Soft;
                mainDirectionalLight.shadowStrength = 1f;
                mainDirectionalLight.shadowBias = 0.05f;
                mainDirectionalLight.shadowNormalBias = 0.4f;
                mainDirectionalLight.shadowNearPlane = 0.1f;
            }

            if (stabilizeAdditionalLights && additionalLights != null)
            {
                foreach (var light in additionalLights)
                {
                    if (light != null)
                    {
                        light.shadows = LightShadows.None; // Luzes adicionais sem sombras para performance
                    }
                }
            }
        }

        private void UpdateTimeOfDay()
        {
            if (mainDirectionalLight == null) return;

            timeOfDay += Time.deltaTime / dayCycleDuration;
            if (timeOfDay >= 1f) timeOfDay -= 1f;

            // Calcula intensidade e cor baseada na hora do dia
            float sunAngle = timeOfDay * 360f - 90f; // 0 = nascer do sol, 90 = meio-dia
            float intensity = Mathf.Lerp(minIntensity, maxIntensity, Mathf.Max(0f, Mathf.Sin(sunAngle * Mathf.Deg2Rad)));
            Color lightColor = Color.Lerp(nightColor, dayColor, Mathf.Max(0f, Mathf.Sin(sunAngle * Mathf.Deg2Rad)));

            mainDirectionalLight.intensity = intensity;
            mainDirectionalLight.color = lightColor;

            // Rotaciona o sol
            mainDirectionalLight.transform.rotation = Quaternion.Euler(sunAngle, 170f, 0f);
        }

        public void SetLightStable(bool stable)
        {
            if (mainDirectionalLight != null)
            {
                if (stable)
                {
                    mainDirectionalLight.intensity = maxIntensity;
                    mainDirectionalLight.color = dayColor;
                }
            }
        }

        public void ForceStabilize()
        {
            StabilizeLights();
        }
    }
}