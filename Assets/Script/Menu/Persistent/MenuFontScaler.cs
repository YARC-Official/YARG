using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using YARG.Settings;

namespace YARG.Menu.Persistent
{
    public class MenuFontScaler : MonoSingleton<MenuFontScaler>
    {
        // For most elements, this is used as the "font size floor" when scaling is set to 100%
        private const float DEFAULT_FULL_SCALE_SIZE = 22f;

        // This overrides the "font size floor" per section, to be used when scaling is set to 100%.
        //  This allows us to have some sections that scale more than others without breaking the layout
        private static readonly Dictionary<string, float> ContainerFullScaleSize = new()
        {
            { "Persistent Canvas", 22.9f },
            { "Button Container", 26f },
            { "MusicLibraryMenu", 36f },
            { "ProfileList", 23.5f },
            { "Songs", 36f },
            { "Content", 20.4f },
            { "Album Cover Container", 24f },
            { "Rating", 26f },
            { "Views", 39f }, //History rows
            { "Tabs", 30f }, // Top tabs
            { "Message of the Day", 40f}
        };

        private static readonly float MaxFullScaleSize = Mathf.Max(
            DEFAULT_FULL_SCALE_SIZE,
            ContainerFullScaleSize.Values.Max()
        );

        [SerializeField][Range(0f, 1f)]
        private float _fontScaleFactor;

        private readonly Dictionary<TMP_Text, TextFontInfo> _fontInfoByText = new();
        private readonly List<ContainerInfo> _containerInfos = new();

        private float FontScaleSetting => SettingsManager.Settings.FontScaling.Value;

        private struct TextFontInfo
        {
            public float BaselineFontSize;
            public float MaxSize;
            public bool UsesAutoSizing;
        }

        private struct ContainerInfo
        {
            public Transform Transform;
            public float FullScaleSize;
            public int ChildCount;
            public int HierarchyDepth;
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
            if (DidChildrenChange())
            {
                DoScaling();
            }
        }

        private bool DidChildrenChange()
        {
            foreach (var containerInfo in _containerInfos)
            {
                var containerTransform = containerInfo.Transform;
                if (containerTransform && containerTransform.childCount == containerInfo.ChildCount)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private void DoScaling()
        {
            if (IsGameplayScene())
            {
                return;
            }

            RebuildScaleSnapshot();
            ApplyScaleToTexts();
        }

        private void RebuildScaleSnapshot()
        {
            var previousInfoByText = new Dictionary<TMP_Text, TextFontInfo>(_fontInfoByText);
            _fontInfoByText.Clear();

            CaptureTextInfo(previousInfoByText);
            CaptureContainerInfo();
            ApplyContainerSizeOverrides();
        }

        private void CaptureTextInfo(Dictionary<TMP_Text, TextFontInfo> previousInfoByText)
        {
            // Set up baseline and max font size using defaults.
            foreach (var text in FindAllTexts())
            {
                bool hasPreviousInfo = previousInfoByText.TryGetValue(text, out var previousInfo);
                bool usesAutoSizing = hasPreviousInfo ? previousInfo.UsesAutoSizing : text.enableAutoSizing;
                float baselineFontSize = hasPreviousInfo
                    ? previousInfo.BaselineFontSize
                    : usesAutoSizing ? text.fontSizeMax : text.fontSize;
                float defaultFullScaleSize = Mathf.Max(DEFAULT_FULL_SCALE_SIZE, baselineFontSize);
                _fontInfoByText[text] = new TextFontInfo
                {
                    BaselineFontSize = baselineFontSize,
                    MaxSize = defaultFullScaleSize,
                    UsesAutoSizing = usesAutoSizing,
                };
            }
        }

        private void CaptureContainerInfo()
        {
            _containerInfos.Clear();
            foreach (var transform in FindAllTransforms())
            {
                if (!ContainerFullScaleSize.TryGetValue(transform.name, out float fullScaleSize))
                {
                    continue;
                }

                _containerInfos.Add(new ContainerInfo
                {
                    Transform = transform,
                    FullScaleSize = fullScaleSize,
                    ChildCount = transform.childCount,
                    HierarchyDepth = GetDepth(transform),
                });
            }

            // Process from shallowest to deepest container so child containers override parents.
            _containerInfos.Sort((left, right) => left.HierarchyDepth.CompareTo(right.HierarchyDepth));
        }

        private void ApplyContainerSizeOverrides()
        {
            foreach (var containerInfo in _containerInfos)
            {
                var containerTransform = containerInfo.Transform;
                if (!containerTransform)
                {
                    continue;
                }

                var textsInContainer = containerTransform.GetComponentsInChildren<TMP_Text>(true);
                foreach (var text in textsInContainer)
                {
                    if (!_fontInfoByText.TryGetValue(text, out var textFontInfo))
                    {
                        continue;
                    }

                    float finalFullScaleSize = Mathf.Max(
                        textFontInfo.BaselineFontSize,
                        containerInfo.FullScaleSize
                    );

                    textFontInfo.MaxSize = finalFullScaleSize;
                    _fontInfoByText[text] = textFontInfo;
                }
            }
        }

        private static int GetDepth(Transform transform)
        {
            int depth = 0;
            while (transform.parent)
            {
                depth++;
                transform = transform.parent;
            }

            return depth;
        }

        private Transform[] FindAllTransforms()
        {
            return FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private TMP_Text[] FindAllTexts()
        {
            return FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private void ApplyScaleToTexts()
        {
            float scaledSize = _fontScaleFactor * MaxFullScaleSize;

            foreach ((var text, TextFontInfo textFontInfo) in _fontInfoByText)
            {
                bool isMissingText = !text;
                if (isMissingText)
                {
                    continue;
                }

                float clampedFontSize = Mathf.Clamp(
                    scaledSize,
                    textFontInfo.BaselineFontSize,
                    textFontInfo.MaxSize
                );

                if (textFontInfo.UsesAutoSizing)
                {
                    text.fontSizeMax = clampedFontSize;
                }
                else
                {
                    text.fontSize = clampedFontSize;
                }
                text.ForceMeshUpdate();
            }
        }

        public void SetFontScalePercent(float percent)
        {
            enabled = _fontScaleFactor > 0f;
            _fontScaleFactor = Mathf.Clamp01(percent / 100f);
            ApplyScaleToTexts();
        }

        private void OnValidate()
        {
            ApplyScaleToTexts();
        }

        private static bool IsGameplayScene()
        {
            return SceneManager.GetActiveScene().name == "Gameplay";
        }
    }
}
