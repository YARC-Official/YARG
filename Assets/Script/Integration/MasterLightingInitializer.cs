using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace YARG.Integration
{
    public class MasterLightingInitializer : MonoBehaviour
    {
        private void Start()
        {
            MasterLightingController.Initializer(SceneManager.GetActiveScene());
        }
    }
}

/*
"The crows seem to be calling my name", thought Caw.

    - Jack Handey.
*/
