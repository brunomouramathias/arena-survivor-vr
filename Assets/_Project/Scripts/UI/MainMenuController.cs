using UnityEngine;
using ArenaSurvivor.Core;

namespace ArenaSurvivor.UI
{
    // Controla o menu principal
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private GameObject controlsPanel;

        private void Start()
        {
            Cursor.visible   = true;
            Cursor.lockState = CursorLockMode.None;
            if (controlsPanel != null) controlsPanel.SetActive(false);
        }

        public void OnPlayClicked()     => GameManager.Instance?.StartGame();
        public void OnControlsClicked() => controlsPanel?.SetActive(true);
        public void OnCloseControls()   => controlsPanel?.SetActive(false);
        public void OnQuitClicked()     => GameManager.Instance?.QuitGame();
    }
}

