using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Input;
using YARG.Localization;
using YARG.Menu;
using YARG.Menu.Data;
using YARG.Menu.Dialogs;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Menu.Settings.Visuals;
using YARG.Settings.Customization;

// pattern: Imperative Shell

namespace YARG.Settings.Metadata
{
    public abstract class PresetSubTab : Tab
    {
        // Keeps default preset fields inspectable without allowing edits; preview
        // controls remain interactive.
        public bool ReadOnlyFields { get; set; }

        // Shared preview visual state across all preset type tabs (Camera, Color,
        // Engine, Highway, RockMeter). Bundled into one object (rather than a
        // handful of loose statics) so there is a single named thing to point at
        // with a documented lifetime: process-lifetime, shared by every
        // PresetSubTab<T>, not persisted to disk.
        protected sealed class PreviewOptionsState
        {
            public bool ForceStarPowerNotes;
            public bool ForceStarPower;
            public bool ForceGroove;
            public bool LeftyFlip;
            public GameMode GameMode = GameMode.FiveFretGuitar;
        }

        protected static readonly PreviewOptionsState PreviewOptions = new();

        private static ColorPresetHeader _colorGroupHeader;
        private static ColorPresetHeader _colorSubHeader;

        // Prefabs needed for this tab type
        private static GameObject _headerPrefab;
        private static GameObject _smallRoundButtonPrefab;

        public static GameObject GetSmallRoundButtonPrefab()
        {
            if (_smallRoundButtonPrefab == null)
            {
                _smallRoundButtonPrefab = Addressables
                    .LoadAssetAsync<GameObject>("Buttons/SmallRoundButton")
                    .WaitForCompletion();
            }
            return _smallRoundButtonPrefab;
        }

        protected static ColorPresetHeader SpawnColorHeader(Transform container, bool subHeader)
        {
            ref var prefab = ref (subHeader ? ref _colorSubHeader : ref _colorGroupHeader);
            if (prefab == null)
            {
                string address = subHeader ? "SettingTab/ColorSubHeader" : "SettingTab/ColorGroupHeader";
                prefab = Addressables.LoadAssetAsync<GameObject>(address)
                    .WaitForCompletion().GetComponent<ColorPresetHeader>();
            }

            return Object.Instantiate(prefab, container);
        }

        public static GameObject GetHeaderPrefab()
        {
            if (_headerPrefab == null)
            {
                _headerPrefab = Addressables
                    .LoadAssetAsync<GameObject>("SettingTab/Header")
                    .WaitForCompletion();
            }
            return _headerPrefab;
        }

        public abstract CustomContent CustomContent { get; }

        protected PresetSubTab(string name, string icon = "Generic", IPreviewBuilder previewBuilder = null)
            : base(name, icon, previewBuilder)
        {
        }

        public abstract void SetPresetReference(object preset);

        protected static GameObject SpawnHeader(Transform container, string unlocalizedText)
        {
            return SpawnRawHeader(container, Localize.Key("Settings.Header", unlocalizedText));
        }

        // Like SpawnHeader but sets the text directly without localization key lookup.
        // Used for group/sub-group labels in the color profile editor.
        public static GameObject SpawnRawHeader(Transform container, string text)
        {
            var go = Object.Instantiate(GetHeaderPrefab(), container);
            go.GetComponentInChildren<TextMeshProUGUI>().text = text;
            return go;
        }

        /// <summary>
        /// Shrinks a confirmation dialog to a compact centered box. The stock
        /// DialogBase panel stretches to screen-minus-600x400, which dwarfs a
        /// one-line message; pin it to a fixed size and tighten the title and
        /// message bands to match. Used by the "Copy from note" and delete-preset
        /// confirmations.
        /// </summary>
        public static void MakeDialogCompact(Dialog dialog)
        {
            if (dialog.transform.Find("Base") is not RectTransform baseRect)
            {
                return;
            }

            baseRect.anchorMin = new Vector2(0.5f, 0.5f);
            baseRect.anchorMax = new Vector2(0.5f, 0.5f);
            baseRect.anchoredPosition = Vector2.zero;
            baseRect.sizeDelta = new Vector2(760f, 280f);

            // Title sits 50px below the top edge by default; pull it up.
            if (baseRect.Find("Title") is RectTransform titleRect)
            {
                titleRect.anchoredPosition = new Vector2(0f, -20f);
            }

            // Message band between the title (top 80) and the buttons (bottom
            // 110; the button strip occupies 50-100).
            if (baseRect.Find("Content") is RectTransform contentRect)
            {
                contentRect.offsetMin = new Vector2(50f, 110f);
                contentRect.offsetMax = new Vector2(-50f, -80f);
            }
        }

        /// <summary>
        /// Shows a compact two-button confirmation dialog (Cancel + confirm) using
        /// the shared <see cref="MakeDialogCompact"/> sizing and the color-picker's
        /// nav convention: Green/confirm runs <paramref name="onConfirm"/>,
        /// Red/back cancels. Returns the confirm <see cref="ColoredButton"/> so the
        /// caller can further tweak it.
        /// </summary>
        /// <remarks>
        /// The base Dialog pushes a navigate-only scheme in OnEnable; this swaps it
        /// for the confirm/cancel scheme. Dialog.OnDisable pops whatever is on top,
        /// so the pop/push stays balanced — this is the one place that convention
        /// lives now that both confirmations share it.
        /// </remarks>
        /// <param name="armDelaySeconds">
        /// If greater than zero, the confirm button starts disabled and arms after
        /// this many seconds. Use for destructive actions (e.g. deletes) so they
        /// can't be confirmed by accidental mashing.
        /// </param>
        public static ColoredButton ShowCompactConfirmation(string title, string message,
            string confirmKey, Color confirmColor, Action onConfirm, Color? cancelColor = null,
            float armDelaySeconds = 0f)
        {
            void Cancel() => DialogManager.Instance.ClearDialog();

            var dialog = DialogManager.Instance.ShowMessage(title, message);
            dialog.ClearButtons();
            dialog.AddDialogButton("Menu.Common.Cancel",
                cancelColor ?? MenuData.Colors.CancelButton, Cancel);
            // AddDialogButton wants a UnityAction; wrap the System.Action so the
            // same delegate can also feed the NavigationScheme.Entry (Action) below.
            var confirmButton = dialog.AddDialogButton(confirmKey, confirmColor, () => onConfirm());

            // The delayed arm only guards the button itself; the armed flag also
            // gates the controller/keyboard Green entry below so it can't bypass
            // the delay through the navigation scheme
            bool armed = armDelaySeconds <= 0f;

            if (armDelaySeconds > 0f)
            {
                ArmButtonAfterDelay(confirmButton, confirmColor, armDelaySeconds,
                    () => armed = true).Forget();
            }

            MakeDialogCompact(dialog);

            Navigator.Instance.PopScheme();
            Navigator.Instance.PushSchemeImmediate(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Green, "Menu.Common.Confirm", () =>
                {
                    if (!armed) return;
                    onConfirm();
                }),
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Cancel", Cancel),
            }, null));

            return confirmButton;
        }

        /// <summary>
        /// Disables the confirm button immediately — fade-free, so it never renders a
        /// frame in its enabled color — and re-enables it, with its color restored,
        /// after the given delay. Guards destructive actions against accidental mashing.
        /// </summary>
        private static async UniTaskVoid ArmButtonAfterDelay(ColoredButton button,
            Color confirmColor, float delaySeconds, Action onArmed)
        {
            // The button's color-tint transition lerps from the enabled color to
            // the disabled one over its fade duration, which flashes the enabled
            // color for a frame; zero the fade while switching
            var uiButton = button.GetComponentInChildren<Button>();
            ColorBlock originalColors = default;
            if (uiButton != null)
            {
                originalColors = uiButton.colors;
                var noFade = originalColors;
                noFade.fadeDuration = 0f;
                uiButton.colors = noFade;
            }

            button.DisableButton();

            await UniTask.Delay((int) (delaySeconds * 1000),
                cancellationToken: button.GetCancellationTokenOnDestroy());

            // The dialog may have been cancelled in the meantime
            if (button == null)
            {
                return;
            }

            button.EnableButton();
            // EnableButton restores the prefab's original color, not the one
            // this button was given
            button.SetBackgroundAndTextColor(confirmColor);

            if (uiButton != null)
            {
                uiButton.colors = originalColors;
            }

            onArmed?.Invoke();
        }


    }
}
