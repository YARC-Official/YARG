using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace YARG.UI.MusicLibrary.ViewTypes {
	public class ButtonViewType : ViewType {
		public override BackgroundType Background => BackgroundType.Category;

		public override string PrimaryText => $"<color=white>{_primary}</color>";

		public override Sprite IconSprite => Addressables.LoadAssetAsync<Sprite>(_iconPath).WaitForCompletion();

		private string _primary;
		private string _iconPath;
		private Action _buttonAction;

		public ButtonViewType(string primary, string iconPath, Action buttonAction) {
			_primary = primary;
			_iconPath = iconPath;
			_buttonAction = buttonAction;
		}

		public override void PrimaryButtonClick() {
			_buttonAction?.Invoke();
		}
	}
}