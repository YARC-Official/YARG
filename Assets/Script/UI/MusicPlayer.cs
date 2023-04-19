using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using YARG;
using System.IO;
using System.Collections;
using UnityEngine.Networking;
using System.Collections.Generic;
using YARG.Data;

public class MusicPlayer : MonoBehaviour
{
    [SerializeField]
    private Button nextButton;
    [SerializeField]
    private Button playButton;
    [SerializeField]
    private Sprite playButtonPlayImage;
    [SerializeField]
    private Sprite playButtonPauseImage;
    [SerializeField]
    private Slider volumeSlider;
    [SerializeField]
    private RawImage albumImage;
    [SerializeField]
    private TMP_Text songName;
    [SerializeField]
    private TMP_Text artistName;
    [SerializeField]
    private Slider progressBar;
    private SongStem volumeStem;
    private SongInfo[] songsAsArray;
    private IEnumerable<string> stems;

    private void Awake() //only gets awakened from the main menu script after the song library is loaded, otherwise problems.
    {   
        if (SongLibrary.Songs.Count <= 0)
        {
            return;
        }
        SetupSong();
    }
     
    void Update(){
        //updates the progress bar (that is technically a non-interactive slider)
        if (GameManager.AudioManager.IsPlaying == true){
            progressBar.value = GameManager.AudioManager.CurrentPositionF / GameManager.AudioManager.AudioLengthF;
        }

        // get a new song when finished
        if (progressBar.value >= 1.0f){
            SetupSong();
        }
    }

    void OnDisable(){ //don't want to play over the song select menu
        GameManager.AudioManager.UnloadSong();
    }

    void OnEnable(){
        SetupSong();
    }
    public void PlayButton(){
        if (GameManager.AudioManager.IsPlaying == true){
            GameManager.AudioManager.Pause();
            playButton.image.sprite = playButtonPlayImage;
        }else{
            GameManager.AudioManager.Play();
            playButton.image.sprite = playButtonPauseImage;
        }
    }
 
    public void NextButton(){
        SetupSong();
    }

    public void VolumeSlider(float volume){
        SetVolumes(volume);
    }
    
    private void SetVolumes(float _volume){
        foreach (var stemName in stems)
        {
            GameManager.AudioManager.SetStemVolume(AudioHelpers.GetStemFromName(Path.GetFileNameWithoutExtension(stemName)), _volume);
        }
        
    }
    //most of the following is lifted and hacked from the song select menu. I didn't want to public or mess with any of that code.
    private void SetupSong(){
            songsAsArray = SongLibrary.Songs.ToArray();
            int n = UnityEngine.Random.Range(0, songsAsArray.Length);
            songName.text = "\""+songsAsArray[n].SongName+"\""; //harbrace compliant!
            artistName.text = songsAsArray[n].artistName;
            progressBar.value = 0.0f;
            playButton.image.sprite = playButtonPauseImage;
            stems = AudioHelpers.GetSupportedStems(songsAsArray[n].folder.FullName);
            GameManager.AudioManager.LoadSong(stems, false);
            GameManager.AudioManager.Play();
            SetVolumes(volumeSlider.value);
            StartCoroutine(GetAlbumCoverCoroutine(songsAsArray[n]));
    }

    private IEnumerator GetAlbumCoverCoroutine(SongInfo song)
    {
        string[] albumPaths = {
            "album.png",
            "album.jpg",
            "album.jpeg",
        };

        foreach (string path in albumPaths)
        {
            string fullPath = Path.Combine(song.folder.FullName, path);
            if (File.Exists(fullPath))
            {
                // Load file
                using UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(fullPath);
                yield return uwr.SendWebRequest();
                var texture = DownloadHandlerTexture.GetContent(uwr);

                // Set album cover
                albumImage.texture = texture;
                albumImage.color = Color.white;
            }
        }
    }
}