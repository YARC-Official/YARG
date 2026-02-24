using TMPro;
using UnityEngine;
using UniVRM10;
using YARG.Venue.Characters;

namespace YARG.Settings.Preview
{
    public class CharacterPreview : MonoBehaviour
    {
        [SerializeField]
        private Transform _characterContainer;

        private Vrm10Instance _vrmInstance;
        private VRMCharacter  _vrmCharacter;
        private GameObject    _character; // The instance, not the prefab loaded from the AssetBundle

        private Animator _animator;

        public GameObject Initialize(GameObject character)
        {
            // Now instantiate the character
            var characterInstance = Instantiate(character, _characterContainer);
            // Rotate the character 180 degrees because reasons
            characterInstance.transform.Rotate(Vector3.up, 180f);
            characterInstance.SetActive(true);

            _character = characterInstance;

            _vrmCharacter = characterInstance.GetComponent<VRMCharacter>();

            _vrmCharacter.Initialize();
            _vrmCharacter.SetPreviewIdle();

            return characterInstance;
        }

        public GameObject Reinitialize(GameObject character)
        {
            if (_character != null)
            {
                Destroy(_character);
            }

            Initialize(character);
            return _character;
        }

        public void Disable()
        {
            if (_character != null)
            {
                _character.SetActive(false);
            }
        }

        private void OnDisable()
        {
            // Release the character so the asset bundle will actually be unloaded
            Destroy(_character);
        }
    }
}