using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Song;

namespace YARG.Menu.DifficultySelect
{
    /// <summary>
    /// UI component for displaying a song in the queue list on the difficulty select screen.
    /// </summary>
    public class SongQueueEntry : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI songNameText;
        [SerializeField] private TextMeshProUGUI artistText;
        [SerializeField] private Image backgroundImage;
        
        private bool _isCurrent;
        
        public void Initialize(SongEntry song, bool isCurrent)
        {
            if (song == null)
            {
                Debug.LogWarning("[SongQueueEntry] Initialize called with null song");
                return;
            }
            
            _isCurrent = isCurrent;
            
            // Set song name
            if (songNameText != null)
            {
                songNameText.text = song.Name;
                // Highlight current song
                songNameText.fontStyle = isCurrent ? FontStyles.Bold : FontStyles.Normal;
            }
            else
            {
                Debug.LogWarning("[SongQueueEntry] songNameText is NULL - prefab might not be set up correctly");
            }
            
            // Set artist
            if (artistText != null)
            {
                artistText.text = song.Artist;
                artistText.fontStyle = isCurrent ? FontStyles.Bold : FontStyles.Normal;
            }
            else
            {
                Debug.LogWarning("[SongQueueEntry] artistText is NULL - prefab might not be set up correctly");
            }
            
            // Highlight current song with green background (like ready button)
            if (backgroundImage != null)
            {
                if (isCurrent)
                {
                    // Green color for current song
                    backgroundImage.color = new Color(0.2f, 0.9f, 0.2f, 0.5f);
                }
                else
                {
                    // Default darker background for other songs
                    backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.3f);
                }
            }
            
            Debug.Log($"[SongQueueEntry] Initialized - Song: {song.Name}, Artist: {song.Artist}, IsCurrent: {isCurrent}");
        }
    }
}
