using System.Collections;
using UnityEngine;
using ArenaSurvivor.Player;

namespace ArenaSurvivor.Items
{
    public enum PickupType { Health, Ammo }

    // Item coletável — gira, brilha e é coletado ao jogador se aproximar
    public class Pickup : MonoBehaviour
    {
        [Header("Configuração")]
        [SerializeField] private PickupType type         = PickupType.Health;
        [SerializeField] private int        healAmount   = 25;
        [SerializeField] private int        ammoAmount   = 15;
        [SerializeField] private float      magnetRadius = 4f;
        [SerializeField] private float      magnetSpeed  = 10f;
        [SerializeField] private float      lifetime     = 20f;
        [SerializeField] private float      rotateSpeed  = 90f;
        [SerializeField] private float      bobHeight    = 0.3f;
        [SerializeField] private float      bobSpeed     = 2f;

        private Transform playerTransform;
        private Vector3   startPos;
        private float     elapsed;

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;

            startPos = transform.position;
            StartCoroutine(Expire());
        }

        private void Update()
        {
            elapsed += Time.deltaTime;

            // Bob e rotação
            float bob = Mathf.Sin(elapsed * bobSpeed) * bobHeight;
            transform.position = startPos + Vector3.up * bob;
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);

            // Imã quando jogador está perto
            if (playerTransform != null)
            {
                float dist = Vector3.Distance(transform.position, playerTransform.position);
                if (dist < magnetRadius)
                {
                    transform.position = Vector3.MoveTowards(
                        transform.position,
                        playerTransform.position,
                        magnetSpeed * Time.deltaTime
                    );
                }

                // Coleta ao tocar
                if (dist < 0.8f) Collect();
            }
        }

        private void Collect()
        {
            if (playerTransform == null) return;

            switch (type)
            {
                case PickupType.Health:
                    var hp = playerTransform.GetComponent<PlayerHealth>();
                    hp?.Heal(healAmount);
                    break;

                case PickupType.Ammo:
                    var shooter = playerTransform.GetComponent<PlayerShooter>();
                    var weapon  = shooter?.CurrentWeapon;
                    weapon?.AddAmmo(ammoAmount);
                    break;
            }

            Core.EventBus.Publish(new Core.ItemPickedUpEvent { ItemType = type.ToString() });
            Destroy(gameObject);
        }

        private IEnumerator Expire()
        {
            yield return new WaitForSeconds(lifetime);
            Destroy(gameObject);
        }
    }
}

