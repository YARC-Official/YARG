using UnityEngine.InputSystem.LowLevel;

namespace YARG.Input
{
    public class GameModeBindings
    {
        public BindingCollection Menu { get; }
        public BindingCollection Gameplay { get; }

        public GameModeBindings(BindingCollection menu, BindingCollection gameplay)
        {
            Menu = menu;
            Gameplay = gameplay;
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