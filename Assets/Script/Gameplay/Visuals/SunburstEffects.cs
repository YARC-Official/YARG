using UnityEngine;

namespace YARG.Gameplay.Visuals
{
    public class SunburstEffects : MonoBehaviour
    {
        [SerializeField]
        private GameObject _grooveSunburstEffect;
        [SerializeField]
        private GameObject _grooveLightEffect;

        [Space]
        [SerializeField]
        private GameObject _starpowerSunburstEffect;
        [SerializeField]
        private GameObject _starpowerLightEffect;

        public void SetSunburstEffects(bool groove, bool starpower)
        {
            _grooveSunburstEffect.SetActive(groove);
            _grooveLightEffect.SetActive(groove);
        }

        private void Update()
        {
            _grooveSunburstEffect.transform.Rotate(0f, 0f, Time.deltaTime * -25f);
            // _starpowerSunburstEffect.transform.Rotate(0f, 0f, Time.deltaTime * -25f);
        }
    }
}
