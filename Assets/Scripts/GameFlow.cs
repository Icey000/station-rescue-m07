using UnityEngine;
using UnityEngine.SceneManagement;
namespace UnityAgentLab
{
    public static class GameFlow
    {
        public static bool IsPaused { get; private set; }
        public static void SetPaused(bool paused)
        {
            IsPaused = paused; Time.timeScale = paused ? 0 : 1;
            AudioListener.pause = paused;
            foreach (PlayerMotor motor in Object.FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None))
            { motor.SetMoveInput(Vector2.zero); motor.CancelRequests(); }
            foreach (PlayerInteraction interaction in Object.FindObjectsByType<PlayerInteraction>(FindObjectsSortMode.None))
                interaction.CancelRequests();
        }
        public static void StartGame() { SetPaused(false); SceneManager.LoadScene("Game"); }
        public static void Restart() { StartGame(); }
        public static void Menu() { SetPaused(false); SceneManager.LoadScene("Menu"); }
        public static void Quit()
        {
            SetPaused(false);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
