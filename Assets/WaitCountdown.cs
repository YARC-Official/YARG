using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Gameplay;
using YARG.Playback;

namespace YARG
{
    public class WaitCountdown : MonoBehaviour
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
            if (measuresLeft != _measuresLeft)
            {
                _measuresLeft = measuresLeft;

                if (measuresLeft > 0)
                {
                    GameObject childToShow = null;
                    GameObject childToHide = null;

                    if (measuresLeft >= 2)
                    {
                        _countdownText.text = measuresLeft.ToString();

                        childToHide = _getReady.gameObject;
                        childToShow = _backgroundCircle.gameObject;
                    }
                    else if (measuresLeft == 1)
                    {
                        childToHide = _backgroundCircle.gameObject;
                        childToShow = _getReady.gameObject;
                    }

                    //update visibility of child objects
                    if (childToShow?.activeSelf == false)
                    {
                        childToShow.SetActive(true);
                    }

                    if (childToHide?.activeSelf == true)
                    {
                        childToHide.SetActive(false);
                    }

                    //update visibility of parent object if hidden
                    if (!gameObject.activeSelf)
                    {
                        gameObject.SetActive(true);
                    }
                }
                else
                {
                    //hide countdown when there is one measure before the next set of notes
                    gameObject.SetActive(false);
                }
            }

        }

        public void ForceReset()
        {
            gameObject.SetActive(false);
        }
    }
}
