using Cysharp.Text;
using TMPro;
using UnityEngine;
using YARG.Core.Engine.ProGuitar;

namespace YARG.Gameplay.Visuals
{
    public class ProStringArray : MonoBehaviour
    {
        [SerializeField]
        private TextMeshPro[] _text;

        private FretBytes _lastState = FretBytes.CreateEmpty();

        private void Awake()
        {
            ResetAll();
        }

        public void ResetAll()
        {
            _lastState = FretBytes.CreateEmpty();
            foreach (var text in _text)
            {
                text.text = string.Empty;
            }
        }

        public void UpdatePressed(FretBytes pressed)
        {
            for (int i = 0; i < 6; i++)
            {
                byte fret = pressed[i];
                if (fret == _lastState[i])
                {
                    continue;
                }

                _lastState[i] = fret;
                _text[i].text = fret == 0
                    ? string.Empty
                    : ZString.Format("{0}", fret);
            }
        }
    }
}