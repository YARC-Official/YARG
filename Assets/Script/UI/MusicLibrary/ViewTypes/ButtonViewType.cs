using System;

namespace YARG.UI.MusicLibrary.ViewTypes {
	public class ButtonViewType : ViewType {
		public override BackgroundType Background => BackgroundType.Category;

		public override string PrimaryText => _primary;

		private string _primary;
		private Action _buttonAction;

		public ButtonViewType(string primary, Action buttonAction) {
			_primary = primary;
			_buttonAction = buttonAction;
		}

		public override void PrimaryButtonClick() {
			_buttonAction?.Invoke();
		}
	}
}