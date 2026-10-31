using System.IO;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using YARG.Core.IO;
using YARG.Helpers.Extensions;

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

        private YARGImage _yargImage;

        public override void ClearDialog()
        {
            base.ClearDialog();

            if (ImageContainer == null)
            {
                return;
            }

            ImageContainer.SetActive(false);
        }

        public void SetImage(string path)
        {
            if (ImageContainer == null || !File.Exists(path))
            {
                return;
            }

            _yargImage = YARGImage.Load(path);
            if (_yargImage == null)
            {
                Debug.LogWarning("Failed to load image: " + path + ". Image will not be displayed.");
                ImageContainer.SetActive(false);
                return;
            }

            try
            {
                var sprite = _yargImage.ToSprite();
                Image.sprite = sprite;
                Image.transform.localScale = new Vector2(1, -1);
                ImageContainer.SetActive(true);
            }
            finally
            {
                _yargImage.Dispose();
                _yargImage = null;
            }
        }

        protected override void OnBeforeClose()
        {
            _yargImage?.Dispose();
            base.OnBeforeClose();
        }
    }
}