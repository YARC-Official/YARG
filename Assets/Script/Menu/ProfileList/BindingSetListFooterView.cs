using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core;

namespace YARG.Menu.ProfileList
{
    public class BindingSetListFooterView : MonoBehaviour
    {
        [SerializeField]
        private Button _addOtherButton;

        private List<GameMode> _remainingGameModes { get; set; }

        public void Init(List<GameMode> remainingGameModes)
        {
            _remainingGameModes = remainingGameModes;
        }

        public void AddOther()
        {

        }
    }
}
