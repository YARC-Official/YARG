using UnityEngine;
using YARG.Core.Input;

namespace YARG.Gameplay.HUD
{
    public abstract class BaseInputViewer : MonoBehaviour
    {



        public abstract void OnInput(GameInput input);

    }
}
