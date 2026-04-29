using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Breathe.Audio
{
    /// <summary>
    /// One per Button — hover uses <see cref="SfxLibrary.UiButtonHover"/> (UI hover); click uses confirm (menu click).
    /// Added by <see cref="SfxPlayer.RegisterMenuClickSoundForHierarchy"/> / scene sweep. Requires an active EventSystem + raycast graphic.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class MenuClickSoundHook : MonoBehaviour, IPointerEnterHandler
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

        public void OnPointerEnter(PointerEventData eventData)
        {
            var btn = GetComponent<Button>();
            if (btn == null || !btn.interactable || !btn.IsActive())
                return;
            SfxPlayer.Instance?.PlayUiHover();
        }

        private static void OnButtonClicked()
        {
            SfxPlayer.Instance?.PlayUiMenuClick();
        }
    }
}
