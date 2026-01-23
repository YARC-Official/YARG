using UnityEngine;

namespace YARG.Gameplay.Visuals
{
    public class StarPowerEffectElement : MonoBehaviour
    {
        private const float ANIM_LENGTH = 1f;

        // Safe amount backwards from the origin that we can assume the start position is
        private const float START_POSITION = 3f;

        private readonly int _animTimestampId = Shader.PropertyToID("_AnimTimestamp");

        // Don't immediately play the effect at the start if animations are disabled
        private float _animTimestamp = float.NegativeInfinity;

        public void Initialize()
        {
            // Assuming that the Z length of the effect is exactly one unit, scale the effect
            float endPosition = GameObject.Find("HUD Location").transform.position.z;
            float totalSpan = endPosition + START_POSITION;
            transform.localScale = transform.localScale.WithZ(transform.localScale.z * totalSpan);

            // Assuming that the effect is spawned at the origin, move the effect
            float zOffset = totalSpan / 2 - START_POSITION;
            transform.localScale = transform.localScale.AddZ(zOffset);
        }

        public void PlayAnimation()
        {
            _animTimestamp = 0f;
        }

        private void Update()
        {
            if (_animTimestamp > ANIM_LENGTH)
            {
                gameObject.SetActive(false);
                return;
            }

            _animTimestamp += Time.deltaTime;

            foreach (var meshRenderer in GetComponentsInChildren<MeshRenderer>())
            {
                foreach (var material in meshRenderer.materials)
                {
                    material.SetFloat(_animTimestampId, _animTimestamp);
                }
            }
        }
    }
}
