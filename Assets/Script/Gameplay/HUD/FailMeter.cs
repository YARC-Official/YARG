using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using YARG.Core.Engine;
using YARG.Core.Logging;

namespace YARG.Gameplay.HUD
{
    public class FailMeter : MonoBehaviour
    {
        [SerializeField]
        private Slider Slider;
        [SerializeField]
        private Image FillImage;

        // TODO: Should probably make a more specific class we can reference here
        private EngineManager _manager;


        // GameManager will have to initialize us
        public void Initialize(EngineManager manager)
        {
            _manager = manager;
            YargLogger.LogDebug("Initialized fail meter");
        }

        // Update is called once per frame
        private void Update()
        {
            // Don't crash the whole game if we didn't get initialized for some reason
            if (_manager == null)
            {
                YargLogger.LogDebug("FailMeter not initialized");
                return;
            }

            var happiness = _manager.Happiness;
            if (happiness < 0.33f)
            {
                FillImage.color = Color.red;
            }
            else
            {
                FillImage.color = Color.green;
            }

            FillImage.fillAmount = happiness;
        }
    }
}
