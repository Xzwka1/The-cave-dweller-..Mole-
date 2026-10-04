using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CaveDweller.Core
{
    public class WinningScreen : MonoBehaviour
    {
        [Header("Scene Navigation")]
        [SerializeField] private string mainMenuSceneName = "Main_Menu";
        [SerializeField] private string firstMapSceneName = "First_MAP";

        private void Start()
        {
            SoundManager.Instance?.PlayVictoryMusic();
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
                {
                    BackToMainMenu();
                }
                else if (kb.rKey.wasPressedThisFrame)
                {
                    PlayAgain();
                }
            }
#else
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                BackToMainMenu();
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                PlayAgain();
            }
#endif
        }

        public void BackToMainMenu()
        {
            SoundManager.Instance?.PlayButtonClickSFX();
            Debug.Log("[WinningScreen] Returning to Main Menu...");
            SceneManager.LoadScene(mainMenuSceneName);
        }

        public void PlayAgain()
        {
            SoundManager.Instance?.PlayButtonClickSFX();
            Debug.Log("[WinningScreen] Starting game again from First_MAP...");
            SceneManager.LoadScene(firstMapSceneName);
        }
    }
}
