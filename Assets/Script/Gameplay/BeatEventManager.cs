using UnityEngine;

namespace YARG.Gameplay
{
    public class BeatEventManager : MonoBehaviour
    {
        private GameManager _gameManager;

        private void Awake()
        {
            _gameManager = GetComponent<GameManager>();
        }
    }
}