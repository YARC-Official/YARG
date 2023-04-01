using UnityEngine;
using Discord;
using System;

public class DiscordController : MonoBehaviour
{
    private Discord.Discord discord;
    private ActivityManager activityManager;
    private Activity activity;

    private float updateTime = 0f;
    private long elaspedPlayTime = 0;

    [SerializeField]
    private long applicationID = 1091177744416637028;
    [Space]
    [SerializeField]
    private string details = "Hello there ladies and gentlemen!"; //At the moment there is no changing of menus, what song is playing, etc. Basic for now.
    [SerializeField]
    private string state = "Are you ready to rock?";
    [Space]
    [SerializeField]
    private string largeImage = "logo"; //only shown in 'view profile' -> 'activity'
    [SerializeField]
    private string largeText = "Yet Another Rhythm Game"; //tooltip for largeimage
    [SerializeField]
    private string smallImage = ""; //Current instrument icon? only shown in 'view profile' -> 'activity' as a little overlay on the bottom right of the Large image - currently unused.
    [SerializeField]
    private string smallText = ""; //Tooltip for smallImage - currently unused.
    
    void Start(){
       InitDiscord();
    }
	
    void InitDiscord() //When the game is started while discord is already running the status update happens nearly instantly. However if discord is opened or re-opened after game start, it can take up to 35 seconds for the status to display.
    {
        discord?.Dispose();
        try{ //if discord isn't open at run start this will throw 'InternalError' instead of 'NotRunning' (don't know why)
            discord = new Discord.Discord(applicationID, (System.UInt64)Discord.CreateFlags.NoRequireDiscord);
        }catch (Exception e) {
            if(e.Message == "InternalError")  {discord = null;}; 
        }
        activityManager = discord?.GetActivityManager();
        elaspedPlayTime = System.DateTimeOffset.Now.ToUnixTimeMilliseconds();
        activity = new Activity{
            Details = details,
            State = state,
            Assets =
            {
                LargeImage = largeImage,
                LargeText = largeText
            },
            Timestamps = 
            {
                Start = elaspedPlayTime,
            }
        };
    }

        
    public void Update() {
        TimedUpdateActivity();
        try {
            if (discord == null) {  InitDiscord(); return; }
            discord.RunCallbacks();
        }
        catch (Exception e) {;
            discord = null;
            if(e.Message == "NotRunning")  InitDiscord();
        }
    }

    private void TimedUpdateActivity(){ // Discord has a rate limit of 5 updates per 20 seconds. Need to make sure it isn't more otherwise Discord will freak the hell out and the activity will never update.
       updateTime -= Time.deltaTime;
        if (updateTime <= 0f){
            activityManager?.UpdateActivity(activity, (result) => {}); //null catch if discord wasn't open when game started. Also, the results could be put into a log file or Debug.Log but it doesn't seem useful at the moment.
            updateTime = 4.5f;
        }
    
    }

}  
