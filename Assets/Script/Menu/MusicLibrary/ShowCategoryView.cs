using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using YARG.Menu.Persistent;

namespace YARG.Menu.MusicLibrary
{
    /// <summary>
    /// Play A Show category view
    /// </summary>
    public class ShowCategoryView : MonoBehaviour
    {
        [SerializeField]
        public TextMeshProUGUI CategoryText;
        [SerializeField]
        private ShowPickerButton _pickerButton;


    }
}