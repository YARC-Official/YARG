using UnityEngine.InputSystem.LowLevel;
using YARG.Core;

namespace YARG.Input
{
    public class GameModeBindings
    {
        public GameMode GameMode { get; }

        public BindingCollection Menu { get; }
        public BindingCollection Gameplay { get; }

        public GameModeBindings(GameMode mode)
        {
            Menu = BindingCollection.CreateMenuBindings();
            Gameplay = BindingCollection.CreateGameplayBindings(mode);
        }

        public void EnableInputs()
        {
            Menu.EnableInputs();
            Gameplay.EnableInputs();
        }

        public void DisableInputs()
        {
            Menu.DisableInputs();
            Gameplay.DisableInputs();
        }

        public void ProcessInputEvent(InputEventPtr eventPtr)
        {
            Menu.ProcessInputEvent(eventPtr);
            Gameplay.ProcessInputEvent(eventPtr);
        }
    }
}