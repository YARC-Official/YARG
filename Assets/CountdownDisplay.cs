using TMPro;
using UnityEditor.Localization.Plugins.XLIFF.V12;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Chart;

namespace YARG
{
    public class CountdownDisplay : MonoBehaviour
    {
        [SerializeField]
        private Image _backgroundCircle;
        [SerializeField]
        private TextMeshProUGUI _countdownText;
        [SerializeField]
        private Image _getReady;

        private uint _measuresLeft;

        public void UpdateCountdown(uint measuresLeft)
        {
            if (measuresLeft == _measuresLeft)
            {
                return; 
            }

            _measuresLeft = measuresLeft;

            if (measuresLeft <= WaitCountdown.END_COUNTDOWN_MEASURE)
            {
                // New measure count is below the threshold where the countdown display should be hidden
                gameObject.SetActive(false);
                return;
            }

            // New measure count is above display threshold
            gameObject.SetActive(true);

            if (measuresLeft > WaitCountdown.GET_READY_MEASURE)
            {
                _countdownText.text = measuresLeft.ToString();

                _getReady.gameObject.SetActive(false);
                _backgroundCircle.gameObject.SetActive(true);
            }
            else if (measuresLeft <= WaitCountdown.GET_READY_MEASURE)
            {
                // Change display from number to "Get Ready!"
                _backgroundCircle.gameObject.SetActive(false);
                _getReady.gameObject.SetActive(true);
            }
        }

        public void ForceReset()
        {
            gameObject.SetActive(false);
        }
    }
}
