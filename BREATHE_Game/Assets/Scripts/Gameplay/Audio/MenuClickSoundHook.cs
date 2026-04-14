using UnityEngine;
using UnityEngine.UI;

namespace Breathe.Audio
{
    /// <summary>
    /// One per Button — plays the shared menu click sound. Does not require SfxPlayer to exist when hooks are added.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class MenuClickSoundHook : MonoBehaviour
    {
        /// <summary>
        /// Adds click hooks under <paramref name="root"/> (idempotent). Safe to call before SfxPlayer awakens.
        /// </summary>
        public static void RegisterHierarchy(Transform root)
        {
            if (root == null) return;
            foreach (var b in root.GetComponentsInChildren<Button>(true))
            {
                if (b == null || b.GetComponent<MenuClickSoundHook>() != null) continue;
                b.gameObject.AddComponent<MenuClickSoundHook>();
            }
        }

        /// <summary>Registers every <see cref="Button"/> in the scene (e.g. after load). Skips already hooked.</summary>
        public static void RegisterAllButtonsInScene()
        {
            var buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var b in buttons)
            {
                if (b == null || b.GetComponent<MenuClickSoundHook>() != null) continue;
                b.gameObject.AddComponent<MenuClickSoundHook>();
            }
        }

        private void Awake()
        {
            var btn = GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(OnButtonClicked);
        }

        private static void OnButtonClicked()
        {
            SfxPlayer.Instance?.PlayUiMenuClick();
        }
    }
}
