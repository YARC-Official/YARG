using UnityEngine;

namespace YARG
{
    public class playnow : MonoBehaviour
    {
        [Header("References")]
        public VenueManager venueManager;

        // Start is called before the first frame update
        void Start()
        {
            venueManager.Play();
        }
    }
}
