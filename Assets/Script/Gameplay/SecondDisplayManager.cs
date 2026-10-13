using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Localization;
using YARG.Settings;

namespace YARG.Venue
{
    /// <summary>
    /// Persistent second-monitor output: a waiting slideshow until a song video plays.
    /// </summary>
    public sealed class SecondDisplayManager : MonoSingleton<SecondDisplayManager>
    {
        public const int DisplayIndex = 1;

        private const float SlideshowInterval = 30f;
        private const float FadeDuration = 1f;

        private static readonly string[] ImageExtensions =
        {
            "*.png", "*.jpg", "*.jpeg"
        };

        public static string WaitingFolder
        {
            get
            {
                var folder = Path.Combine(PathHelper.PersistentDataPath, "waiting");
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                return folder;
            }
        }

        public static bool IsReady => Instance != null && Instance._ready;

        private static int _mainThreadId;

        private CanvasGroup _waitingGroup;
        private CanvasGroup _videoGroup;
        private RawImage _imageA;
        private RawImage _imageB;
        private RawImage _videoImage;
        private TextMeshProUGUI _waitingText;

        private readonly List<string> _imagePaths = new();
        private Texture2D _textureA;
        private Texture2D _textureB;
        private bool _showingA = true;
        private bool _showingVideo;
        private bool _ready;
        private bool _fading;
        private int _fadeVersion;
        private int _currentPathIndex = -1;
        private float _slideshowTimer;

        public static void TryInitialize()
        {
            if (!SettingsManager.Settings.SendVideoBackgroundToSecondDisplay.Value)
            {
                if (Instance != null)
                {
                    Instance.gameObject.SetActive(false);
                }

                return;
            }

            if (Instance != null)
            {
                Instance.gameObject.SetActive(true);
                Instance.ShowWaiting();
                return;
            }

            if (!TryActivateDisplay())
            {
                return;
            }

            var go = new GameObject(nameof(SecondDisplayManager));
            DontDestroyOnLoad(go);
            go.AddComponent<SecondDisplayManager>();
        }

        public static bool TryActivateDisplay()
        {
            if (Display.displays.Length <= DisplayIndex)
            {
                return false;
            }

            var display = Display.displays[DisplayIndex];
            if (!display.active)
            {
                display.Activate();
            }

            return display.active;
        }

        protected override void SingletonAwake()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            BuildUi();
            _ready = true;
            ShowWaiting();
            YargLogger.LogInfo("Second display waiting screen started");
        }

        protected override void SingletonDestroy()
        {
            DestroyTexture(ref _textureA);
            DestroyTexture(ref _textureB);
            _ready = false;
        }

        private void Update()
        {
            if (!_ready || _showingVideo || _fading || _imagePaths.Count < 2)
            {
                return;
            }

            _slideshowTimer += Time.unscaledDeltaTime;
            if (_slideshowTimer >= SlideshowInterval)
            {
                _slideshowTimer = 0f;
                FadeToNextImage().Forget();
            }
        }

        public void ShowVideo(Texture texture, float aspectRatio)
        {
            if (!_ready || !IsMainThread() || texture == null || aspectRatio <= 0f)
            {
                return;
            }

            _fadeVersion++;
            _fading = false;
            _showingVideo = true;

            _videoImage.texture = texture;
            SetAspect(_videoImage, aspectRatio);

            _videoGroup.alpha = 1f;
            _waitingGroup.alpha = 0f;
        }

        public void ShowWaiting()
        {
            if (!_ready || !IsMainThread())
            {
                return;
            }

            _fadeVersion++;
            _fading = false;
            _showingVideo = false;
            _slideshowTimer = 0f;

            _videoImage.texture = null;
            _videoGroup.alpha = 0f;
            _waitingGroup.alpha = 1f;
            RefreshWaitingText();
            RefreshImagePaths();
            ShowImageImmediate(PickNextPath());
        }

        private void BuildUi()
        {
            var cameraObject = new GameObject("Second Display Camera");
            cameraObject.transform.SetParent(transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;
            camera.depth = -100;
            camera.orthographic = true;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.targetDisplay = DisplayIndex;

            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderType = CameraRenderType.Base;
            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.None;

            var canvasObject = new GameObject("Second Display Canvas");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.targetDisplay = DisplayIndex;
            canvas.sortingOrder = 0;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            var letterbox = CreateFullScreenChild(canvasObject.transform, "Letterbox");
            var letterboxImage = letterbox.AddComponent<Image>();
            letterboxImage.color = Color.black;
            letterboxImage.raycastTarget = false;

            var waitingRoot = CreateFullScreenChild(canvasObject.transform, "Waiting");
            _waitingGroup = waitingRoot.AddComponent<CanvasGroup>();
            _waitingGroup.blocksRaycasts = false;
            _waitingGroup.interactable = false;

            _imageA = CreateFitImage(waitingRoot.transform, "Image A");
            _imageB = CreateFitImage(waitingRoot.transform, "Image B");
            _imageB.color = new Color(1f, 1f, 1f, 0f);

            var textBar = CreateChild(waitingRoot.transform, "Text Bar");
            var textBarRect = textBar.GetComponent<RectTransform>();
            textBarRect.anchorMin = new Vector2(0f, 0f);
            textBarRect.anchorMax = new Vector2(1f, 0.18f);
            textBarRect.offsetMin = Vector2.zero;
            textBarRect.offsetMax = Vector2.zero;
            var textBarImage = textBar.AddComponent<Image>();
            textBarImage.color = new Color(0f, 0f, 0f, 0.45f);
            textBarImage.raycastTarget = false;

            var textObject = CreateChild(textBar.transform, "Waiting Text");
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            _waitingText = textObject.AddComponent<TextMeshProUGUI>();
            _waitingText.font = TMP_Settings.defaultFontAsset;
            _waitingText.fontSize = 42f;
            _waitingText.fontStyle = FontStyles.SmallCaps;
            _waitingText.alignment = TextAlignmentOptions.Center;
            _waitingText.color = Color.white;
            _waitingText.outlineWidth = 0.2f;
            _waitingText.outlineColor = Color.black;
            _waitingText.raycastTarget = false;
            RefreshWaitingText();

            var videoRoot = CreateFullScreenChild(canvasObject.transform, "Video");
            _videoGroup = videoRoot.AddComponent<CanvasGroup>();
            _videoGroup.alpha = 0f;
            _videoGroup.blocksRaycasts = false;
            _videoGroup.interactable = false;
            _videoImage = CreateFitImage(videoRoot.transform, "Video Image");
        }

        private void RefreshWaitingText()
        {
            if (_waitingText == null)
            {
                return;
            }

            _waitingText.text = LocalizationManager.TryGetLocalizedKey(
                "Gameplay.SecondDisplay.WaitingForVideo", out var localized)
                ? localized
                : "Waiting for video";
        }

        private void RefreshImagePaths()
        {
            _imagePaths.Clear();

            var folder = WaitingFolder;
            foreach (var ext in ImageExtensions)
            {
                _imagePaths.AddRange(Directory.EnumerateFiles(folder, ext, PathHelper.SafeSearchOptions));
            }
        }

        private string PickNextPath()
        {
            if (_imagePaths.Count == 0)
            {
                return null;
            }

            if (_imagePaths.Count == 1)
            {
                _currentPathIndex = 0;
                return _imagePaths[0];
            }

            int index;
            do
            {
                index = Random.Range(0, _imagePaths.Count);
            } while (index == _currentPathIndex);

            _currentPathIndex = index;
            return _imagePaths[index];
        }

        private void ShowImageImmediate(string path)
        {
            var incoming = _showingA ? _imageA : _imageB;
            var outgoing = _showingA ? _imageB : _imageA;
            outgoing.color = new Color(1f, 1f, 1f, 0f);

            if (string.IsNullOrEmpty(path))
            {
                incoming.texture = null;
                incoming.color = new Color(1f, 1f, 1f, 0f);
                return;
            }

            var texture = LoadImage(path);
            if (texture == null)
            {
                incoming.texture = null;
                incoming.color = new Color(1f, 1f, 1f, 0f);
                return;
            }

            AssignTexture(incoming, texture);
            incoming.color = Color.white;
        }

        private async UniTaskVoid FadeToNextImage()
        {
            RefreshImagePaths();
            var path = PickNextPath();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var texture = LoadImage(path);
            if (texture == null)
            {
                return;
            }

            int version = ++_fadeVersion;
            _fading = true;

            var incoming = _showingA ? _imageB : _imageA;
            var outgoing = _showingA ? _imageA : _imageB;
            AssignTexture(incoming, texture);
            incoming.color = new Color(1f, 1f, 1f, 0f);

            float elapsed = 0f;
            while (elapsed < FadeDuration)
            {
                if (version != _fadeVersion || _showingVideo)
                {
                    _fading = false;
                    return;
                }

                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Clamp01(elapsed / FadeDuration);
                incoming.color = new Color(1f, 1f, 1f, alpha);
                outgoing.color = new Color(1f, 1f, 1f, 1f - alpha);
                await UniTask.Yield();
            }

            if (version != _fadeVersion || _showingVideo)
            {
                _fading = false;
                return;
            }

            incoming.color = Color.white;
            outgoing.color = new Color(1f, 1f, 1f, 0f);
            _showingA = !_showingA;
            _fading = false;
        }

        private void AssignTexture(RawImage image, Texture2D texture)
        {
            if (image == _imageA)
            {
                DestroyTexture(ref _textureA);
                _textureA = texture;
            }
            else
            {
                DestroyTexture(ref _textureB);
                _textureB = texture;
            }

            image.texture = texture;
            if (texture != null && texture.height > 0)
            {
                SetAspect(image, (float) texture.width / texture.height);
            }
        }

        private static bool IsMainThread()
        {
            return _mainThreadId != 0 && Thread.CurrentThread.ManagedThreadId == _mainThreadId;
        }

        private static Texture2D LoadImage(string path)
        {
            if (!IsMainThread())
            {
                return null;
            }

            try
            {
                var bytes = File.ReadAllBytes(path);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes))
                {
                    Destroy(texture);
                    return null;
                }

                return texture;
            }
            catch (System.Exception ex)
            {
                YargLogger.LogFormatWarning<string, string>("Failed to load waiting image `{0}`: {1}", path, ex.Message);
                return null;
            }
        }

        private static void SetAspect(RawImage image, float aspectRatio)
        {
            var fitter = image.GetComponent<AspectRatioFitter>();
            if (fitter != null)
            {
                fitter.aspectRatio = aspectRatio;
            }
        }

        private static void DestroyTexture(ref Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            Destroy(texture);
            texture = null;
        }

        private static RawImage CreateFitImage(Transform parent, string name)
        {
            var child = CreateFullScreenChild(parent, name);
            var image = child.AddComponent<RawImage>();
            image.color = Color.white;
            image.raycastTarget = false;

            var fitter = child.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 16f / 9f;
            return image;
        }

        private static GameObject CreateFullScreenChild(Transform parent, string name)
        {
            var child = CreateChild(parent, name);
            var rect = child.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return child;
        }

        private static GameObject CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            var rect = child.AddComponent<RectTransform>();
            rect.pivot = new Vector2(0.5f, 0.5f);
            return child;
        }
    }
}
