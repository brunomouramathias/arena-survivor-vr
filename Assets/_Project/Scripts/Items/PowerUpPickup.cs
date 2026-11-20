using System.Collections.Generic;
using UnityEngine;
using ArenaSurvivor.Core;
using ArenaSurvivor.Player;

namespace ArenaSurvivor.Items
{
    public enum PowerUpKind { Speed, Weapon, FireRate, Damage, Health }

    // Ícone de poder que "brota" quando o último inimigo da onda morre.
    // Fica girando/brilhando no chão, é atraído pelo jogador e aplica uma evolução.
    public class PowerUpPickup : MonoBehaviour
    {
        // Quantos poderes estão no chão agora (o WaveManager espera serem coletados)
        public static int Active { get; private set; }

        // Sequência de evolução: 1ª limpeza = velocidade, 2ª = arma mais forte,
        // 3ª = cadência, 4ª = dano, 5ª = vida, e depois repete mais forte.
        static readonly PowerUpKind[] Order =
        {
            PowerUpKind.Speed,
            PowerUpKind.Weapon,
            PowerUpKind.FireRate,
            PowerUpKind.Damage,
            PowerUpKind.Health
        };

        static int nextIndex;

        [SerializeField] private float magnetRadius = 3.2f;
        [SerializeField] private float collectRadius = 1.4f;
        [SerializeField] private float lifetime      = 30f;
        [SerializeField] private float rotateSpeed   = 120f;
        [SerializeField] private float bobHeight     = 0.18f;
        [SerializeField] private float bobSpeed      = 2.2f;

        private PowerUpKind kind;
        private float       amount;
        private string      label;
        private Color       color;

        private Transform  visual;
        private Transform  player;
        private Vector3    startPos;
        private float      elapsed;
        private bool       collected;

        // ── API ──────────────────────────────────────────────────────────────
        public PowerUpKind Kind => kind;
        public string Label => label;
        public bool Collected => collected;
        public bool IsBuiltIcon => visual != null;

        // Cria o próximo poder da sequência na posição indicada
        public static PowerUpPickup SpawnNext(Vector3 position)
        {
            var kind = Order[nextIndex % Order.Length];
            int cycle = nextIndex / Order.Length;
            nextIndex++;
            return Spawn(kind, cycle, position);
        }

        // Cria um poder específico (usado também por testes)
        public static PowerUpPickup Spawn(PowerUpKind kind, int cycle, Vector3 position)
        {
            var go = new GameObject("PowerUp_" + kind);
            go.transform.position = position + Vector3.up * 1.1f;

            var pu = go.AddComponent<PowerUpPickup>();
            pu.Setup(kind, cycle);
            return pu;
        }

        public static void ResetProgression()
        {
            nextIndex = 0;
            Active = 0;
        }

        // ── Construção visual (procedural, sem precisar de prefab) ───────────
        private void Setup(PowerUpKind newKind, int cycle)
        {
            kind = newKind;

            switch (kind)
            {
                case PowerUpKind.Speed:
                    amount = 0.25f;
                    color  = new Color(0.25f, 0.85f, 1f);
                    label  = $"+{Mathf.RoundToInt(amount * 100)}% VELOCIDADE";
                    break;
                case PowerUpKind.Weapon:
                    amount = 0.35f;
                    color  = new Color(1f, 0.55f, 0.15f);
                    label  = "ARMA MAIS FORTE";
                    break;
                case PowerUpKind.FireRate:
                    amount = 0.20f;
                    color  = new Color(1f, 0.9f, 0.2f);
                    label  = $"+{Mathf.RoundToInt(amount * 100)}% CADÊNCIA";
                    break;
                case PowerUpKind.Damage:
                    amount = 0.30f;
                    color  = new Color(1f, 0.3f, 0.55f);
                    label  = $"+{Mathf.RoundToInt(amount * 100)}% DANO";
                    break;
                default:
                    amount = 1f;
                    color  = new Color(0.35f, 1f, 0.45f);
                    label  = "VIDA CHEIA";
                    break;
            }

            // ciclos seguintes deixam o poder mais forte
            if (kind != PowerUpKind.Health) amount += 0.10f * cycle;

            BuildVisual();
            Active++;
        }

        private void BuildVisual()
        {
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) unlit = Shader.Find("Unlit/Color");

            visual = new GameObject("Icon").transform;
            visual.SetParent(transform, false);

            // Núcleo (octaedro improvisado: cubo girado)
            var core = GameObject.CreatePrimitive(PrimitiveType.Cube);
            core.name = "Core";
            Destroy(core.GetComponent<Collider>());
            core.transform.SetParent(visual, false);
            core.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            core.transform.localScale = Vector3.one * 0.42f;
            var coreMat = new Material(unlit) { name = "PowerCore" };
            coreMat.SetColor("_BaseColor", color);
            core.GetComponent<Renderer>().sharedMaterial = coreMat;

            // Brilho em cruz (dá leitura de "poder" mesmo de longe)
            var shineMat = new Material(unlit) { name = "PowerShine" };
            shineMat.SetColor("_BaseColor", Color.Lerp(color, Color.white, 0.7f));

            var v = GameObject.CreatePrimitive(PrimitiveType.Cube);
            v.name = "ShineV";
            Destroy(v.GetComponent<Collider>());
            v.transform.SetParent(visual, false);
            v.transform.localScale = new Vector3(0.055f, 0.85f, 0.055f);
            v.GetComponent<Renderer>().sharedMaterial = shineMat;

            var h = GameObject.CreatePrimitive(PrimitiveType.Cube);
            h.name = "ShineH";
            Destroy(h.GetComponent<Collider>());
            h.transform.SetParent(visual, false);
            h.transform.localScale = new Vector3(0.85f, 0.055f, 0.055f);
            h.GetComponent<Renderer>().sharedMaterial = shineMat;

            // Anel no chão
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Ring";
            Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = new Vector3(0f, -0.95f, 0f);
            ring.transform.localScale = new Vector3(1.5f, 0.015f, 1.5f);
            var ringMat = new Material(unlit) { name = "PowerRing" };
            ringMat.SetColor("_BaseColor", color);
            ring.GetComponent<Renderer>().sharedMaterial = ringMat;

            // Luz
            var lightGO = new GameObject("Light");
            lightGO.transform.SetParent(transform, false);
            var l = lightGO.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = 5f;
            l.intensity = 2.2f;

            PoolService.Instance?.ShowFloatingText(transform.position + Vector3.up * 0.6f, label, color);
        }

        private void Start()
        {
            startPos = transform.position;
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null) player = playerGO.transform;
        }

        private void OnDestroy()
        {
            if (!collected) Active = Mathf.Max(0, Active - 1);
        }

        private void Update()
        {
            if (collected) return;

            elapsed += Time.deltaTime;

            if (visual != null)
            {
                visual.localPosition = Vector3.up * (Mathf.Sin(elapsed * bobSpeed) * bobHeight);
                visual.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
            }

            if (player == null)
            {
                var playerGO = GameObject.FindGameObjectWithTag("Player");
                if (playerGO != null) player = playerGO.transform;
                if (player == null) return;
            }

            Vector3 target = player.position + Vector3.up * 0.9f;
            float dist = Vector3.Distance(transform.position, target);

            if (dist < magnetRadius)
                transform.position = Vector3.MoveTowards(transform.position, target, 9f * Time.deltaTime);

            if (dist < collectRadius) Collect();

            if (elapsed > lifetime) { collected = true; Destroy(gameObject); }
        }

        public void Collect()
        {
            if (collected) return;
            collected = true;
            Active = Mathf.Max(0, Active - 1);

            Apply(FindAnyObjectByType<PlayerStats>(), FindAnyObjectByType<PlayerShooter>(), FindAnyObjectByType<PlayerHealth>());

            PoolService.Instance?.ShowFloatingText(
                transform.position + Vector3.up * 0.8f, label, color);

            Destroy(gameObject);
        }

        // Aplica o efeito (público para testes)
        public void Apply(PlayerStats stats, PlayerShooter shooter, PlayerHealth health)
        {
            switch (kind)
            {
                case PowerUpKind.Speed:
                    stats?.UpgradeSpeed(amount);
                    break;

                case PowerUpKind.Weapon:
                    // libera a próxima arma; se todas já estão liberadas, vira +dano
                    if (shooter == null || !shooter.UnlockAndEquipNext())
                        stats?.UpgradeDamagePercent(amount);
                    break;

                case PowerUpKind.FireRate:
                    stats?.UpgradeFireRate(amount);
                    break;

                case PowerUpKind.Damage:
                    stats?.UpgradeDamagePercent(amount);
                    break;

                case PowerUpKind.Health:
                    health?.Heal(9999);
                    break;
            }
        }
    }
}
