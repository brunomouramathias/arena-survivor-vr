using System.Collections.Generic;
using UnityEngine;
using ArenaSurvivor.Utils;

namespace ArenaSurvivor.Core
{
    // Serviço central de pooling — acesso global via singleton
    public class PoolService : MonoBehaviour
    {
        public static PoolService Instance { get; private set; }

        [Header("Prefabs para pools")]
        [SerializeField] private FloatingText floatingTextPrefab;

        // Pools privados criados em runtime
        private ObjectPool<FloatingText> floatingTextPool;

        // Pools adicionais registrados em runtime por outros sistemas
        private readonly Dictionary<string, object> namedPools = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (floatingTextPrefab != null)
                floatingTextPool = new ObjectPool<FloatingText>(floatingTextPrefab, 20, transform);
        }

        public void ShowFloatingText(Vector3 worldPos, string text, Color color)
        {
            if (floatingTextPool == null) return;
            var ft = floatingTextPool.Get(worldPos + Vector3.up * 0.5f, Quaternion.identity);
            ft.Setup(text, color);
        }

        // Permite que outros sistemas registrem seus próprios pools nomeados
        public void RegisterPool<T>(string key, ObjectPool<T> pool) where T : Component
        {
            namedPools[key] = pool;
        }

        public ObjectPool<T> GetPool<T>(string key) where T : Component
        {
            if (namedPools.TryGetValue(key, out var pool))
                return pool as ObjectPool<T>;
            return null;
        }
    }
}
