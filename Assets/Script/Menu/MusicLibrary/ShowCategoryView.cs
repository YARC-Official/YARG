using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
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
        [Space]
        [SerializeField]
        private GameObject _unselectedVisual;
        [SerializeField]
        private GameObject _selectedVisual;

        public void SetSelected(bool selected)
        {
            var image = _selectedVisual.GetComponent<Image>();

            // Ease in the color on selection
            if (selected)
            {
                _unselectedVisual.SetActive(false);
                var color = image.color;
                color.a = 0.01f;
                image.color = color;
                _selectedVisual.SetActive(true);
                // image.CrossFadeAlpha(1.0f, 0.1f, false);
                image.DOFade(1.0f, 0.2f).SetEase(Ease.OutCubic);
            }
            else
            {
                _unselectedVisual.SetActive(true);
                var color = image.color;
                color.a = 1.0f;
                image.color = color;
                _selectedVisual.SetActive(false);
            }
        }
    }
}