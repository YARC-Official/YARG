namespace YARG.UI.MusicLibrary.ViewTypes {
	public class CategoryViewType : ViewType {
		public override BackgroundType Background => BackgroundType.Category;

		public override string PrimaryText => _primary;
		public override string SideText => _side;

		private string _primary;
		private string _side;

		public CategoryViewType(string primary, string side) {
			_primary = primary;
			_side = side;
		}
	}
}