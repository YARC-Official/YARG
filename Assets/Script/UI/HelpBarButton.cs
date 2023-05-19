using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Input;

namespace YARG.UI {
	[ExecuteInEditMode]
	public class HelpBarButton : MonoBehaviour {

		[SerializeField]
		private MenuAction navigationType;
		[SerializeField]
		private string description;

		[Header("GameObjects")]
		[SerializeField]
		private Image icon;
		[SerializeField]
		private TextMeshProUGUI tmpButton;
		[SerializeField]
		private TextMeshProUGUI tmpDescription;

		private readonly Color GREEN = new Color(0.240566f, 1f, 0.3107658f);
		private readonly Color RED = new Color(1f, 0.2470681f, 0.2392157f);
		private readonly Color YELLOW = new Color(1f, 0.8784314f, .08627451f);

		void OnEnable() {
			// TODO: animate?
		}

		void Update() {
			tmpDescription.text = description;
			icon.color = navigationType switch {
				MenuAction.Confirm => GREEN,
				MenuAction.Back => RED,
				MenuAction.Shortcut1 => YELLOW,
				_ => Color.white
			};

			// TODO: controller types
			tmpButton.text = navigationType switch {
				MenuAction.Confirm => "A",
				MenuAction.Back => "B",
				MenuAction.Shortcut1 => "Y",
				_ => ""
			};
		}
	}
}