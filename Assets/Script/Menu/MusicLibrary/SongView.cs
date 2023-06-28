using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.UI.MusicLibrary.ViewTypes;

namespace YARG.UI.MusicLibrary
{
    public class SongView : MonoBehaviour
    {
        [SerializeField]
        private CanvasGroup _canvasGroup;

        [Space]
        [SerializeField]
        private GameObject _normalBackground;

        [SerializeField]
        private GameObject _selectedBackground;

        [SerializeField]
        private GameObject _categoryBackground;

        [SerializeField]
        private GameObject _selectedCategoryBackground;

        [Space]
        [SerializeField]
        private GameObject _secondaryTextContiner;

        [SerializeField]
        private GameObject _asMadeFamousByTextContainer;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _primaryText;

        [SerializeField]
        private TextMeshProUGUI[] _secondaryText;

        [SerializeField]
        private TextMeshProUGUI _sideText;

        [SerializeField]
        private Image _icon;

        private int _relativeSongIndex;
        private CancellationTokenSource _cancellationTokenSource;

        public void Init(int relativeSongIndex)
        {
            _relativeSongIndex = relativeSongIndex;
        }

        public void UpdateView()
        {
            int realIndex = SongSelection.Instance.SelectedIndex + _relativeSongIndex;
            bool selected = _relativeSongIndex == 0;

            if (realIndex < 0 || realIndex >= SongSelection.Instance.ViewList.Count)
            {
                _canvasGroup.alpha = 0f;
                return;
            }

            _canvasGroup.alpha = 1f;

            var viewType = SongSelection.Instance.ViewList[realIndex];

            _sideText.text = viewType.SideText;

            // Change font styles if selected
            if (selected)
            {
                _primaryText.color = Color.white;
                _primaryText.text = $"<b>{viewType.PrimaryText}</b>";

                foreach (var text in _secondaryText)
                {
                    text.color = new Color(0.192f, 0.894f, 0.945f, 1.0f);
                    text.text = $"<font-weight=500>{viewType.SecondaryText}</font-weight>";
                }
            }
            else
            {
                _primaryText.color = new Color(0.192f, 0.894f, 0.945f, 1.0f);
                _primaryText.text = viewType.PrimaryText;

                foreach (var text in _secondaryText)
                {
                    text.color = new Color(0.192f, 0.894f, 0.945f, 0.3f);
                    text.text = viewType.SecondaryText;
                }
            }

            // Set icon
            if (_cancellationTokenSource is { IsCancellationRequested: false })
            {
                _cancellationTokenSource.Cancel();
            }

            _cancellationTokenSource = new CancellationTokenSource();
            SetIcon(viewType, _cancellationTokenSource.Token).Forget();

            // Set secondary text type
            _secondaryTextContiner.SetActive(!viewType.UseAsMadeFamousBy);
            _asMadeFamousByTextContainer.SetActive(viewType.UseAsMadeFamousBy);

            SetBackground(viewType.Background, selected);
        }

        private async UniTask SetIcon(ViewType type, CancellationToken token)
        {
            _icon.gameObject.SetActive(false);

            try
            {
                var icon = await type.GetIcon();

                token.ThrowIfCancellationRequested();

                if (icon == null)
                {
                    _icon.gameObject.SetActive(false);
                }
                else
                {
                    _icon.gameObject.SetActive(true);
                    _icon.sprite = icon;
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void SetBackground(ViewType.BackgroundType type, bool selected)
        {
            _normalBackground.SetActive(false);
            _selectedBackground.SetActive(false);
            _categoryBackground.SetActive(false);
            _selectedCategoryBackground.SetActive(false);

            switch (type)
            {
                case ViewType.BackgroundType.Normal:
                    if (selected)
                    {
                        _selectedBackground.SetActive(true);
                    }
                    else
                    {
                        _normalBackground.SetActive(true);
                    }

                    break;
                case ViewType.BackgroundType.Category:
                    if (selected)
                    {
                        _selectedCategoryBackground.SetActive(true);
                    }
                    else
                    {
                        _categoryBackground.SetActive(true);
                    }

                    break;
            }
        }

        public void SecondaryTextClick()
        {
            int realIndex = SongSelection.Instance.SelectedIndex + _relativeSongIndex;
            var viewType = SongSelection.Instance.ViewList[realIndex];

            viewType.SecondaryTextClick();
        }

        public void IconClick()
        {
            int realIndex = SongSelection.Instance.SelectedIndex + _relativeSongIndex;
            var viewType = SongSelection.Instance.ViewList[realIndex];

            viewType.IconClick();
        }
    }
}