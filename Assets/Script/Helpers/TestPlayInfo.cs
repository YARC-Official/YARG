using UnityEngine;

namespace YARG.Helpers
{
    [CreateAssetMenu(fileName = "TestPlayInfo", menuName = "YARG/TestPlayInfo", order = 1)]
    public class TestPlayInfo : ScriptableObject
    {
        [HideInInspector]
        public bool TestPlayMode;

        [HideInInspector]
        public string TestPlaySongHash;

        public bool NoBotsMode;
    }
}