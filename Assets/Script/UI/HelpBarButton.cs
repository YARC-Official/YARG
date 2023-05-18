using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

using YARG.Input;

namespace YARG.UI {
    [ExecuteInEditMode]
    public class HelpBarButton : MonoBehaviour {

        [SerializeField]
        private NavigationType navigationType; // YARG.Core.Input.InputActions.MenuAction
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
                NavigationType.PRIMARY => GREEN,
                NavigationType.SECONDARY => RED,
                NavigationType.TERTIARY => YELLOW,
                _ => Color.white
			};

            // TODO: controller types
			tmpButton.text = navigationType switch {
                NavigationType.PRIMARY => "A",
                NavigationType.SECONDARY => "B",
                NavigationType.TERTIARY => "Y",
                _ => ""
			};
		}
    }
}
