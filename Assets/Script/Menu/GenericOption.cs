using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.UI
{
    public class GenericOption : MonoBehaviour
    {
        [SerializeField]
        private Image topBorder;

        [SerializeField]
        private Image bottomBorder;

        [SerializeField]
        private GameObject selectedBackground;

        [SerializeField]
        private TextMeshProUGUI text;

        [SerializeField]
        private Image image;

        public event Action<GenericOption> MouseHoverEvent;
        public event Action<GenericOption> MouseClickEvent;

        private void MouseEnter()
        {
            MouseHoverEvent?.Invoke(this);
        }

        private void MouseClick()
        {
            MouseClickEvent?.Invoke(this);
        }

        public void SetSelected(bool selected)
        {
            selectedBackground.SetActive(selected);

            if (selected)
            {
                topBorder.color = Color.white;
                bottomBorder.color = Color.white;
            }
            else
            {
                topBorder.color = new Color32(22, 39, 90, 255);
                bottomBorder.color = new Color32(22, 39, 90, 255);
            }
        }

        public void SetText(string t)
        {
            image.gameObject.SetActive(false);
            text.text = t;
        }

        public void SetImage(Sprite img)
        {
            if (img == null)
            {
                image.gameObject.SetActive(false);
            }
            else
            {
                image.gameObject.SetActive(true);
                image.sprite = img;
            }
        }
    }
}