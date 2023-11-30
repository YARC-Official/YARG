using UnityEngine;
using YARG.Settings;

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

        private int _stripeCount;

        public void Initialize()
        {
            if (!SettingsManager.Settings.AntiGhosting.Data)
            {
                SpawnStripe(new Color(1f, 0.5f, 0f));
            }

            if (SettingsManager.Settings.InfiniteFrontEnd.Data)
            {
                SpawnStripe(new Color(0.3f, 0.75f, 0.3f));
            }

            if (SettingsManager.Settings.DynamicWindow.Data)
            {
                SpawnStripe(new Color(0.25f, 0.65f, 0.9f));
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