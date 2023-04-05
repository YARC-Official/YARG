/*
TO DO:
Reconnect if discord is closed and reopened/opened during game (and not memory the game into a crash)
Display Album art with little icon overlay
*/

using System;
using Discord;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

public class DiscordController : MonoBehaviour {
	
    private void OnEnable() //listen to the loading of scenes
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable() //not sure why the discord controller would ever be disabled but, eh you never know, right?
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) //when the play scene loads, find the play manager object and it's Play script
    {
		
		if (scene.name == "PlayScene"){
			 YARG.PlayMode.Play  test = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Play Manager").GetComponent<YARG.PlayMode.Play>();
			 test.OnSongStart += OnSongStart;
			 test.OnSongEnd += OnSongEnd;
		}
    }
	private void OnSceneUnloaded(Scene scene) // Disconnect listeners on play scene unload
    {
		if (scene.name == "PlayScene"){
			 YARG.PlayMode.Play  test = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Play Manager").GetComponent<YARG.PlayMode.Play>();
			 test.OnSongStart -= OnSongStart;
			 test.OnSongEnd -= OnSongEnd;
		}
    }
	
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
	private long elaspedPlayTime;

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
		elaspedPlayTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
		SetActivity(defaultLargeImage, defaultLargeText, defaultDetails, defaultState, elaspedPlayTime);
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

	private void OnSongStart(object sender, YARG.PlayMode.Play.OnSongStartEndEventArgs e){
		SetActivity(defaultLargeImage, defaultLargeText, "\""+e.songName+"\"", "by "+e.artistName, DateTimeOffset.Now.ToUnixTimeMilliseconds(), DateTimeOffset.Now.AddSeconds(e.songLength).ToUnixTimeMilliseconds() ); //Now Harbrace compliant!
	}

	private void OnSongEnd(object sender, YARG.PlayMode.Play.OnSongStartEndEventArgs e){
		SetActivity(defaultLargeImage, defaultLargeText, defaultDetails, defaultState, elaspedPlayTime); //flip back to time in game
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

	private void SetActivity(string largeImage, string largeText, string details, string state, long startTime, long? endTime = null){ //discord want time in unix epoch milliseconds (aka long)
		CurrentActivity = new Activity {
			Assets = {
				LargeImage = largeImage,
				LargeText = largeText
			},
			Details = details,
			State = state,
			Timestamps = {
				Start = startTime,
				End = endTime ?? 0,
			}
		};
	}

}
