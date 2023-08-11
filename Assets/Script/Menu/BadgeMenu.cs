using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI; // Import the UI namespace

namespace YARG
{
    [ExecuteInEditMode]
    public class BadgeMenu : MonoBehaviour
    {
        [Space(20)]
        
        [SerializeField]
        private RawImage BadgeImgComponent;

        [SerializeField]
        private TextMeshProUGUI BadgeTextComponent;

        [Space(20)]

        [SerializeField]
        private Color BadgeColor;

        [SerializeField]
        private string BadgeText;

        // This method is called whenever an inspector variable is changed in the Editor
        private void OnValidate()
        {
            // Update the color in the Editor
            if (BadgeImgComponent != null)
            {
                Graphic badgeGraphic = BadgeImgComponent.GetComponent<Graphic>();
                badgeGraphic.color = BadgeColor;
            }

            if (BadgeTextComponent != null)
            {
                BadgeTextComponent.color = BadgeColor;
                BadgeTextComponent.text = BadgeText;

            }
        }

        // Start is called before the first frame update
        void Start()
        {
            // Set the color of the RawImage's Graphic component
            if (BadgeImgComponent != null)
            {
                Graphic badgeGraphic = BadgeImgComponent.GetComponent<Graphic>();
                badgeGraphic.color = BadgeColor;
            }

            // Set the color of the text
            if (BadgeTextComponent != null)
            {
                BadgeTextComponent.color = BadgeColor;
                BadgeTextComponent.text = BadgeText;
            }

            // disable script
            enabled = false;
        }
    }
}