namespace YARG.Audio {
	public readonly struct UninitializedMic {
		public readonly int    DeviceId;
		public readonly string DisplayName;

		public UninitializedMic(int deviceId, string displayName) {
			DeviceId = deviceId;
			DisplayName = displayName;
		}
	}
}