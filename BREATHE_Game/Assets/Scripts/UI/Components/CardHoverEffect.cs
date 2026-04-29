using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Breathe.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class CardHoverEffect : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        private Vector3 _baseScale;
        private RectTransform _rt;
        private bool _interactable = true;
        private bool _hovering;

        public bool IsHovering => _hovering;

        // Keep subtle so cards near panel edges do not clip when scaled.
        private const float HoverScale = 1.03f;
        private const float PressScale = 1.05f;
        private const float AnimSpeed = 12f;
        private const float FadeSpeed = 6f;

        private float _targetScale = 1f;

        private TextMeshProUGUI _hoverLabel;
        private float _hoverLabelAlpha;
        private float _hoverLabelTarget;
        private Color _hoverLabelBaseColor;

        bool _pointerDown;

        /// <summary>
        /// When true (set by <see cref="ConfigureFillHoverFromButtonIfAny"/> via <see cref="MenuUiChrome.AttachStandardButtonHover"/>):
        /// drive <see cref="Button"/> fill <see cref="Image.color"/> from <see cref="Selectable.colors"/> (Selectable ColorTint fights scale-on-root setups).
        /// </summary>
        bool _useFillTintFromButton;

        Button _btn;
        Image _fillImg;
        Color _normC;
        Color _highlightC;
        Color _pressedC;
        Color _disabledC;

        public void SetInteractable(bool interactable)
        {
            _interactable = interactable;
            if (!interactable)
            {
                _hovering = false;
                _targetScale = 1f;
            }
            ApplyFillTintForState();
        }

        /// <summary>Turn off hover tint layer (level-select cards: layered overlays only).</summary>
        public void DisableFillTintFromButton()
        {
            _useFillTintFromButton = false;
            _btn = null;
            _fillImg = null;
        }

        /// <summary>Call after button colors are styled (e.g. <see cref="MenuUiChrome.StyleButtonLikeSettings"/>).</summary>
        public void ConfigureFillHoverFromButtonIfAny()
        {
            _btn = GetComponent<Button>();
            _fillImg = _btn != null ? _btn.targetGraphic as Image : null;
            if (_btn == null || _fillImg == null)
            {
                _useFillTintFromButton = false;
                return;
            }
            _useFillTintFromButton = true;
            _btn.transition = Selectable.Transition.None;
            CacheSelectableColors();
            ApplyFillTintForState();
        }

        void CacheSelectableColors()
        {
            var c = _btn.colors;
            _normC = c.normalColor;
            _highlightC = c.highlightedColor;
            _pressedC = c.pressedColor;
            _disabledC = c.disabledColor;
        }

        void ApplyFillTintForState()
        {
            if (!_useFillTintFromButton || _fillImg == null || _btn == null)
                return;
            if (!_btn.interactable)
            {
                _fillImg.color = _disabledC;
                return;
            }

            if (_pointerDown)
                _fillImg.color = _pressedC;
            else if (_hovering)
                _fillImg.color = _highlightC;
            else
                _fillImg.color = _normC;
        }

        public void RefreshTintFromSelectableColors()
        {
            if (!_useFillTintFromButton || _btn == null || _fillImg == null)
                return;
            CacheSelectableColors();
            ApplyFillTintForState();
        }

        public void SetHoverLabel(TextMeshProUGUI label)
        {
            _hoverLabel = label;
            if (label != null)
            {
                _hoverLabelBaseColor = label.color;
                _hoverLabelAlpha = 0f;
                _hoverLabelTarget = 0f;
                label.color = new Color(_hoverLabelBaseColor.r, _hoverLabelBaseColor.g,
                    _hoverLabelBaseColor.b, 0f);
            }
        }

        void Awake()
        {
            _rt = GetComponent<RectTransform>();
            _baseScale = _rt.localScale;
        }

        void Update()
        {
            float current = _rt.localScale.x / _baseScale.x;
            float next = Mathf.Lerp(current, _targetScale, Time.unscaledDeltaTime * AnimSpeed);
            _rt.localScale = _baseScale * next;

            if (_hoverLabel != null)
            {
                _hoverLabelAlpha = Mathf.Lerp(_hoverLabelAlpha, _hoverLabelTarget,
                    Time.unscaledDeltaTime * FadeSpeed);
                _hoverLabel.color = new Color(_hoverLabelBaseColor.r, _hoverLabelBaseColor.g,
                    _hoverLabelBaseColor.b, _hoverLabelAlpha);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_interactable) return;
            _hovering = true;
            _targetScale = HoverScale;
            _hoverLabelTarget = 1f;
            ApplyFillTintForState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovering = false;
            _targetScale = 1f;
            _hoverLabelTarget = 0f;
            ApplyFillTintForState();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_interactable) return;
            _pointerDown = true;
            _targetScale = PressScale;
            ApplyFillTintForState();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pointerDown = false;
            _targetScale = _hovering ? HoverScale : 1f;
            ApplyFillTintForState();
        }
    }
}
