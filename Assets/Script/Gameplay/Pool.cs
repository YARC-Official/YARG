using System.Collections.Generic;
using UnityEngine;

namespace YARG.Gameplay
{
    public abstract class Pool : MonoBehaviour
    {
        private readonly Stack<GameObject> _stack;

        [SerializeField]
        private GameObject _prefab;
        [SerializeField]
        private int _prewarmAmount = 15;

        private void Awake()
        {
            for (int i = 0; i < _prewarmAmount; i++)
            {
                var gameObject = CreateNew();
                gameObject.SetActive(false);
            }
        }

        private GameObject CreateNew()
        {
            var gameObject = Instantiate(_prefab, transform);
            _stack.Push(gameObject);
            return gameObject;
        }

        public GameObject Take()
        {
            if (_stack.TryPop(out var obj))
            {
                obj.SetActive(true);
                return obj;
            }
            
            return CreateNew();
        }

        public void Return(GameObject obj)
        {
            obj.SetActive(false);
            _stack.Push(obj);
        }
    }
}