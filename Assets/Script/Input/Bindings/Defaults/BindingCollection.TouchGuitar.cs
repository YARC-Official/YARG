using YARG.Core;
using YARG.Core.Input;

namespace YARG.Input
{
    public partial class BindingCollection
    {
        public bool SetDefaultBindings(TouchGuitarDevice touch)
        {
            return IsMenu ? SetDefaultMenuBindings(touch) : SetDefaultGameplayBindings(touch);
        }

        private bool SetDefaultGameplayBindings(TouchGuitarDevice touch)
        {
            if (Mode != GameMode.FiveFretGuitar)
                return false;

            AddBinding(GuitarAction.GreenFret, touch.fret1);
            AddBinding(GuitarAction.RedFret, touch.fret2);
            AddBinding(GuitarAction.YellowFret, touch.fret3);
            AddBinding(GuitarAction.BlueFret, touch.fret4);
            AddBinding(GuitarAction.OrangeFret, touch.fret5);

            AddBinding(GuitarAction.StrumDown, touch.strum);
            AddBinding(GuitarAction.StarPower, touch.starPower);
            AddBinding(GuitarAction.Whammy, touch.whammy);

            return true;
        }

        // The touchscreen already drives the menus; leaving the device
        // unbound there means a stray touch can never navigate
        private bool SetDefaultMenuBindings(TouchGuitarDevice touch)
        {
            return IsMenu;
        }
    }
}
