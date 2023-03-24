using TMPro;
using UnityEngine;

namespace YARG.Settings {
	public abstract class AbstractSetting : MonoBehaviour {
		[SerializeField]
		private TextMeshProUGUI settingText;

		protected string settingName;

		public void Setup(string text, string settingName) {
			settingText.text = text;
			this.settingName = settingName;
			OnSetup();
		}

		protected virtual void OnSetup() {

		}
	}
}