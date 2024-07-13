using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace YARG.Gameplay.Visuals
{
    public class StrikelinePositioner : MonoBehaviour
    {
        private float _zOffset;

        private Coroutine _coroutine;

        private void Start()
        {
            _zOffset = transform.localPosition.z;
            _coroutine = StartCoroutine(RaiseStrikeline());
        }

        private IEnumerator RaiseStrikeline()
        {
            var transform = this.transform;
            transform.localPosition = transform.localPosition.WithZ(_zOffset - 1.2f);

            yield return new WaitForSeconds(0.5f);
            yield return DOTween.Sequence()
                .PrependInterval(0.25f) // Total duration of sequence
                .Append(transform.DOMoveZ(_zOffset + 0.1f, 0.167f)).SetEase(Ease.OutCirc)
                .Append(transform.DOMoveZ(_zOffset, 0.083f)).SetEase(Ease.InOutSine);
        }

        private void OnDestroy()
        {
            StopCoroutine(_coroutine);
        }
    }
}
