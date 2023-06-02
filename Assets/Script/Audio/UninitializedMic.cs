namespace YARG.Audio {
	public readonly struct UninitializedMic {
		public readonly int    DeviceId;
		public readonly string DisplayName;

		public readonly bool IsDefault;

		public UninitializedMic(int deviceId, string displayName, bool isDefault) {
			DeviceId = deviceId;
			DisplayName = displayName;
			IsDefault = isDefault;
		}
	}
}