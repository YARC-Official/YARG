using System;
using System.Collections.Generic;
using Discord;
using Unity.Collections;
using UnityEngine;
using YARG;
using YARG.Data;
using YARG.PlayMode;
using YARG.Song;
using YARG.UI;

public class DiscordController : MonoBehaviour {
	/*
	TODO:
	Determine if discord is installed at all on windows, linux and mac. Don't bother running otherwise (doubt anyone would install it DURING gameplay lol)
	Reconnect if discord is closed and reopened/opened during game (and not memory leak the game into a crash)

	Unsure if possible:
	If Yarg crashes the discord presences will stay

	Impossible at the moment:
	Display Album art with little icon overlay - Currently only possible with the api's art assets or a url, NOT with a local image file
	Progress bar
	Pausing the timer (instead of blanking it)
	Other specalized things that only very popular apps can do (like spotify and fortnite)
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

	[System.Serializable]
	private struct distinctDetails {
		public string defaultDetails;
		public string defaultState;
		public string defaultLargeImage;
		public string defaultLargeText;
	}
	
	private distinctDetails defaultDetails;

	[SerializeField]
	private GameObject songStartObject;

	[SerializeField]
	private long applicationID = 1091177744416637028;

	[Space]
	[Header("Default Values")]
	[SerializeField]
	private distinctDetails stableDetails = new distinctDetails {
		defaultDetails = "Hello there ladies and gentlemen!",
		defaultState = "Are you ready to rock?",
		defaultLargeImage = "icon_stable",
		defaultLargeText = "Yet Another Rhythm Game"
	};
	[SerializeField]
	private distinctDetails nightlyDetails = new distinctDetails {
		defaultDetails = "Hello there ladies and gentlemen!",
		defaultState = "Are you ready to test?",
		defaultLargeImage = "icon_nightly",
		defaultLargeText = "Yet Another Rhythm Game - Nightly Build"
	};
	[SerializeField]
	private distinctDetails devDetails = new distinctDetails {
		defaultDetails = "Hello there ladies and gentlemen!",
		defaultState = "Are you ready to develop?",
		defaultLargeImage = "icon_dev",
		defaultLargeText = "Yet Another Rhythm Game - Developer Build"
	};
	[SerializeField]
	private string defaultSmallImage = ""; // little overlay image on the bottom right of the Large image
	[SerializeField]
	private string defaultSmallText = ""; // Tooltip for smallImage

	[Space]
	[Header("Current Values")]
	[SerializeField]
	private string currentDetails; //Line of text just below the game name
	[SerializeField]
	private string currenttState; //smaller 2nd line of text
	private string currentLargeImage; // string name comes from the assets uploaded to the app
	[SerializeField]
	private string currentLargeText; //Tooltip text for the large icon
	[SerializeField]
	private string currentSmallImage; // little overlay on the bottom right of the small image
	[SerializeField]
	private string currentSmallText; // Tooltip for smallImage

	private string songName;
	private string artistName;

	// A bunch of time handling stuff
	private long gameStartTime; //start of YARG, doesn't stop
	private float songLengthSeconds; //Length of song in seconds (not milliseconds)


	private void Start() {
		Instance = this;

		// if it's a Nightly build, use the Nightly logo, otherwise use the Stable logo
		if (Constants.VERSION_TAG.beta) {
			defaultDetails = nightlyDetails;
		} else {
			defaultDetails = stableDetails;
		}

		// if it's running in the editor, use the Dev logo
		#if UNITY_EDITOR
			defaultDetails = devDetails;
		#endif

		

		// Listen to the changing of songs
		Play.OnSongStart += OnSongStart;
		Play.OnSongEnd += OnSongEnd;

		// Listen to instrument selection
		DifficultySelect.OnInstrumentSelection += OnInstrumentSelection;

		// Listen to pausing
		Play.OnPauseToggle += OnPauseToggle;

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
		// Not sure why the discord controller would ever be destroyed but, eh you never know, right?
		Play.OnSongStart -= OnSongStart;
		Play.OnSongEnd -= OnSongEnd;
		DifficultySelect.OnInstrumentSelection -= OnInstrumentSelection;
		Play.OnPauseToggle -= OnPauseToggle;
	}

	private void OnPauseToggle(bool pause) {
		SetActivity(
			// State data
			pause ? "pause1" : currentSmallImage,
			pause ? "Paused" : currentSmallText,
			
			// Song data
			songName,
			"by " + artistName,

			// Time data
			pause ? 0 : DateTimeOffset.Now.ToUnixTimeMilliseconds(),
			pause ? 0 : DateTimeOffset.Now.AddSeconds(Play.Instance.SongLength- Play.Instance.SongTime).ToUnixTimeMilliseconds()
		);
	}

	private void OnSongStart(SongEntry song) {
		songLengthSeconds = song.SongLengthTimeSpan.Seconds;
		songName = song.Name;
		artistName = song.Artist;
		SetActivity(
			currentSmallImage,
			currentSmallText,
			songName,
			"by " + artistName,
			DateTimeOffset.Now.ToUnixTimeMilliseconds(),
			DateTimeOffset.Now.AddSeconds(Play.Instance.SongLength- Play.Instance.SongTime).ToUnixTimeMilliseconds()
		);
	}

	private void OnSongEnd(SongEntry song) {
		SetDefaultActivity();
	}

	private void OnInstrumentSelection(YARG.PlayerManager.Player playerInfo) {
		// ToLowerInvariant() because the DISCORD API DOESN'T HAVE UPPERCASE ARTWORK NAMES (WHY)
		currentSmallImage = playerInfo.chosenInstrument.ToLowerInvariant();
		
#pragma warning disable format
		
		currentSmallText = playerInfo.chosenInstrument switch {
			"vocals"     => "Belting one out",
			"harmVocals" => "Belting one out, with friends!",
			"drums"      => "Working the skins",
			"realDrums"  => "Really working the skins",
			"ghDrums"    => "Working the skins +1",
			"guitar"     => "Making it talk",
			"guitarCoop" => "GTR_COOP_PLACEHOLDER",
			"rhythm"     => "RHYTHM_PLACEHOLDER",
			"realGuitar" => "Really making it talk",
			"bass"       => "In the groove",
			"realBass"   => "Really in the groove",
			"keys"       => "Tickling the ivory",
			"realKeys"   => "Really tickling the ivory",
			_            => ""
		};

#pragma warning restore format
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
				LargeImage = defaultDetails.defaultLargeImage,
				LargeText = defaultDetails.defaultLargeText,
				SmallImage = defaultSmallImage,
				SmallText = defaultSmallText
			},
			Details = defaultDetails.defaultDetails,
			State = defaultDetails.defaultState,
			Timestamps = {
				Start = gameStartTime
			}
		};
	}

	private void SetActivity(string smallImage, string smallText, string details, string state, long startTimeStamp, long endTimeStamp) {
		CurrentActivity = new Activity {
			Assets = {
				LargeImage = defaultDetails.defaultLargeImage, //the YARG logo and tooltip does not change, at this point in time.
				LargeText = defaultDetails.defaultLargeText,
				SmallImage = smallImage,
				SmallText = smallText,
			},
			Details = details,
			State = state,
			Timestamps = {
				Start = startTimeStamp,
				End = endTimeStamp,
			}
		};
	}
}
