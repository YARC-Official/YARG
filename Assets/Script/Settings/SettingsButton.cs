using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

namespace YARG.Settings {
	public class SettingsButton : MonoBehaviour {
		private string buttonName;
		private Action customCallback;

		[SerializeField]
		private LocalizeStringEvent text;

		public void SetInfo(string buttonName) {
			this.buttonName = buttonName;
			customCallback = null;

			text.StringReference = new LocalizedString {
				TableReference = "Settings",
				TableEntryReference = buttonName
			};
		}

		public void SetCustomCallback(Action action, string localizationKey) {
			buttonName = null;
			customCallback = action;

			text.StringReference = new LocalizedString {
				TableReference = "Settings",
				TableEntryReference = localizationKey
			};
		}

		public void OnClick() {
			if (customCallback == null) {
				SettingsManager.InvokeButton(buttonName);
			} else {
				customCallback.Invoke();
			}
		}
	}
}