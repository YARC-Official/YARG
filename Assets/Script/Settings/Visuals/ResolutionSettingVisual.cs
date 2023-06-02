using System.Collections.Generic;
using TMPro;
using UnityEngine;
using YARG.Settings.Types;

namespace YARG.Settings.Visuals {
	public class ResolutionSettingVisual : AbstractSettingVisual<ResolutionSetting> {
		[SerializeField]
		private TMP_Dropdown _dropdown;

		private readonly List<Resolution> _resolutionCache = new();

		protected override void OnSettingInit() {
			RefreshVisual();
		}

		public override void RefreshVisual() {
			// Get the possible resolutions
			_resolutionCache.Clear();
			foreach (var resolution in Screen.resolutions) {
				_resolutionCache.Add(resolution);
			}

			// Add the options (in order)
			_dropdown.options.Clear();
			_dropdown.options.Add(new("<i>Highest</i>"));
			foreach (var resolution in _resolutionCache) {
				_dropdown.options.Add(new(resolution.ToString()));
			}

			// Select the right option
			if (Setting.Data == null) {
				_dropdown.SetValueWithoutNotify(0);
			} else {
				_dropdown.SetValueWithoutNotify(_resolutionCache.IndexOf(Setting.Data.Value) + 1);
			}
		}

		public void OnDropdownChange() {
			if (_dropdown.value == 0) {
				Setting.Data = null;
			} else {
				Setting.Data = _resolutionCache[_dropdown.value - 1];
			}

			RefreshVisual();
		}
	}
}