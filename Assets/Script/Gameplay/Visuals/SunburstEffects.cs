using UnityEngine;
using YARG.Settings;

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
            starpower &= SettingsManager.Settings.StarPowerHighwayFx.Value != StarPowerHighwayFxMode.Off;

            _grooveSunburstEffect.SetActive(groove && !starpower);
            _grooveLightEffect.SetActive(groove && !starpower);

            _starpowerSunburstEffect.SetActive(starpower);
            _starpowerLightEffect.SetActive(starpower);
        }

        private void Update()
        {
            _grooveSunburstEffect.transform.Rotate(0f, 0f, Time.deltaTime * -25f);
            _starpowerSunburstEffect.transform.Rotate(0f, 0f, Time.deltaTime * -25f);
        }
    }
}
