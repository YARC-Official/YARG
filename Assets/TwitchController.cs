using System.IO; 
using UnityEngine;
using YARG.PlayMode;
using YARG.UI;
using YARG.Data;
public class TwitchController : MonoBehaviour
{
    // Define the path and filename for the text file. When compiled, dataPath is 'application root/data', add the /../ bumps it back to the root folder, where the exe is. For convenience.
    // The debug logging is probably excessive :)
    private string path;
    void Start()
    {
        path = Application.dataPath + "/../"+ "/currentsong.txt";
        //While YARG should delete the file on exit, you never know if a crash or something prevented that.
        deleteCurrentSongFile();
        createEmptySongFile();

        // Listen to the changing of songs
		Play.OnSongStart += OnSongStart;
		Play.OnSongEnd += OnSongEnd;

		// Listen to instrument selection - NYI, let's confirm the rest works
		DifficultySelect.OnInstrumentSelection += OnInstrumentSelection;

		// Listen to pausing - NYI, let's confirm the rest works
		Play.OnPauseToggle += OnPauseToggle;
    }

	private void OnApplicationQuit() {
		deleteCurrentSongFile();
	}

    void OnSongStart(SongInfo song){
   
        // Open the text file for appending
        StreamWriter writer = new StreamWriter(path, true);

        // Write two lines of text to the file
        writer.WriteLine(song.SongName);
        writer.WriteLine(song.artistName);

        // Close the file
        writer.Close();

        // Confirm that the text file was updated
        Debug.Log("Text file updated at " + path);
        
    }

    private void deleteCurrentSongFile(){
        if (File.Exists(path)){
                File.Delete(path);
                Debug.Log("Text file deleted at " + path);
            }
    }

    private void createEmptySongFile(){
        StreamWriter writer = new StreamWriter(path, false);
        writer.Close();
        // Confirm that the text file was created
        Debug.Log("Text file created at " + path);
    }

    void OnSongEnd(SongInfo song){
        //it might seem odd to delete and create instead of blanking the file but blanking can be slower as a file operation than deleteing. 
        deleteCurrentSongFile();
        createEmptySongFile();
    }

    void OnInstrumentSelection(YARG.PlayerManager.Player playerInfo){
        //on step at at time, want feedback from the twitch guys saying song name works right first
    }

    private void OnPauseToggle(bool pause) {
        //on step at at time, want feedback from the twitch guys saying song name works right first
        if (pause) {
        } else { //unpause
        }
    }

}
