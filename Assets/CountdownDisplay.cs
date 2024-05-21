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
        private double _circlePercentage;

        public bool UpdateCountdown(uint measuresLeft)
        {
            bool countdownStillActive = true;

            if (measuresLeft != _measuresLeft)
            {
                _measuresLeft = measuresLeft;

                if (measuresLeft >= WaitCountdown.GET_READY_MEASURE)
                {
                    GameObject childToShow = null;
                    GameObject childToHide = null;

                    if (measuresLeft >= WaitCountdown.GET_READY_MEASURE + 1)
                    {
                        _countdownText.text = measuresLeft.ToString();

                        childToHide = _getReady.gameObject;
                        childToShow = _backgroundCircle.gameObject;
                    }
                    else if (measuresLeft == WaitCountdown.GET_READY_MEASURE)
                    {
                        childToHide = _backgroundCircle.gameObject;
                        childToShow = _getReady.gameObject;
                    }

                    // Update visibility of child objects
                    if (childToShow?.activeSelf == false)
                    {
                        childToShow.SetActive(true);
                    }

                    if (childToHide?.activeSelf == true)
                    {
                        childToHide.SetActive(false);
                    }

                    // Update visibility of parent object if hidden
                    if (!gameObject.activeSelf)
                    {
                        gameObject.SetActive(true);
                    }
                }
                else
                {
                    // Hide countdown
                    gameObject.SetActive(false);
                    countdownStillActive = false;
                }
            }

            return countdownStillActive;
        }

        public void UpdateCirclePercentage(double percent)
        {
            if (percent != _circlePercentage)
            {
                _circlePercentage = percent;
            }
        }

        public void ForceReset()
        {
            gameObject.SetActive(false);
        }
    }
}
