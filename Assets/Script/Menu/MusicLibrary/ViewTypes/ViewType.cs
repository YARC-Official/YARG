using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace YARG.UI.MusicLibrary.ViewTypes {
	public abstract class ViewType {
		public enum BackgroundType {
			Normal,
			Category
		}

		public abstract BackgroundType Background { get; }

		public abstract string PrimaryText { get; }
		public virtual string SecondaryText => string.Empty;
		public virtual bool UseAsMadeFamousBy => false;

		public virtual string SideText => string.Empty;

		public virtual async UniTask<Sprite> GetIcon() {
			return null;
		}

		public virtual void SecondaryTextClick() {

		}

		public virtual void PrimaryButtonClick() {

		}

		public virtual void IconClick() {

		}
	}
}