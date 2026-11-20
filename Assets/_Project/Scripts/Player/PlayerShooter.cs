using UnityEngine;
using ArenaSurvivor.Core;
using ArenaSurvivor.Weapons;

namespace ArenaSurvivor.Player
{
    // Gerencia as armas do jogador: equipar, trocar, atirar, recarregar
    public class PlayerShooter : MonoBehaviour
    {
        [Header("Configuração de armas")]
        [SerializeField] private Weapon[] weaponSlots;   // Slots 1, 2, 3
        [SerializeField] private Transform weaponHolder;

        private int currentSlot = 0;

        public Weapon CurrentWeapon => (weaponSlots != null && weaponSlots.Length > 0)
            ? weaponSlots[currentSlot]
            : null;

        private PlayerController controller;
        private PlayerAnimation  animCtrl;

        private InputService inputService;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            animCtrl   = GetComponent<PlayerAnimation>();
        }

        private void Start()
        {
            inputService = InputService.Instance ?? FindAnyObjectByType<InputService>();
            EquipSlot(0);
        }

        private void Update()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying())
            {
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Arena" && GameManager.Instance.CurrentState == GameState.Menu)
                    GameManager.Instance.SetState(GameState.Playing);
                else
                    return;
            }

            if (inputService == null)
                inputService = InputService.Instance ?? FindAnyObjectByType<InputService>();
            if (inputService == null) return;

            HandleWeaponSwitch();

            if (CurrentWeapon == null) return;

            // Arma completamente sem munição (pente e reserva zerados): volta para a pistola,
            // que tem reserva infinita. Assim o jogador nunca fica sem conseguir atirar.
            if (CurrentWeapon.IsDry && currentSlot != 0)
            {
                FallbackToPistol();
                return;
            }

            // Andando sem atirar: cano acompanha o corpo. Atirando/parado: cano na mira do mouse.
            if (controller != null)
            {
                bool aiming = inputService.FireHeld || !controller.IsMovingNow;
                Vector3 dir = aiming
                    ? controller.AimPoint - CurrentWeapon.transform.position
                    : controller.transform.forward;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                    CurrentWeapon.transform.rotation = Quaternion.LookRotation(dir);
            }

            // Atirar
            if (inputService.FireHeld)
            {
                CurrentWeapon.TryFire(controller != null ? controller.AimPoint : transform.position + transform.forward * 10f);
                animCtrl?.TriggerShoot();
            }

            // Recarregar
            if (inputService.ReloadDown)
                CurrentWeapon.TryReload();
        }

        // Troca para a pistola quando a arma equipada acaba a munição
        private void FallbackToPistol()
        {
            if (!EquipSlot(0)) return;
            PoolService.Instance?.ShowFloatingText(
                transform.position + Vector3.up * 1.8f, "SEM MUNIÇÃO — PISTOLA", new Color(1f, 0.75f, 0.3f));
        }

        private void HandleWeaponSwitch()
        {
            if (inputService == null) return;

            // Teclas 1, 2, 3
            int slotKey = inputService.WeaponSlotDown;
            if (slotKey >= 1 && slotKey <= weaponSlots.Length)
            {
                EquipSlot(slotKey - 1);
                return;
            }

            // Roda do mouse
            float scroll = inputService.WeaponScrollDelta;
            if (scroll > 0.01f)
            {
                int next = (currentSlot + 1) % weaponSlots.Length;
                EquipSlot(next);
            }
            else if (scroll < -0.01f)
            {
                int prev = (currentSlot - 1 + weaponSlots.Length) % weaponSlots.Length;
                EquipSlot(prev);
            }
        }

        public bool EquipSlot(int slot)
        {
            if (slot == currentSlot && CurrentWeapon != null) return true;
            if (slot < 0 || slot >= weaponSlots.Length) return false;
            if (weaponSlots[slot] == null || !weaponSlots[slot].IsUnlocked) return false;

            // Desativa arma atual
            if (weaponSlots[currentSlot] != null)
                weaponSlots[currentSlot].gameObject.SetActive(false);

            currentSlot = slot;
            weaponSlots[currentSlot].gameObject.SetActive(true);

            EventBus.Publish(new WeaponChangedEvent { SlotIndex = slot });
            return true;
        }

        public void UnlockWeapon(int slot)
        {
            if (slot < 0 || slot >= weaponSlots.Length) return;
            weaponSlots[slot]?.Unlock();
        }

        // Poder das ondas: libera e equipa a próxima arma travada.
        // Se não houver nenhuma travada, retorna false (aí o poder vira +dano).
        public bool UnlockAndEquipNext()
        {
            if (weaponSlots == null) return false;

            for (int i = 0; i < weaponSlots.Length; i++)
            {
                if (weaponSlots[i] == null || weaponSlots[i].IsUnlocked) continue;
                weaponSlots[i].Unlock();
                EquipSlot(i);
                return true;
            }
            return false;
        }

        public Weapon GetWeaponInSlot(int slot)
        {
            if (slot < 0 || slot >= weaponSlots.Length) return null;
            return weaponSlots[slot];
        }
    }
}
