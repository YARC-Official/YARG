using System;
using UnityEngine;

namespace YARG.Helpers.Authoring
{
    // WARNING: Changing this could break themes or venues!
    //
    // This script is used a lot in theme creation.
    // Changing the serialized fields in this file will result in older themes
    // not working properly. Only change if you need to.

    public class Spin : MonoBehaviour
    {
        [SerializeField]
        private bool _resetOnEnable = true;

        [Space]
        [SerializeField]
        private Vector3 _rotationPerSecond;

        private Quaternion _initial;

        private void Awake()
        {
            _initial = transform.localRotation;
        }

        private void OnEnable()
        {
            if (_resetOnEnable)
            {
                transform.localRotation = _initial;
            }
        }

        private void Update()
        {
            transform.Rotate(_rotationPerSecond * Time.deltaTime);
        }
    }
}