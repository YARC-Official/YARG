using UnityEngine;
using YARG.Core.Game;

namespace YARG.Gameplay.Visuals
{
    public class IndicatorStripes : MonoBehaviour
    {
        [SerializeField]
        private GameObject _stripPrefab;
        [SerializeField]
        private float _spacing = 0.3f;

        [Space]
        [SerializeField]
        private Transform _leftContainer;
        [SerializeField]
        private Transform _rightContainer;

        [Header("Engine Preset Stripe Colors")]
        [SerializeField]
        private Color _casual;
        [SerializeField]
        private Color _precision;
        [SerializeField]
        private Color _custom;

        [Header("Custom Preset Setting Colors")]
        [SerializeField]
        private Color _ghostingAllowedColor;
        [SerializeField]
        private Color _infiniteFrontEndColor;
        [SerializeField]
        private Color _dynamicHitWindowColor;

        private int _stripeCount;
        private bool _isCustomPreset;

        public void Initialize(EnginePreset enginePreset)
        {
            _isCustomPreset = false;

            if (enginePreset == EnginePreset.Default || enginePreset == EnginePreset.SoloTaps)
            {
                // Don't spawn any stripes in if it's the default
                // or solo taps, too, since it doesn't alter hit windows
            }
            else if (enginePreset == EnginePreset.Casual)
            {
                SpawnStripe(_casual);
            }
            else if (enginePreset == EnginePreset.Precision)
            {
                SpawnStripe(_precision);
            }
            else
            {
                // Otherwise, it must be a custom preset
                SpawnStripe(_custom);
                _isCustomPreset = true;
            }
        }

        public void Initialize(EnginePreset.FiveFretGuitarPreset guitarPreset)
        {
            if (!_isCustomPreset) return;

            if (!guitarPreset.AntiGhosting)
            {
                SpawnStripe(_ghostingAllowedColor);
            }

            if (guitarPreset.InfiniteFrontEnd)
            {
                SpawnStripe(_infiniteFrontEndColor);
            }

            if (guitarPreset.HitWindow.IsDynamic)
            {
                SpawnStripe(_dynamicHitWindowColor);
            }
        }

        private void SpawnStripe(Color c)
        {
            SpawnStripe(_leftContainer, c);
            SpawnStripe(_rightContainer, c);

            _stripeCount++;
        }

        private void SpawnStripe(Transform container, Color c)
        {
            var stripe = Instantiate(_stripPrefab, container);
            stripe.transform.localPosition = Vector3.zero.AddZ(-_spacing * _stripeCount);

            foreach (var meshRenderer in stripe.GetComponentsInChildren<MeshRenderer>())
            {
                foreach (var material in meshRenderer.materials)
                {
                    material.color = c;
                }
            }
        }
    }
}