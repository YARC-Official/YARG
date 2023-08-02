using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Chart;
using YARG.Gameplay;

namespace YARG.Menu.Gameplay
{
    public class PracticeView : MonoBehaviour
    {

        private GameManager _gameManager;

        [SerializeField]
        private CanvasGroup _canvasGroup;
        [SerializeField]
        private GameObject _normalBackground;
        [SerializeField]
        private GameObject _selectedBackground;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _sectionName;

        private Button _button;

        private Section _section;

        private void Awake()
        {
            _gameManager = FindObjectOfType<GameManager>();

            _button = GetComponent<Button>();

            _button.onClick.AddListener(() =>
            {

            });
        }

        public void ShowAsSection(bool selected, Section section)
        {
            _canvasGroup.alpha = 1f;

            _section = section;

            // TODO: Make speed work
            _sectionName.text = _section.Name;

            // Set correct background
            _normalBackground.SetActive(!selected);
            _selectedBackground.SetActive(selected);

            if (selected)
            {
                _sectionName.text = $"<color=white><font-weight=700>{_sectionName.text}</font-weight></color>";
            }

            _button.interactable = true;
        }

        public void Hide()
        {
            _canvasGroup.alpha = 0f;
            _button.interactable = false;

            _section = null;
        }
    }
}