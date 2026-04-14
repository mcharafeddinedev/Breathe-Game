using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Breathe.Audio
{
    /// <summary>
    /// Menu/settings sliders: menu blip on grab, then repeats while the pointer is held so dragging is audible.
    /// </summary>
    public sealed class MenuSlider : Slider
    {
        [SerializeField, Tooltip("Seconds after pointer down before repeat blips begin (quick taps stay a single blip).")]
        float _delayBeforeRepeat = 0.2f;
        [SerializeField, Tooltip("Seconds between blips while held/dragging.")]
        float _repeatInterval = 0.14f;

        Coroutine _repeat;
        bool _held;

        protected override void OnDisable()
        {
            StopRepeat();
            base.OnDisable();
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            SfxPlayer.Instance?.PlayUiMenuClick();
            _held = true;
            if (_repeat != null)
                StopCoroutine(_repeat);
            _repeat = StartCoroutine(RepeatWhileHeld());
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            StopRepeat();
            base.OnPointerUp(eventData);
        }

        void StopRepeat()
        {
            _held = false;
            if (_repeat != null)
            {
                StopCoroutine(_repeat);
                _repeat = null;
            }
        }

        IEnumerator RepeatWhileHeld()
        {
            yield return new WaitForSecondsRealtime(_delayBeforeRepeat);
            var wait = new WaitForSecondsRealtime(_repeatInterval);
            while (_held)
            {
                SfxPlayer.Instance?.PlayUiMenuClick();
                yield return wait;
            }
        }
    }
}
