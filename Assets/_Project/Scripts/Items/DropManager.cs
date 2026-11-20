using UnityEngine;

namespace ArenaSurvivor.Items
{
    // Gerencia o drop de itens pelo cenário
    public class DropManager : MonoBehaviour
    {
        public static DropManager Instance { get; private set; }

        [SerializeField] private GameObject healthPickupPrefab;
        [SerializeField] private GameObject ammoPickupPrefab;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void DropHealth(Vector3 position)
        {
            if (healthPickupPrefab != null)
                Instantiate(healthPickupPrefab, position + Vector3.up * 0.3f, Quaternion.identity);
        }

        public void DropAmmo(Vector3 position)
        {
            if (ammoPickupPrefab != null)
                Instantiate(ammoPickupPrefab, position + Vector3.up * 0.3f, Quaternion.identity);
        }
    }
}

