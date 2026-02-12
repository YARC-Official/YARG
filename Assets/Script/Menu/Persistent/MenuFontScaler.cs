using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using YARG.Core.Logging;
using YARG.Settings;

namespace YARG.Menu.Persistent
{
    public class MenuFontScaler : MonoSingleton<MenuFontScaler>
    {
        // For most elements, this is used as the "font size floor" when scaling is set to 100%
        private const float DEFAULT_MAX_FONT_SIZE = 22f;

        // This overrides the "font size floor" per section, to be used when scaling is set to 100%.
        //  This allows us to have some sections that scale more than others without breaking the layout
        private static readonly Dictionary<string, float> MaxFontSizeByContainerName = new()
        {
            { "Persistent Canvas", 22.9f },
            { "MusicLibraryMenu", 36f },
            { "ProfileList", 23.5f },
            { "Songs", 36f },
            { "Content", 20.4f },
        };

        private static readonly float AbsoluteMaxFontSize = Mathf.Max(
            DEFAULT_MAX_FONT_SIZE,
            MaxFontSizeByContainerName.Values.Max()
        );

        [SerializeField][Range(0f, 1f)]
        private float _fontScaleFactor;

        private readonly Dictionary<TMP_Text, TextFontInfo> _fontInfoByText = new();
        private readonly Dictionary<Transform, int> _childCountByTransform = new();

        private float FontScaleSetting => SettingsManager.Settings.FontScaling.Value;

        private struct TextFontInfo
        {
            public float BaseFontSize;
            public float MaxFontSize;
        }

        protected override void SingletonAwake()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        protected override void SingletonDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Start()
        {
            if (SettingsManager.Settings != null)
            {
                _fontScaleFactor = Mathf.Clamp01(FontScaleSetting / 100f);
            }

            DoScaling();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            DoScaling();
        }

        private void Update()
        {
            if (DidAnyTransformChildrenChange())
            {
                DoScaling();
            }
        }

        private bool DidAnyTransformChildrenChange()
        {
            foreach ((var transform, int count) in _childCountByTransform)
            {
                var didChildrenChange = transform == null || transform.childCount != count;
                if (!didChildrenChange)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private void DoScaling()
        {
            BuildTextToFontInfo();
            ScaleTexts();
        }

        private void BuildTextToFontInfo()
        {
            var previous = new Dictionary<TMP_Text, TextFontInfo>(_fontInfoByText);
            _fontInfoByText.Clear();
            _childCountByTransform.Clear();

            //Set up baseline and max font size using defaults
            foreach (var text in FindAllTexts())
            {
                float baselineFontSize = previous.TryGetValue(text, out var previousInfo)
                    ? previousInfo.BaseFontSize
                    : text.fontSize;

                _fontInfoByText[text] = new TextFontInfo
                {
                    BaseFontSize = baselineFontSize,
                    MaxFontSize = DEFAULT_MAX_FONT_SIZE,
                };
            }

            // Override with any custom max font sizes for containers, and store child counts to detect changes later
            foreach (var transform in FindAllTransforms())
            {
                if (!MaxFontSizeByContainerName.TryGetValue(transform.name, out float maxFontSize))
                {
                    continue;
                }

                _childCountByTransform[transform] = transform.childCount;

                var transformTexts = transform.GetComponentsInChildren<TMP_Text>(true);
                foreach (var text in transformTexts)
                {
                    if (!_fontInfoByText.TryGetValue(text, out var scaleInfo))
                    {
                        continue;
                    }

                    scaleInfo.MaxFontSize = maxFontSize;
                    _fontInfoByText[text] = scaleInfo;
                }
            }
        }

        private Transform[] FindAllTransforms()
        {
            return FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private TMP_Text[] FindAllTexts()
        {
            return FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private void ScaleTexts()
        {
            foreach ((var text, TextFontInfo scaleInfo) in _fontInfoByText)
            {
                if (text == null)
                {
                    continue;
                }

                float scaledSize = _fontScaleFactor * AbsoluteMaxFontSize;
                scaledSize = Mathf.Clamp(scaledSize, scaleInfo.BaseFontSize, scaleInfo.MaxFontSize);

                text.enableAutoSizing = false;
                text.fontSize = scaledSize;
                text.ForceMeshUpdate();
            }
        }

        public void SetFontScalePercent(float percent)
        {
            _fontScaleFactor = Mathf.Clamp01(percent / 100f);
            ScaleTexts();
            enabled = _fontScaleFactor > 0f;
        }

        private void OnValidate()
        {
            ScaleTexts();
        }
    }
}
