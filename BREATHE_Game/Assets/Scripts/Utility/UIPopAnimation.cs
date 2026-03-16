using System;
using System.Collections;
using UnityEngine;

namespace Breathe.Utility
{
    // Pure code-driven pop-in / pop-out scale animations for UI elements.
    // No Animator or DOTween needed — just start the coroutines from any MonoBehaviour,
    // or use UIPopAnimator for Inspector-configurable auto-play behavior.
    public static class UIPopAnimation
    {
        // Standard "back" easing constant (1.70158).
        public const float DefaultOvershoot = 1.70158f;

        // Scales target from zero to targetScale with a bouncy overshoot ease.
        public static IEnumerator PopIn(
            Transform target,
            float duration = 0.35f,
            Vector3? targetScale = null,
            float overshoot = DefaultOvershoot,
            float delay = 0f,
            Action onComplete = null)
        {
            if (target == null) yield break;

            Vector3 final3 = targetScale ?? Vector3.one;
            target.localScale = Vector3.zero;

            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float s = EvaluateBackOut(t, overshoot);
                target.localScale = final3 * s;
                yield return null;
            }

            target.localScale = final3;
            onComplete?.Invoke();
        }

        // Scales target from its current scale down to zero with an accelerating ease.
        public static IEnumerator PopOut(
            Transform target,
            float duration = 0.2f,
            float delay = 0f,
            bool deactivateOnComplete = false,
            Action onComplete = null)
        {
            if (target == null) yield break;

            Vector3 startScale = target.localScale;

            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float s = EvaluateBackIn(t);
                target.localScale = startScale * (1f - s);
                yield return null;
            }

            target.localScale = Vector3.zero;

            if (deactivateOnComplete && target.gameObject != null)
                target.gameObject.SetActive(false);

            onComplete?.Invoke();
        }

        // Pop-in, hold at full scale, then pop-out. Good for transient popups.
        public static IEnumerator PopInHoldOut(
            Transform target,
            float popInDuration = 0.35f,
            float holdDuration = 1.0f,
            float popOutDuration = 0.2f,
            Vector3? targetScale = null,
            float overshoot = DefaultOvershoot,
            bool deactivateOnComplete = true,
            Action onComplete = null)
        {
            if (target == null) yield break;

            Vector3 final3 = targetScale ?? Vector3.one;

            yield return PopIn(target, popInDuration, final3, overshoot);

            if (holdDuration > 0f)
                yield return new WaitForSeconds(holdDuration);

            yield return PopOut(target, popOutDuration, deactivateOnComplete: deactivateOnComplete);

            onComplete?.Invoke();
        }

        // "Back" ease-out: overshoots the target then settles. Output peaks above 1.0.
        public static float EvaluateBackOut(float t, float overshoot = DefaultOvershoot)
        {
            float c3 = overshoot + 1f;
            float tm1 = t - 1f;
            return 1f + c3 * tm1 * tm1 * tm1 + overshoot * tm1 * tm1;
        }

        // "Back" ease-in: pulls back slightly then accelerates through. Used inverted for pop-out.
        public static float EvaluateBackIn(float t, float overshoot = DefaultOvershoot)
        {
            float c3 = overshoot + 1f;
            return c3 * t * t * t - overshoot * t * t;
        }
    }
}
