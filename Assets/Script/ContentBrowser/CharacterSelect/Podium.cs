using System;
using DG.Tweening;
using UnityEngine;
using YARG.Helpers.Extensions;
using YARG.Settings;
using CharacterInfo = YARG.ContentBrowser.CharacterSelect.CharacterDisplayManager.CharacterInfo;

namespace YARG.ContentBrowser.CharacterSelect
{
    public class Podium : MonoBehaviour
    {
        private GameObject _characterPrefab;
        private GameObject _characterInstance;

        private GameObject _podiumPrefab;

        private bool     _isAddressable;
        private bool     _hasAttract;
        private bool     _hasIdle;
        private Animator _animator;

        [NonSerialized]
        public string Name;
        [NonSerialized]
        public string Credits;

        [SerializeField]
        public Transform CharacterLocation;
        [Space]
        [SerializeField]
        public Light Light;

        [NonSerialized]
        public CharacterInfo CharacterInfo;


        private void OnEnable()
        {
            if (_characterInstance != null)
            {
                _characterInstance.SetActive(true);
            }
        }

        public void SetCharacter(CharacterInfo characterInfo)
        {
            if (_characterInstance != null)
            {
                ClearCharacter();
            }

            if (characterInfo == null)
            {
                return;
            }

            CharacterInfo = characterInfo;

            _isAddressable = characterInfo.IsAddressable;

            _characterInstance = Instantiate(characterInfo.Prefab, CharacterLocation);
            _characterInstance.transform.SetParent(CharacterLocation, false);
            _characterInstance.transform.localPosition = Vector3.zero;

            if (isActiveAndEnabled)
            {
                _characterInstance.SetActive(true);
            }

            Name = characterInfo.Name;
            Credits = characterInfo.Author;

            if (CharacterDisplayManager.IsSelected(characterInfo))
            {
                SetLightColor(Color.gold, 0f);
            }
            else if (CharacterDisplayManager.IsHidden(characterInfo))
            {
                SetLightColor(Color.darkRed, 0f);
            }
            else
            {
                SetLightColor(Color.white, 0f);
            }

            _animator = _characterInstance.GetComponent<Animator>();

            if (_animator == null)
            {
                return;
            }

            _hasAttract = _animator.HasParameter("Attract");
            _hasIdle = _animator.HasParameter("Idle");

            if (_hasAttract)
            {
                _animator.SetTrigger("Attract");
                return;
            }

            if (_hasIdle)
            {
                _animator.SetTrigger("Idle");
                return;
            }
        }

        public void SetLightColor(Color color, float duration)
        {
            if (duration > 0f)
            {
                Light.DOColor(color, duration);
            }
            else
            {
                Light.color = color;
            }
        }

        public void ClearCharacter()
        {
            Name = string.Empty;
            Credits = string.Empty;
            DestroyCharacter();
        }

        private void DestroyCharacter()
        {
            if (_characterInstance == null)
            {
                return;
            }

            Destroy(_characterInstance);

            _characterInstance = null;
            _animator = null;
        }

        private void OnDisable()
        {
            if (_characterInstance != null)
            {
                _characterInstance.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            DestroyCharacter();
        }
    }
}