using TMPro;
using UnityEngine;
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

        private const float HoverScale = 1.06f;
        private const float PressScale = 1.10f;
        private const float AnimSpeed = 12f;
        private const float FadeSpeed = 6f;

        private float _targetScale = 1f;

        private TextMeshProUGUI _hoverLabel;
        private float _hoverLabelAlpha;
        private float _hoverLabelTarget;
        private Color _hoverLabelBaseColor;

        public void SetInteractable(bool interactable)
        {
            _interactable = interactable;
            if (!interactable)
            {
                _hovering = false;
                _targetScale = 1f;
            }
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

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            _baseScale = _rt.localScale;
        }

        private void Update()
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
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovering = false;
            _targetScale = 1f;
            _hoverLabelTarget = 0f;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_interactable) return;
            _targetScale = PressScale;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _targetScale = _hovering ? HoverScale : 1f;
        }
    }
}
