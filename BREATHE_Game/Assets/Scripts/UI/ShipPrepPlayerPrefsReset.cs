using UnityEngine;
using UnityEngine.SceneManagement;
using Breathe.Audio;
using Breathe.Utility;

namespace Breathe.UI
{
    /// <summary>
    /// Inspector-only workflow for shipping tests: resets <see cref="PlayerPrefs"/>.
    /// Add this to any GameObject in the scene; use the Inspector button added by the Editor script — no in-game UI.
    /// </summary>
    public sealed class ShipPrepPlayerPrefsReset : MonoBehaviour
    {
        [SerializeField, Tooltip("After reset in Play Mode, reload the active scene so DontDestroy singletons/UI pick up cleared prefs. Ignored in Edit Mode.")]
        bool _reloadSceneAfterReset = true;

        /// <summary>Deletes all prefs, restores default audio triple; in Play Mode also refreshes music and optionally reloads the scene.</summary>
        public void ExecuteShipPrep()
        {
            PlayerPrefs.DeleteAll();
            AudioVolumeBootstrap.ReapplyShippingAudioDefaults();

            if (Application.isPlaying)
            {
                SceneMusicDirector.Instance?.RefreshFromMusicSlider();
                Debug.Log("[ShipPrep] PlayerPrefs cleared; shipping audio defaults restored (Master 90% default, slider 0–150%; Music 50%; SFX 75%).");

                if (_reloadSceneAfterReset)
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
            else
            {
                Debug.Log("[ShipPrep] PlayerPrefs cleared on disk; shipping audio defaults written. Enter Play to run the game with a clean state (no scene reload in Edit Mode).");
            }
        }
    }
}
