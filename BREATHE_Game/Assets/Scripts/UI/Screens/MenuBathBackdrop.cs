using UnityEngine;
using Breathe.Gameplay;

namespace Breathe.UI
{
    /// <summary>
    /// Same bath + caustics stack as the Bubbles minigame (menu preset: calmer caustics, no ambient floaters).
    /// Place on a scene object or let <see cref="MainMenuController"/> create one at runtime.
    /// </summary>
    public sealed class MenuBathBackdrop : MonoBehaviour
    {
        [SerializeField] private Camera _camera;

        private void Awake()
        {
            var cam = _camera != null ? _camera : Camera.main;
            if (cam != null)
                cam.backgroundColor = new Color(0.06f, 0.20f, 0.30f);
            BathBackdropBuilder.Build(cam, out GameObject root, BathBackdropBuilder.MenuDefault);
            if (root != null)
                root.transform.SetParent(transform, true);
        }
    }
}
