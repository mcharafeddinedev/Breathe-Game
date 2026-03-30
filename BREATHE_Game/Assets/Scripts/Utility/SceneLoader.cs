using UnityEngine;
using UnityEngine.SceneManagement;

namespace Breathe.Utility
{
    // Centralized scene transitions. All scene loads go through here so
    // the main-menu scene name and transition logic live in one place.
    public static class SceneLoader
    {
        public const string MainMenuScene = "MainMenu";

        public static void LoadMainMenu()
        {
            Debug.Log("[SceneLoader] Loading Main Menu...");
            SceneManager.LoadScene(MainMenuScene);
        }

        public static void LoadMinigame(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[SceneLoader] Cannot load minigame — scene name is null or empty.");
                return;
            }

            if (!IsSceneInBuildSettings(sceneName))
            {
                Debug.LogWarning($"[SceneLoader] Scene \"{sceneName}\" is not in Build Settings. Add it when the scene is created.");
                return;
            }

            Debug.Log($"[SceneLoader] Loading minigame scene: {sceneName}");
            SceneManager.LoadScene(sceneName);
        }

        public static void ReloadCurrentScene()
        {
            string current = SceneManager.GetActiveScene().name;
            Debug.Log($"[SceneLoader] Reloading scene: {current}");
            SceneManager.LoadScene(current);
        }

        public static bool IsSceneInBuildSettings(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (name == sceneName) return true;
            }
            return false;
        }
    }
}
