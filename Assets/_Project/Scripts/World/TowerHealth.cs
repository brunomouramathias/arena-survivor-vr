using System;
using System.Collections.Generic;
using UnityEngine;
using ArenaSurvivor.Core;

namespace ArenaSurvivor.World
{
    // Torre defensiva: tem vida, recebe dano dos inimigos, mostra barra e pode ser destruída.
    public class TowerHealth : MonoBehaviour
    {
        public static readonly List<TowerHealth> All = new List<TowerHealth>();

        [Header("Vida")]
        [SerializeField] private int maxHealth = 500;
        [SerializeField] private GameObject destroyEffectPrefab;

        private int current;
        private bool destroyed;

        private Renderer[] renderers;
        private Collider[] colliders;
        private MaterialPropertyBlock mpb;
        private Transform barRoot;
        private Transform barFill;
        private Material fillMat;
        private float flashTimer;
        private Color baseColor = new Color(0.25f, 0.95f, 0.35f);

        public bool  Destroyed => destroyed;
        public float Normalized => maxHealth > 0 ? Mathf.Clamp01((float)current / maxHealth) : 0f;
        public int   Current => current;
        public int   Max => maxHealth;
        public event Action<TowerHealth> DestroyedEvent;

        private void Awake()
        {
            current = maxHealth;
            if (!All.Contains(this)) All.Add(this);
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            mpb = new MaterialPropertyBlock();
            BuildHealthBar();
        }

        private void OnDestroy()
        {
            All.Remove(this);
            if (fillMat != null) Destroy(fillMat);
        }

        public void Configure(int hp, GameObject destroyFx)
        {
            maxHealth = Mathf.Max(1, hp);
            current = maxHealth;
            destroyEffectPrefab = destroyFx;
            UpdateBar();
        }

        public void TakeDamage(int amount)
        {
            if (destroyed || amount <= 0) return;
            current = Mathf.Max(0, current - amount);
            flashTimer = 0.12f;
            UpdateBar();

            PoolService.Instance?.ShowFloatingText(
                transform.position + Vector3.up * (GetHeight() * 0.75f),
                "-" + amount,
                new Color(1f, 0.45f, 0.2f));

            if (current <= 0) DestroyTower();
        }

        private void Update()
        {
            if (flashTimer > 0f)
            {
                flashTimer -= Time.unscaledDeltaTime;
                if (flashTimer <= 0f) ClearTint();
                else SetTint(new Color(1f, 0.55f, 0.45f));
            }
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (barRoot != null && cam != null)
                barRoot.rotation = cam.transform.rotation;
        }

        private void DestroyTower()
        {
            if (destroyed) return;
            destroyed = true;

            if (destroyEffectPrefab != null)
            {
                var fx = Instantiate(destroyEffectPrefab, transform.position + Vector3.up * 1f, Quaternion.identity);
                Destroy(fx, 4f);
            }

            foreach (var r in renderers) if (r != null) r.enabled = false;
            foreach (var c in colliders) if (c != null) c.enabled = false;

            if (barRoot != null) barRoot.gameObject.SetActive(false);

            PoolService.Instance?.ShowFloatingText(
                transform.position + Vector3.up * 2f, "TORRE DESTRUÍDA", new Color(1f, 0.25f, 0.2f));

            DestroyedEvent?.Invoke(this);

            // Derrota se todas as torres caíram
            bool anyAlive = false;
            foreach (var t in All) if (t != null && !t.Destroyed) { anyAlive = true; break; }
            if (!anyAlive)
            {
                Debug.LogWarning("[TORRES] Todas as torres foram destruídas — fim de jogo.");
                GameManager.Instance?.SetState(GameState.GameOver);
            }
        }

        private void SetTint(Color c)
        {
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", c);
                r.SetPropertyBlock(block);
            }
        }

        private void ClearTint()
        {
            foreach (var r in renderers)
                if (r != null) r.SetPropertyBlock(null);
        }

        private float GetHeight()
        {
            if (renderers == null || renderers.Length == 0) return 4f;
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return Mathf.Max(2f, b.size.y);
        }

        private void BuildHealthBar()
        {
            float h = GetHeight();
            barRoot = new GameObject("TowerHealthBar").transform;
            barRoot.SetParent(transform, false);

            // As torres vêm de prefab com escala 100x: compensa para a barra ficar em metros
            Vector3 ls = transform.lossyScale;
            float sx = Mathf.Max(0.001f, ls.x), sy = Mathf.Max(0.001f, ls.y), sz = Mathf.Max(0.001f, ls.z);
            barRoot.localScale = new Vector3(1f / sx, 1f / sy, 1f / sz);
            barRoot.localPosition = new Vector3(0f, (h + 0.7f) / sy, 0f);

            var unlit = Shader.Find("Universal Render Pipeline/Unlit");

            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "BG";
            Destroy(bg.GetComponent<Collider>());
            bg.transform.SetParent(barRoot, false);
            bg.transform.localScale = new Vector3(2.4f, 0.34f, 1f);
            var bgMat = new Material(unlit) { name = "TowerBarBG" };
            bgMat.SetColor("_BaseColor", new Color(0.05f, 0.05f, 0.07f, 1f));
            bg.GetComponent<Renderer>().sharedMaterial = bgMat;

            var fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fill.name = "Fill";
            Destroy(fill.GetComponent<Collider>());
            fill.transform.SetParent(barRoot, false);
            fill.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            fill.transform.localScale = new Vector3(2.2f, 0.24f, 1f);
            fillMat = new Material(unlit) { name = "TowerBarFill" };
            fillMat.SetColor("_BaseColor", baseColor);
            fill.GetComponent<Renderer>().sharedMaterial = fillMat;
            barFill = fill.transform;

            UpdateBar();
        }

        private void UpdateBar()
        {
            if (barFill == null) return;
            float n = Normalized;
            barFill.localScale = new Vector3(2.2f * n, 0.24f, 1f);
            barFill.localPosition = new Vector3(-2.2f * (1f - n) * 0.5f, 0f, -0.01f);
            if (fillMat != null)
                fillMat.SetColor("_BaseColor", Color.Lerp(new Color(0.9f, 0.2f, 0.15f), baseColor, n));
        }
    }
}
