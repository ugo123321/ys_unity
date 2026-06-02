using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rzz.UI
{
    // Centralized scene-flow controller. Phase 2 entry points for Main -> Battle -> back-to-Main.
    // Future Phase 2.4: route stage clear / fail / pause through here.
    public static class GameFlowController
    {
        public static string MainSceneName = "Main";
        public static string BattleSceneName = "Battle";

        public static void LoadBattle(string sceneName = null)
        {
            SceneManager.LoadScene(sceneName ?? BattleSceneName, LoadSceneMode.Single);
        }

        public static void LoadMain()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(MainSceneName, LoadSceneMode.Single);
        }

        public static void RestartCurrentScene()
        {
            Time.timeScale = 1f;
            var s = SceneManager.GetActiveScene();
            SceneManager.LoadScene(s.name, LoadSceneMode.Single);
        }
    }
}
