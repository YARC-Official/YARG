using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Input;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Menu.Settings;
using YARG.Settings;
using YARG.Settings.Types;

namespace YARG.Menu.Settings.Visuals
{
    public abstract class BaseSettingVisual : MonoBehaviour
    {
        protected static readonly NavigationScheme.Entry NavigateFinish = new(MenuAction.Red, "Menu.Common.Confirm", () =>
        {
            Navigator.Instance.PopScheme();
        });

        [SerializeField]
        protected TextMeshProUGUI _settingLabel;

        public TextMeshProUGUI SettingLabel => _settingLabel;

        [SerializeField]
        protected GameObject _evenBackground;

        [SerializeField]
        private GameObject _advancedMarker;

        public bool IsPresetSetting { get; private set; }
        public bool HasDescription { get; private set; }
        public bool IsEditable { get; private set; } = true;
        public string UnlocalizedName { get; private set; }

        public void AssignSetting(string settingName, bool hasDescription)
        {
            IsPresetSetting = false;
            HasDescription = hasDescription;
            UnlocalizedName = settingName;

            _settingLabel.text = Localize.Key("Settings.Setting", settingName, "Name");

            AssignSettingFromVariable(SettingsManager.GetSettingByName(settingName));

            OnSettingInit();
        }

        public void AssignPresetSetting(string unlocalizedName, bool hasDescription, ISettingType reference)
        {
            IsPresetSetting = true;
            HasDescription = hasDescription;
            UnlocalizedName = unlocalizedName;

            _settingLabel.text = Localize.Key("Settings.PresetSetting", unlocalizedName, "Name");

            AssignSettingFromVariable(reference);

            OnSettingInit();

            // Subscribe now if the visual is currently active. OnEnable ran
            // before this assignment (during AddComponent/Instantiate), when
            // IsPresetSetting was still false, so it skipped subscribing.
            if (isActiveAndEnabled)
            {
                SubscribeToSettingChanged();
            }
        }

        public virtual void AssignIndex(int index)
        {
            _evenBackground.SetActive(index % 2 == 0);
        }

        public void ShowAdvancedMarker(bool show)
        {
            if (_advancedMarker != null)
            {
                _advancedMarker.SetActive(show);
            }
        }

        public virtual void SetEditable(bool editable, bool dim = true)
        {
            IsEditable = editable;
            var canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            canvasGroup.alpha = !editable && dim ? 0.5f : 1f;
            canvasGroup.interactable = editable;
            canvasGroup.blocksRaycasts = true;

            foreach (var selectable in GetComponentsInChildren<Selectable>(true))
            {
                selectable.interactable = editable;
            }
        }

        /// <summary>
        /// Hides the setting label text and divider line. Useful when a setting
        /// visual is placed in a context where these decorations are redundant
        /// (e.g., the instrument dropdown in the preview header).
        /// </summary>
        public void HideLabel()
        {
            if (_settingLabel != null)
            {
                _settingLabel.gameObject.SetActive(false);
            }

            // Hide the divider line (child of the BaseSetting prefab root)
            var divider = transform.Find("Divider");
            if (divider != null)
            {
                divider.gameObject.SetActive(false);
            }

            // Reclaim the label's reserved width. LabledSetting-based prefabs
            // inset the value Container by a fixed 325px on the left for the
            // label; in narrow contexts (e.g. the preview header row) that
            // exceeds the whole row width and the control collapses.
            if (transform.Find("Container") is RectTransform container)
            {
                container.offsetMin = new Vector2(10f, container.offsetMin.y);
                container.offsetMax = new Vector2(-10f, container.offsetMax.y);
            }
        }

        protected abstract void AssignSettingFromVariable(ISettingType reference);

        /// <summary>
        /// Whether the <see cref="Setting"/> has been assigned. Used by the
        /// event-driven refresh guard. The generic subclass overrides this.
        /// </summary>
        protected abstract bool HasSettingAssigned { get; }

        protected virtual void OnSettingInit()
        {
            RefreshVisual();
        }

        /// <summary>
        /// Re-subscribes to the setting-changed event when the visual becomes
        /// active (or re-activates after a panel toggle). Only preset visuals
        /// need this sync (for overlapping color fields); regular settings
        /// don't overlap, so they skip the global event to avoid unnecessary
        /// re-renders.
        /// </summary>
        protected virtual void OnEnable()
        {
            if (IsPresetSetting)
            {
                SubscribeToSettingChanged();
            }
        }

        // Idempotent subscribe: the -= before += means a re-assign or re-enable
        // never stacks a second handler, so OnDisable's single -= always fully
        // unsubscribes. Callers own their own precondition (active / preset).
        private void SubscribeToSettingChanged()
        {
            if (SettingsMenu.Instance == null)
            {
                return;
            }

            SettingsMenu.Instance.SettingChanged -= OnAnySettingChanged;
            SettingsMenu.Instance.SettingChanged += OnAnySettingChanged;
        }

        protected virtual void OnDisable()
        {
            if (SettingsMenu.Instance != null)
            {
                SettingsMenu.Instance.SettingChanged -= OnAnySettingChanged;
            }
        }

        private void OnAnySettingChanged()
        {
            // Guard: the event can fire before/after Setting is assigned.
            if (!HasSettingAssigned) return;

            RefreshVisual();
        }

        public abstract void RefreshVisual();

        public abstract NavigationScheme GetNavigationScheme();
    }

    public abstract class BaseSettingVisual<T> : BaseSettingVisual where T : ISettingType
    {
        protected T Setting { get; private set; }

        protected sealed override bool HasSettingAssigned => Setting != null;

        protected sealed override void AssignSettingFromVariable(ISettingType reference)
        {
            Setting = (T) reference;
        }
    }
}
