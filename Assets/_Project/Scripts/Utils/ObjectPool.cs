using System.Collections.Generic;
using UnityEngine;

namespace ArenaSurvivor.Utils
{
    // Pool genérico de objetos para evitar Instantiate/Destroy em combate
    public class ObjectPool<T> where T : Component
    {
        private readonly T prefab;
        private readonly Transform parent;
        private readonly Queue<T> pool = new();

        public ObjectPool(T prefab, int initialSize, Transform parent = null)
        {
            this.prefab  = prefab;
            this.parent  = parent;

            // Pré-aquecer o pool
            for (int i = 0; i < initialSize; i++)
            {
                var obj = Object.Instantiate(prefab, parent);
                obj.gameObject.SetActive(false);
                pool.Enqueue(obj);
            }
        }

        public T Get(Vector3 position, Quaternion rotation)
        {
            T obj;
            if (pool.Count > 0)
            {
                obj = pool.Dequeue();
            }
            else
            {
                obj = Object.Instantiate(prefab, parent);
            }

            obj.transform.SetPositionAndRotation(position, rotation);
            obj.gameObject.SetActive(true);
            return obj;
        }

        public void Return(T obj)
        {
            obj.gameObject.SetActive(false);
            pool.Enqueue(obj);
        }
    }
}
