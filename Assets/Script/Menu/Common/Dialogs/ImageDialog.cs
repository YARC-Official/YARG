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
            var sprite = _yargImage.ToSprite();

            Image.sprite = sprite;
            ImageContainer.SetActive(true);
        }

        protected override void OnBeforeClose()
        {
            _yargImage?.Dispose();
            base.OnBeforeClose();
        }
    }
}