using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Menu.Navigation;

namespace YARG.Menu.Settings.Visuals
{
    public class ColorPresetHeader : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private Button _toggle;
        [SerializeField] private ColoredButton _copyButton;
        [SerializeField] private Image[] _swatchFills;
        [SerializeField] private Image[] _swatchStrokes;
        [SerializeField] private GameObject _copyOutline;

        public TextMeshProUGUI Label => _label;
        public Button Toggle => _toggle;
        public ColoredButton CopyButton => _copyButton;
        public GameObject CopyOutline => _copyOutline;

        public void ShowSwatch(int index, Sprite fill, Sprite stroke)
        {
            _swatchFills[index].sprite = fill;
            _swatchStrokes[index].sprite = stroke;
            _swatchFills[index].gameObject.SetActive(true);
        }

        public void TintSwatch(int index, Color fill, Color stroke)
        {
            _swatchFills[index].color = fill;
            _swatchStrokes[index].color = stroke;
        }
    }
}
