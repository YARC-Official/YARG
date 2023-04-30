using System.Collections.Generic;

namespace YARG.Metadata {
	public class ButtonRowMetadata : AbstractMetadata {
		public List<string> Buttons { get; private set; }

		public ButtonRowMetadata(List<string> buttons) {
			Buttons = buttons;
		}

		public ButtonRowMetadata(string button) {
			Buttons = new() {
				button
			};
		}
	}
}