using System;
using Discord;
using UnityEngine;
using YARG.Data;
using YARG.PlayMode;
using YARG.UI;

public class DiscordController : MonoBehaviour {
	/*
	TODO:
	Determine if discord is installed at all on windows, linux and mac. Don't bother running otherwise (doubt anyone would install it DURING gameplay lol)
	Reconnect if discord is closed and reopened/opened during game (and not memory leak the game into a crash)
	Need icons for pro drums, 5 lane drums, pro bass, pro guitars, pro keys

	Impossible at the moment:
	Display Album art with little icon overlay - Currently only possible with the api's art assets or a url, NOT with a local image file
	Progress bar
	Pausing the timer (instead othe blanking it)
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
	private string defaultLargeImage = "logo"; // string name comes from the assets uploaded to the app
	[SerializeField]
	private string defaultLargeText = "Yet Another Rhythm Game"; //Tooltip text for the large icon
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
	//A bunch of time handling stuff
	private long gameStartTime; //start of YARG, doesn't stop
	private long songStartTime; //time at song start
	private long songEndTime; //time when the song should end (start+ length)
	private float songLengthSeconds; //Length of song in seconds (not milliseconds)
	private long songTimePlayed; //the amount of song played


	private void Start() {

		Instance = this;

		// Listen to the changing of songs
		Play.OnSongStart += OnSongStart;
		Play.OnSongEnd += OnSongEnd;
		// Listen to instrument selection
		DifficultySelect.OnInstrumentSelection += OnInstrumentSelection;
		//listen to pausing
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

	private void OnPauseToggle(bool pause){
		if (pause){
			songTimePlayed = DateTimeOffset.Now.ToUnixTimeMilliseconds() - songStartTime; //get duration of song played
			SetActivity(currentSmallImage,currentSmallText, songName, "by " + artistName, 0,0);
		}else{
			SetActivity(currentSmallImage,currentSmallText, songName, "by " + artistName, DateTimeOffset.Now.ToUnixTimeMilliseconds(), DateTimeOffset.Now.AddSeconds(songLengthSeconds).ToUnixTimeMilliseconds() - songTimePlayed);
		}
	}

	private void OnSongStart(SongInfo song) {
		songStartTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
		songLengthSeconds = song.songLength;
		songEndTime = DateTimeOffset.Now.AddSeconds(songLengthSeconds).ToUnixTimeMilliseconds();
		songName = song.SongName;
		artistName = song.artistName;
		SetActivity(currentSmallImage,currentSmallText, songName, "by " + artistName, DateTimeOffset.Now.ToUnixTimeMilliseconds(), DateTimeOffset.Now.AddSeconds(songLengthSeconds).ToUnixTimeMilliseconds());
	}

	private void OnSongEnd(SongInfo song) {
		SetDefaultActivity();
	}

	private void OnInstrumentSelection(YARG.PlayerManager.Player playerInfo){
		switch (playerInfo.chosenInstrument)
		{
			case "vocals": //microphone, vocals
				currentSmallImage = "mic";
				currentSmallText = "belting one out";
			break;

			case "harmVocals": //Multiple microphones, Harmony
				currentSmallImage = "harmony";
				currentSmallText = "belting one out, with friends!";
			break;         
			
			case "drums": //drums
				currentSmallImage = "drums";
				currentSmallText = "Working the skins";
			break;

			case "realDrums": // pro drums
				currentSmallImage = "drums";
				currentSmallText = "Really working the skins";
			break;

			case "ghDrums": // 5 lane drums
				currentSmallImage = "drums";
				currentSmallText = "Working the skins +1";
			break;

			case "guitar": //guitar
				currentSmallImage = "guitar";
				currentSmallText = "Making it talk";
			break;

			case "realGuitar": //pro guitar
				currentSmallImage = "guitar";
				currentSmallText = "Really making it talk";
			break;

			case "bass": //bass
				currentSmallImage = "bass";
				currentSmallText = "In the groove";
			break;

			case "realBass": //pro bass
				currentSmallImage = "bass";
				currentSmallText = "Really in the groove";
			break;

			case "keys": //Keyboard, piano
				currentSmallImage = "keys";
				currentSmallText = "tickling the ivory";
			break;

			case "realKeys": //pro keys
				currentSmallImage = "keys";
				currentSmallText = "Really tickling the ivory";
			break;

			default:
				currentSmallImage = "";
				currentSmallText = "";
			break;
		}
	}			

	private void Update() {
		if (discord == null ) {
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


private void SetActivity(string _smallImage, string _smallText, string _details, string _state, long _startTimeStamp, long _endTimeStamp){
	CurrentActivity = new Activity {
			Assets = {
				LargeImage = defaultLargeImage, //the YARG logo and tooltip does not change, at this point in time.
				LargeText = defaultLargeText,
				SmallImage = _smallImage,
				SmallText = _smallText,
			},
			Details = _details,
			State = _state,
			Timestamps = {
				Start = _startTimeStamp,
				End = _endTimeStamp,
			}
		};
}
}
