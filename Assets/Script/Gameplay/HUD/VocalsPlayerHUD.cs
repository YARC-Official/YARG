using UnityEngine;
using UnityEngine.UI;

namespace YARG.Gameplay.HUD
{
    public class VocalsPlayerHUD : MonoBehaviour
    {
        [SerializeField]
        private Image _comboMeterFill;

        private float _comboMeterFillTarget;

        private void Update()
        {
            if (_comboMeterFillTarget == 0f)
            {
                // Go to zero instantly
                _comboMeterFill.fillAmount = 0f;
            }
            else
            {
                _comboMeterFill.fillAmount = Mathf.Lerp(_comboMeterFill.fillAmount,
                    _comboMeterFillTarget, Time.deltaTime * 12f);
            }
        }

        public void UpdateInfo(float phrasePercent)
        {
            _comboMeterFillTarget = phrasePercent;
        }
    }
}