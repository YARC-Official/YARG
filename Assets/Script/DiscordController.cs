using System;
using Discord;
using UnityEngine;
using YARG.Data;
using YARG.PlayMode;

public class DiscordController : MonoBehaviour {
	/*
	TODO:
	Reconnect if discord is closed and reopened/opened during game (and not memory the game into a crash)
	Display Album art with little icon overlay
	*/

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
	private GameObject songStartObject;

	[SerializeField]
	private long applicationID = 1091177744416637028;

	[Space]
	[Header("Default Values")]
	[SerializeField]
	private string defaultDetails = "Hello there ladies and gentlemen!"; //Line of text just below the game name
	[SerializeField]
	private string defaultState = "Are you ready to rock?"; //smaller 2nd line of text
	private string defaultLargeImage = "logo"; // only shown in 'view profile' -> 'activity'
	[SerializeField]
	private string defaultLargeText = "Yet Another Rhythm Game"; //Top large line of text
	[SerializeField]
	private string defaultSmallImage = ""; // only shown in 'view profile' -> 'activity' as a little overlay on the bottom right of the Large image - currently unused.
	[SerializeField]
	private string defaultSmallText = ""; // Tooltip for smallImage - currently unused.
	/* Not used for anything at the moment. Remind me to remove this later :)
		[Space]
		[Header("Current Values")]
		[SerializeField]
		private string currentDetails; //Line of text just below the game name
		[SerializeField]
		private string currenttState; //smaller 2nd line of text
		private string currentLargeImage; // only shown in 'view profile' -> 'activity'
		[SerializeField]
		private string currentLargeText; //Top large line of text
		[SerializeField]
		private string currentSmallImage; // only shown in 'view profile' -> 'activity' as a little overlay on the bottom right of the Large image - currently unused.
		[SerializeField]
		private string currentSmallText; // Tooltip for smallImage - currently unused.
	*/
	private long gameStartTime;

	private void Start() {
		Instance = this;

		// Listen to the changing of songs
		Play.OnSongStart += OnSongStart;
		Play.OnSongEnd += OnSongEnd;

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
		gameStartTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

		// Set default activity
		SetDefaultActivity();
	}

	private void OnDestroy() {
		// Not sure why the discord controller would ever be disabled but, eh you never know, right?
		Play.OnSongStart -= OnSongStart;
		Play.OnSongEnd -= OnSongEnd;
	}

	private void OnSongStart(SongInfo song) {
		CurrentActivity = new Activity {
			Assets = {
				LargeImage = defaultLargeImage,
				LargeText = defaultLargeText
			},
			Details = song.SongName,
			State = "by " + song.artistName,
			Timestamps = {
				Start = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
				End = DateTimeOffset.Now.AddSeconds(song.songLength).ToUnixTimeMilliseconds(),
			}
		};
	}

	private void OnSongEnd(SongInfo song) {
		SetDefaultActivity();
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

	private void SetDefaultActivity() {
		CurrentActivity = new Activity {
			Assets = {
				LargeImage = defaultLargeImage,
				LargeText = defaultLargeText
			},
			Details = defaultDetails,
			State = defaultState,
			Timestamps = {
				Start = gameStartTime
			}
		};
	}
}
