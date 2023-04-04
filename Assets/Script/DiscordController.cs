using System;
using Discord;
using UnityEngine;

public class DiscordController : MonoBehaviour {
	public static DiscordController Instance {
		get;
		private set;
	}

	private Discord.Discord discord;

	public Activity CurrentActivity {
		set {
			if (discord == null) {
				return;
			}

			discord.GetActivityManager().UpdateActivity(value, result => {
				if (result != Result.Ok) {
					Debug.LogWarning("Discord Activity Error: " + result);
				}
			});
		}
	}

	[SerializeField]
	private long applicationID = 1091177744416637028;

	// At the moment there is no changing of menus, what song is playing, etc. Basic for now.
	[Space]
	[SerializeField]
	private string details = "Hello there ladies and gentlemen!";

	[SerializeField]
	private string state = "Are you ready to rock?";

	// only shown in 'view profile' -> 'activity'
	[Space]
	[SerializeField]
	private string largeImage = "logo";

	// tooltip for largeimage
	[SerializeField]
	private string largeText = "Yet Another Rhythm Game";

	// Current instrument icon? only shown in 'view profile' -> 'activity' as a little overlay on the bottom right of the Large image - currently unused.
	[SerializeField]
	private string smallImage = "";

	// Tooltip for smallImage - currently unused.
	[SerializeField]
	private string smallText = "";

	private void Start() {
		Instance = this;

		// Create the Discord instance
		try {
			discord = new Discord.Discord(applicationID, (ulong) CreateFlags.NoRequireDiscord);
		} catch (Exception e) {
			Debug.LogWarning("Failed to start Discord presence client.");
			Debug.LogException(e);

			discord = null;
			return;
		}

		// Start the activity
		long elaspedPlayTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
		CurrentActivity = new Activity {
			Details = details,
			State = state,
			Assets = {
				LargeImage = largeImage,
				LargeText = largeText
			},
			Timestamps = {
				Start = elaspedPlayTime,
			}
		};
	}

	private void Update() {
		if (discord == null) {
			return;
		}

		try {
			discord.RunCallbacks();
		} catch (Exception e) {
			Debug.LogException(e);

			TryDispose();
		}
	}

	private void TryDispose() {
		if (discord == null) {
			return;
		}

		try {
			discord.GetActivityManager().ClearActivity((result) => { });
			discord.Dispose();
			discord = null;
		} catch (Exception e) {
			Debug.Log("Failed to clear activity or dispose of Discord.");
			Debug.LogException(e);
		}
	}

	private void OnApplicationQuit() {
		TryDispose();
	}
}