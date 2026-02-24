using TMPro;
using UnityEngine;
using UniVRM10;

namespace YARG.Settings.Preview
{
    public class CharacterPreviewUI : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _characterName;
        [SerializeField]
        private TextMeshProUGUI _characterAuthor;

        public void Initialize(GameObject character)
        {
            if (character == null)
            {
                _characterName.SetText(string.Empty);
                _characterAuthor.SetText(string.Empty);
                return;
            }

            var vrmInstance = character.GetComponent<Vrm10Instance>();

            var authors = vrmInstance.Vrm.Meta.Authors;

            var author = authors.Count switch
            {
                0 => "",
                1 => authors[0],
                _ => string.Join(", ", authors)
            };

            _characterName.SetText(vrmInstance.Vrm.Meta.Name);
            _characterAuthor.SetText(author);
        }

        public void Disable()
        {
            _characterName.SetText(string.Empty);
            _characterAuthor.SetText(string.Empty);
        }
    }
}