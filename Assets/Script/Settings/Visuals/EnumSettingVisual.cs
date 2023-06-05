using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using YARG.Settings.Types;

namespace YARG.Settings.Visuals {
	public class EnumSettingVisual : AbstractSettingVisual<EnumSetting> {
		[SerializeField]
		private TMP_Dropdown _dropdown;

		private readonly List<int> _enumNamesToIndexCache = new();

		protected override void OnSettingInit() {
			RefreshVisual();
		}

		public override void RefreshVisual() {
			// Add the options (in order), and get enum indices
			_enumNamesToIndexCache.Clear();
			_dropdown.options.Clear();
			foreach (var name in Enum.GetNames(Setting.EnumType)) {
				_enumNamesToIndexCache.Add((int) Enum.Parse(Setting.EnumType, name));

				_dropdown.options.Add(new(new LocalizedString {
					TableReference = "Settings",
					TableEntryReference = $"Enum.{name}"
				}.GetLocalizedString()));
			}

			// Select the right option
			_dropdown.SetValueWithoutNotify(_enumNamesToIndexCache[Setting.Data]);
		}

		public void OnDropdownChange() {
			Setting.Data = _enumNamesToIndexCache[_dropdown.value];
			RefreshVisual();
		}
	}
}