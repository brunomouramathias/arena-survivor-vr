using System.Collections;
using TMPro;
using UnityEngine;

namespace ArenaSurvivor.Utils
{
    // Texto flutuante de dano/pontuação — sobe e desaparece
    public class FloatingText : MonoBehaviour
    {
        [SerializeField] private TextMeshPro textMesh;
        [SerializeField] private float riseSpeed = 2f;
        [SerializeField] private float lifetime  = 1f;

        private Camera mainCamera;

        private void Awake()
        {
            if (textMesh == null)
                textMesh = GetComponent<TextMeshPro>();
            mainCamera = Camera.main;
        }

        public void Setup(string text, Color color)
        {
            textMesh.text  = text;
            textMesh.color = color;
            StartCoroutine(AnimateAndReturn());
        }

        private IEnumerator AnimateAndReturn()
        {
            float elapsed = 0f;
            Color startColor = textMesh.color;

            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;

                // Sobe
                transform.position += Vector3.up * riseSpeed * Time.deltaTime;

                // Vira para a câmera
                if (mainCamera != null)
                    transform.forward = mainCamera.transform.forward;

                // Desvanece no final
                float alpha = Mathf.Lerp(1f, 0f, elapsed / lifetime);
                textMesh.color = new Color(startColor.r, startColor.g, startColor.b, alpha);

                yield return null;
            }

            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }
    }
}
