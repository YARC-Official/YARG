using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using YARG.Settings;

namespace YARG.Integration
{
    public class DataStreamInitializer : MonoBehaviour
    {
        private void Start()
        {
            if (SettingsManager.Settings.DataStreamEnable.Value)
            {
                DataStreamController.Initializer(SceneManager.GetActiveScene());
            }
        }
    }
}
