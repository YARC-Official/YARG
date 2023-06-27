using UnityEngine;

namespace YARG.UI {
	public class Socials : MonoBehaviour {
		public void OpenTwitter() {
			Application.OpenURL("https://twitter.com/EliteAsian123");
		}

		public void OpenDiscord() {
			Application.OpenURL("https://discord.gg/sqpu4R552r");
		}

		public void OpenGithub() {
			Application.OpenURL("https://github.com/EliteAsian123/YARG");
		}

		public void OpenPatreon() {
			Application.OpenURL("https://www.patreon.com/YARG_Official");
		}
	}
}