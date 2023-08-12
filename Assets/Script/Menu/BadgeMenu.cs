using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Import the UI namespace
namespace YARG.Menu
{
    [ExecuteInEditMode]
    public class BadgeMenu : MonoBehaviour
    {

        [Space(20)]

        [SerializeField]
        private RawImage badgeImgComponent;

        [SerializeField]
        private TextMeshProUGUI badgeTextComponent;

        [Space(20)]

        [SerializeField]
        private Color badgeColor;

        [SerializeField]
        private string badgeText;

        // This method is called whenever an inspector variable is changed in the Editor
        private void OnValidate()
        {
            UpdateElements();
        }

        // Start is called before the first frame update
        void Start()
        {
            UpdateElements();

            // disable script
            enabled = false;
        }

        private void UpdateElements()
        {
            // Set the color of the RawImage's Graphic component
            if (badgeImgComponent != null)
            {
                var badgeGraphic = badgeImgComponent.GetComponent<Graphic>();
                badgeGraphic.color = badgeColor;
            }

            // Set the color of the text
            if (badgeTextComponent != null)
            {
                badgeTextComponent.color = badgeColor;
                badgeTextComponent.text = badgeText;
            }
        }
    }
}