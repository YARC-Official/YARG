using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace YARG.Menu.ProfileList
{
    public class ControllerEntryView : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _name;

        public void Initialize(string name)
        {
            _name.text = name;
        }
    }
}
