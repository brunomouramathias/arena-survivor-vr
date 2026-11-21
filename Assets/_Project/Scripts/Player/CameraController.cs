using System.Collections;
using UnityEngine;

namespace ArenaSurvivor.Player
{
    // Câmera que segue o jogador com inclinação fixa (top-down inclinada) e tremor.
    // O tremor NUNCA entra na mira: a pose "limpa" (sem tremor) fica separada e é ela que
    // gera o raio do mouse — senão cada tiro mudava de direção junto com o tremor.
    public class CameraController : MonoBehaviour
    {
        [Header("Alvo")]
        [SerializeField] private Transform target;

        [Header("Configuração da câmera")]
        [SerializeField] private float height   = 11.0f;
        [SerializeField] private float tilt     = 52f;    // Ângulo de inclinação
        [SerializeField] private float smoothTime = 0.1f;

        [Header("Tremor (shake)")]
        [SerializeField] private float shakeDecay = 5f;

        private Vector3 velocity      = Vector3.zero;
        private Vector3 cleanPosition;
        private bool    hasCleanPosition;
        private float   shakeMagnitude;

        // Pose sem tremor: usada pela mira (PlayerController) e pelos testes
        public Vector3    StablePosition { get; private set; }
        public Quaternion StableRotation { get; private set; }

        private void Awake()
        {
            cleanPosition    = transform.position;
            hasCleanPosition = true;
            StablePosition   = transform.position;
            StableRotation   = transform.rotation;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // Posição desejada: acima e atrás do jogador
            Vector3 offset  = Quaternion.Euler(tilt, 0f, 0f) * Vector3.back * height;
            Vector3 desired = target.position + offset;

            // Suavização só na pose limpa (o tremor não pode "contaminar" o movimento)
            if (!hasCleanPosition) { cleanPosition = transform.position; hasCleanPosition = true; }
            cleanPosition = Vector3.SmoothDamp(cleanPosition, desired, ref velocity, smoothTime);

            Vector3 lookDir = target.position - cleanPosition;
            StablePosition  = cleanPosition;
            StableRotation  = lookDir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(lookDir) : transform.rotation;

            // Tremor aplicado apenas na câmera de verdade
            Vector3 shaken = cleanPosition;
            if (shakeMagnitude > 0.01f)
            {
                shaken += Random.insideUnitSphere * shakeMagnitude;
                shakeMagnitude = Mathf.MoveTowards(shakeMagnitude, 0f, shakeDecay * Time.deltaTime);
            }

            transform.SetPositionAndRotation(shaken, StableRotation);
        }

        public void Shake(float magnitude)
        {
            shakeMagnitude = Mathf.Max(shakeMagnitude, magnitude);
        }

        public void SetTarget(Transform t) => target = t;

        // Raio do mouse calculado na pose SEM tremor: a mira (e o tiro) não treme junto.
        public Ray ScreenRay(Vector2 screenPosition, Camera cam)
        {
            if (cam == null || cam.orthographic)
                return new Ray(StablePosition, transform.forward);

            Vector3 viewport = cam.ScreenToViewportPoint(screenPosition);

            float tanY = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanX = tanY * Mathf.Max(0.0001f, cam.aspect);

            Vector3 dirView  = new Vector3((2f * viewport.x - 1f) * tanX, (2f * viewport.y - 1f) * tanY, 1f);
            Vector3 dirWorld = StableRotation * dirView;

            return new Ray(StablePosition, dirWorld.normalized);
        }
    }
}
