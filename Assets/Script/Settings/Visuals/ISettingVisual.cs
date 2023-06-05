namespace YARG.Settings.Visuals {
	public interface ISettingVisual {
		public string SettingName { get; }

		public void SetSetting(string name);
		public void RefreshVisual();
	}
}