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
        private const float MARQUEE_SPEED = 70f;
        private const float MARQUEE_END_PAUSE = 1f;
        private const float MARQUEE_FADE_DURATION = 0.25f;
        private const float MARQUEE_RIGHT_INSET = 15f;

        private bool _marqueeActive;
        private float _marqueeStartTime;

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
        private Image _starsObtainedIcon;
        [SerializeField]
        private TextMeshProUGUI _scoreText;
        [SerializeField]
        private TextMeshProUGUI _buttonHelpText;

        [Space]
        [SerializeField]
        private Image _trackGradient;
        [SerializeField]
        private Image _normalCategoryHeaderGradient;
        [SerializeField]
        private Image _secondaryHeaderBackground;
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

            SetMarqueeActive(selected && viewType is SongViewType);

            if (viewType is SecondaryHeaderViewType && viewType.GetIcon() == null)
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
                bool hasGoldStars = viewType switch
                {
                    SortHeaderViewType sortHeader => sortHeader.HasGoldStars,
                    SecondaryHeaderViewType secondaryHeader => secondaryHeader.HasGoldStars,
                    _ => false,
                };
                _starsObtainedIcon.sprite = hasGoldStars ? _starGoldSprite : _starWhiteSprite;
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

        public override void Hide()
        {
            SetMarqueeActive(false);
            base.Hide();
        }

        private void LateUpdate()
        {
            if (!_marqueeActive)
            {
                return;
            }

            float elapsed = Time.unscaledTime - _marqueeStartTime;
            UpdateMarquee(_primaryText[0], elapsed);
            UpdateMarquee(_secondaryText[0], elapsed);
        }

        private void SetMarqueeActive(bool active)
        {
            _marqueeActive = active;
            _marqueeStartTime = Time.unscaledTime;

            SetTextOverflow(_primaryText[0], active);
            SetTextOverflow(_secondaryText[0], active);
        }

        private static void SetTextOverflow(TextMeshProUGUI text, bool marquee)
        {
            text.overflowMode = marquee ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            text.ForceMeshUpdate();
        }

        private static void UpdateMarquee(TextMeshProUGUI text, float elapsed)
        {
            text.ForceMeshUpdate();

            float availableWidth = text.rectTransform.rect.width;
            if (text.preferredWidth <= availableWidth)
            {
                return;
            }

            float marqueeWidth = availableWidth - MARQUEE_RIGHT_INSET;
            float overflow = text.preferredWidth - marqueeWidth;

            float travelTime = overflow / MARQUEE_SPEED;
            float cycleTime = travelTime + 2f * (MARQUEE_END_PAUSE + MARQUEE_FADE_DURATION);
            float phase = elapsed % cycleTime;
            float offset;
            float alpha = 1f;

            if (phase < MARQUEE_END_PAUSE)
            {
                offset = 0f;
            }
            else if (phase < MARQUEE_END_PAUSE + travelTime)
            {
                offset = (phase - MARQUEE_END_PAUSE) * MARQUEE_SPEED;
            }
            else if (phase < 2f * MARQUEE_END_PAUSE + travelTime)
            {
                offset = overflow;
            }
            else if (phase < 2f * MARQUEE_END_PAUSE + travelTime + MARQUEE_FADE_DURATION)
            {
                offset = overflow;
                float fadeProgress = (phase - 2f * MARQUEE_END_PAUSE - travelTime) / MARQUEE_FADE_DURATION;
                alpha = 1f - Mathf.SmoothStep(0f, 1f, fadeProgress);
            }
            else
            {
                offset = 0f;
                float fadeProgress = (phase - 2f * MARQUEE_END_PAUSE - travelTime - MARQUEE_FADE_DURATION) /
                    MARQUEE_FADE_DURATION;
                alpha = Mathf.SmoothStep(0f, 1f, fadeProgress);
            }

            Rect bounds = text.rectTransform.rect;
            bounds.xMax -= MARQUEE_RIGHT_INSET;
            for (int i = 0; i < text.textInfo.characterCount; i++)
            {
                var character = text.textInfo.characterInfo[i];
                if (!character.isVisible)
                {
                    continue;
                }

                var meshInfo = text.textInfo.meshInfo[character.materialReferenceIndex];
                int vertexIndex = character.vertexIndex;
                var vertices = meshInfo.vertices;
                var uvs = meshInfo.uvs0;
                var colors = meshInfo.colors32;

                float originalLeft = vertices[vertexIndex].x;
                float originalRight = vertices[vertexIndex + 2].x;
                float shiftedLeft = originalLeft - offset;
                float shiftedRight = originalRight - offset;
                float width = originalRight - originalLeft;

                float clippedLeft = Mathf.Clamp(shiftedLeft, bounds.xMin, bounds.xMax);
                float clippedRight = Mathf.Clamp(shiftedRight, bounds.xMin, bounds.xMax);
                float leftT = width > 0f ? (clippedLeft - shiftedLeft) / width : 0f;
                float rightT = width > 0f ? (clippedRight - shiftedLeft) / width : 1f;

                Vector4 bottomLeftUv = uvs[vertexIndex];
                Vector4 topLeftUv = uvs[vertexIndex + 1];
                Vector4 topRightUv = uvs[vertexIndex + 2];
                Vector4 bottomRightUv = uvs[vertexIndex + 3];

                vertices[vertexIndex].x = clippedLeft;
                vertices[vertexIndex + 1].x = clippedLeft;
                vertices[vertexIndex + 2].x = clippedRight;
                vertices[vertexIndex + 3].x = clippedRight;
                uvs[vertexIndex] = Vector4.Lerp(bottomLeftUv, bottomRightUv, leftT);
                uvs[vertexIndex + 1] = Vector4.Lerp(topLeftUv, topRightUv, leftT);
                uvs[vertexIndex + 2] = Vector4.Lerp(topLeftUv, topRightUv, rightT);
                uvs[vertexIndex + 3] = Vector4.Lerp(bottomLeftUv, bottomRightUv, rightT);

                for (int vertexOffset = 0; vertexOffset < 4; vertexOffset++)
                {
                    int colorIndex = vertexIndex + vertexOffset;
                    var color = colors[colorIndex];
                    color.a = (byte) (color.a * alpha);
                    colors[colorIndex] = color;
                }
            }

            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Uv0 |
                TMP_VertexDataUpdateFlags.Colors32);
        }

        protected override void SetBackground(bool selected, BaseViewType.BackgroundType type)
        {
            _trackGradient.gameObject.SetActive(selected);

            bool showSecondaryHeaderBackground = type == BaseViewType.BackgroundType.SecondaryHeader;
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
}
