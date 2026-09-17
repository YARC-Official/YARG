using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Menu.Data;
using YARG.Core.Game;
using YARG.Menu.ListMenu;
using YARG.Settings;
using YARG.Song;

namespace YARG.Menu.MusicLibrary
{
    public class SongView : ViewObject<ViewType>
    {
        [SerializeField]
        private GameObject _songNameContainer;
        [SerializeField]
        private GameObject _instrumentDifficultyViewContainer;
        [SerializeField]
        private InstrumentDifficultyView _instrumentDifficultyView;
        [SerializeField]
        private StarView _starView;

        [Space]
        [SerializeField]
        private GameObject _secondaryTextContainer;
        [SerializeField]
        private GameObject _asMadeFamousByTextContainer;

        [Space]
        [SerializeField]
        private GameObject _favoriteButtonContainer;
        [SerializeField]
        private GameObject _favoriteButtonContainerSelected;
        [SerializeField]
        private Image[] _favoriteButtons;

        [Space]
        [SerializeField]
        private Sprite _favoriteUnfilled;
        [SerializeField]
        private Sprite _favouriteFilled;
        [SerializeField]
        private Sprite _secondaryHeaderIcon;

        [Space]
        [SerializeField]
        private GameObject _categoryNameContainer;
        [SerializeField]
        private TextMeshProUGUI _categoryText;
        [SerializeField]
        private RectTransform _starsObtainedView;
        [SerializeField]
        private TextMeshProUGUI _starsObtainedText;
        [SerializeField]
        private TextMeshProUGUI _scoreText;
        [SerializeField]
        private TextMeshProUGUI _buttonHelpText;

        [Space]
        [SerializeField]
        private Image _trackGradient;
        [SerializeField]
        private Image _normalCategoryHeaderGradient;
        private SecondaryHeaderGradientGraphic _secondaryHeaderBackground;
        [SerializeField]
        private GameObject _buttonHeaderBackground;

        [SerializeField]
        private Image _selectedSourceIconBackground;

        [SerializeField]
        private GameObject _starHeaderGroup;
        [SerializeField]
        private Image[] _starHeaderImages;

        [SerializeField]
        private Sprite _starGoldSprite;
        [SerializeField]
        private Sprite _starWhiteSprite;

        public override void Show(bool selected, ViewType viewType)
        {
            base.Show(selected, viewType);

            if (viewType is SecondaryHeaderViewType)
            {
                SetIcon(_secondaryHeaderIcon);
            }

            var scoreInfoMode = SettingsManager.Settings.HighScoreInfo.Value;

            // use category header primary text (which supports wider text), when used as section header
            if (viewType.UseWiderPrimaryText)
            {
                _songNameContainer.SetActive(false);
                _categoryNameContainer.SetActive(true);
            }
            else
            {
                _songNameContainer.SetActive(true);
                _categoryNameContainer.SetActive(false);
            }

            _selectedSourceIconBackground.enabled = selected & viewType is SongViewType;

            // Set score and instrument display
            var scoreInfo = viewType.GetScoreInfo();
            _instrumentDifficultyViewContainer.SetActive(scoreInfo is not null);
            if (scoreInfo is not null)
            {
                _instrumentDifficultyView.SetInfo(scoreInfo.Value);
            }

            // Set star view
            var starAmount = viewType.GetStarAmount();
            _starView.gameObject.SetActive(scoreInfoMode == HighScoreInfoMode.Stars && starAmount is not null);
            if (starAmount is not null)
            {
                _starView.SetStars(starAmount.Value);
            }

            // Set "As Made Famous By" text
            _asMadeFamousByTextContainer.SetActive(viewType.UseAsMadeFamousBy);
            if (viewType.UseAsMadeFamousBy)
            {
                _asMadeFamousByTextContainer.GetComponent<TextMeshProUGUI>().color = selected ? MenuData.Colors.BrightText : MenuData.Colors.TrackDefaultSecondary;
            }

            // Set stars obtained view
            _starsObtainedView.gameObject.SetActive(viewType is SortHeaderViewType or SecondaryHeaderViewType);
            if (viewType is SortHeaderViewType or SecondaryHeaderViewType)
            {
                _starsObtainedText.text = viewType.GetSideText(selected);
            }

            // Set help text for button views
            _buttonHelpText.gameObject.SetActive(viewType is ButtonViewType);
            if (viewType is ButtonViewType)
            {
                _buttonHelpText.text = viewType.GetSideText(selected);
            }

            _scoreText.gameObject.SetActive(scoreInfoMode == HighScoreInfoMode.Score && viewType is SongViewType);
            if (scoreInfoMode == HighScoreInfoMode.Score)
            {
                _scoreText.text = viewType.GetSideText(selected);
            }

            // Show/hide favorite button
            var favoriteInfo = viewType.GetFavoriteInfo();

            if (SettingsManager.Settings.ShowFavoriteButton.Value)
            {
                _favoriteButtonContainer.SetActive(!selected && favoriteInfo.ShowFavoriteButton);
                _favoriteButtonContainerSelected.SetActive(selected && favoriteInfo.ShowFavoriteButton);
                UpdateFavoriteSprite(favoriteInfo);
            }
            else
            {
                _favoriteButtonContainer.SetActive(false);
                _favoriteButtonContainerSelected.SetActive(false);
            }

            // Set height
            if (viewType is SortHeaderViewType)
            {
                gameObject.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 60);
            }
            else if (viewType is SecondaryHeaderViewType)
            {
                gameObject.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 50);
            }
            else
            {
                gameObject.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 70);
            }

            StarAmount starHeaderAmount = StarAmount.None;

            foreach (StarAmount amount in Enum.GetValues(typeof(StarAmount)))
            {
                if (amount == StarAmount.None || amount == StarAmount.NoPart)
                    continue; // skip irrelevant entries

                string displayName = amount.GetDisplayName();

                if (_categoryText.text.Contains(">" + displayName + "<"))
                {
                    starHeaderAmount = amount;
                    break;
                }
            }

            if (starHeaderAmount != StarAmount.None && SettingsManager.Settings.LibrarySort == SortAttribute.Stars)
            {
                int starCount = starHeaderAmount.GetStarCount();
                Sprite starSprite = starHeaderAmount == StarAmount.StarGold ? _starGoldSprite : _starWhiteSprite;

                _categoryText.gameObject.SetActive(false);
                _starHeaderGroup.SetActive(true);

                for (int i = 0; i < _starHeaderImages.Length; i++)
                {
                    bool show = i < starCount;
                    _starHeaderImages[i].gameObject.SetActive(show);
                    if (show)
                    {
                        _starHeaderImages[i].sprite = starSprite;
                    }
                }
            }
            else
            {
                _categoryText.gameObject.SetActive(true);
                _starHeaderGroup.SetActive(false);
            }
        }

        protected override void SetBackground(bool selected, BaseViewType.BackgroundType type)
        {
            _trackGradient.gameObject.SetActive(selected);

            bool showSecondaryHeaderBackground = type == BaseViewType.BackgroundType.SecondaryHeader;
            if (showSecondaryHeaderBackground)
                EnsureSecondaryHeaderBackground();

            if (_secondaryHeaderBackground != null)
                _secondaryHeaderBackground.gameObject.SetActive(showSecondaryHeaderBackground);

            NormalBackground.SetActive(false);
            SelectedBackground.SetActive(false);
            CategoryBackground.SetActive(false);
            _buttonHeaderBackground.SetActive(false);

            switch (type)
            {
                case BaseViewType.BackgroundType.Normal:
                    if (selected)
                    {
                        SelectedBackground.SetActive(true);
                    }
                    else
                    {
                        NormalBackground.SetActive(true);
                    }

                    break;
                case BaseViewType.BackgroundType.SecondaryHeader:
                    break;
                case BaseViewType.BackgroundType.Category:
                    if (selected)
                    {
                        SelectedBackground.SetActive(true);
                    }
                    else
                    {
                        CategoryBackground.SetActive(true);
                    }

                    break;
                case BaseViewType.BackgroundType.Button:
                    if (selected)
                    {
                        SelectedBackground.SetActive(true);
                    }
                    else
                    {
                        _buttonHeaderBackground.SetActive(true);
                    }

                    break;
                default:
                    break;
            }
        }

        private void EnsureSecondaryHeaderBackground()
        {
            if (_secondaryHeaderBackground != null) return;

            var overlayObject = new GameObject("Secondary Header Background", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(SecondaryHeaderGradientGraphic));
            var overlayTransform = overlayObject.GetComponent<RectTransform>();
            overlayTransform.SetParent(NormalBackground.transform.parent, false);
            overlayTransform.anchorMin = Vector2.zero;
            overlayTransform.anchorMax = Vector2.one;
            overlayTransform.offsetMin = Vector2.zero;
            overlayTransform.offsetMax = Vector2.zero;

            _secondaryHeaderBackground = overlayObject.GetComponent<SecondaryHeaderGradientGraphic>();
            _secondaryHeaderBackground.raycastTarget = false;
        }

        private void UpdateFavoriteSprite(ViewType.FavoriteInfo favoriteInfo)
        {
            if (!favoriteInfo.ShowFavoriteButton) return;

            foreach (var button in _favoriteButtons)
            {
                button.sprite = favoriteInfo.IsFavorited
                    ? _favouriteFilled
                    : _favoriteUnfilled;
            }
        }

        public void PrimaryTextClick()
        {
            if (!Showing) return;

            ViewType.PrimaryButtonClick();
        }

        public void SecondaryTextClick()
        {
            if (!Showing) return;

            ViewType.SecondaryTextClick();
        }

        public void FavoriteClick()
        {
            if (!Showing) return;

            ViewType.FavoriteClick();

            // Update the sprite after in case the state changed
            UpdateFavoriteSprite(ViewType.GetFavoriteInfo());
        }
    }

    internal sealed class SecondaryHeaderGradientGraphic : MaskableGraphic
    {
        private const int COLUMN_COUNT = 2;
        private const int ROW_COUNT = 4;

        // Opaque, luminance-equivalent grayscale values for the requested corners.
        // Rows run bottom-to-top and columns run left-to-right.
        private static readonly Color32[] Colors =
        {
            new(0x13, 0x13, 0x13, 0xFF), // Bottom edge left:  #091326
            new(0x2F, 0x2F, 0x2F, 0xFF), // Bottom edge right: #1E2F4A
            new(0x0B, 0x0B, 0x0B, 0xFF), // Bottom-left:  #060B17
            new(0x1B, 0x1B, 0x1B, 0xFF), // Bottom-right: #111B2D
            new(0x18, 0x18, 0x18, 0xFF), // Top-left:     #021930
            new(0x18, 0x18, 0x18, 0xFF), // Top-right:    #09182C
            new(0x2A, 0x2A, 0x2A, 0xFF), // Top edge left:     #042B50
            new(0x2A, 0x2A, 0x2A, 0xFF), // Top edge right:    #0F2B49
        };

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            Rect rect = GetPixelAdjustedRect();
            float edgeHeight = Mathf.Min(1f, rect.height * 0.5f);
            for (int row = 0; row < ROW_COUNT; row++)
            {
                float y = row switch
                {
                    0 => rect.yMin,
                    1 => rect.yMin + edgeHeight,
                    2 => rect.yMax - edgeHeight,
                    _ => rect.yMax,
                };
                for (int column = 0; column < COLUMN_COUNT; column++)
                {
                    float x = Mathf.Lerp(rect.xMin, rect.xMax, (float) column / (COLUMN_COUNT - 1));
                    AddVertex(vertexHelper, x, y, Colors[row * COLUMN_COUNT + column]);

                    if (row == 0 || column == 0) continue;

                    int topRight = row * COLUMN_COUNT + column;
                    int topLeft = topRight - 1;
                    int bottomRight = topRight - COLUMN_COUNT;
                    int bottomLeft = bottomRight - 1;
                    vertexHelper.AddTriangle(bottomLeft, topLeft, bottomRight);
                    vertexHelper.AddTriangle(bottomRight, topLeft, topRight);
                }
            }
        }

        private static void AddVertex(VertexHelper vertexHelper, float x, float y, Color32 color)
        {
            vertexHelper.AddVert(new Vector3(x, y), color, Vector2.zero);
        }
    }
}
