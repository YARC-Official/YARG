using UnityEngine;

namespace YARG.Menu.Dialogs
{
    /// <summary>
    /// A message dialog that shows images and text
    /// </summary>
    public class ImageDialog : MessageDialog
    {
        [Space]
        [SerializeField]
        private GameObject _imageContainer;

        public override void ClearDialog()
        {
            base.ClearDialog();

            if (_imageContainer == null)
            {
                return;
            }

            _imageContainer.SetActive(false);
        }
    }
}