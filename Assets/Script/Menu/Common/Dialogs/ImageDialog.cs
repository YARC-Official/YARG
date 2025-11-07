using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace YARG.Menu.Dialogs
{
    /// <summary>
    /// A message dialog that shows images and text
    /// </summary>
    public class ImageDialog : MessageDialog
    {
        [FormerlySerializedAs("_imageContainer")]
        [Space]
        [SerializeField]
        protected GameObject ImageContainer;
        [SerializeField]
        protected Image Image;

        public override void ClearDialog()
        {
            base.ClearDialog();

            if (ImageContainer == null)
            {
                return;
            }

            ImageContainer.SetActive(false);
        }
    }
}