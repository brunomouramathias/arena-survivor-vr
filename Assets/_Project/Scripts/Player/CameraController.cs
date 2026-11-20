using System.Collections;
using UnityEngine;

namespace ArenaSurvivor.Player
{
    // Câmera que segue o jogador com inclinação fixa (top-down inclinada) e tremor
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

        private Vector3 velocity = Vector3.zero;
        private float   shakeMagnitude;

        private void LateUpdate()
        {
            if (target == null) return;

            // Posição desejada: acima e atrás do jogador
            Vector3 offset  = Quaternion.Euler(tilt, 0f, 0f) * Vector3.back * height;
            Vector3 desired = target.position + offset;

            // Aplica tremor
            if (shakeMagnitude > 0.01f)
            {
                desired += Random.insideUnitSphere * shakeMagnitude;
                shakeMagnitude = Mathf.MoveTowards(shakeMagnitude, 0f, shakeDecay * Time.deltaTime);
            }

            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
            transform.LookAt(target.position);
        }

        public void Shake(float magnitude)
        {
            shakeMagnitude = Mathf.Max(shakeMagnitude, magnitude);
        }

        public void SetTarget(Transform t) => target = t;
    }
}
