using YARG.Core;

namespace YARG.Menu.Dialogs
{
    public class FourLaneDrumsBindingDialog : FriendlyBindingDialog
    {
        protected override (string initial, string complete) BindingMessages { get; set; } = (
            "When a pad/cymbal is highlighted, strike the corresponding input on your drum kit.\n\nClick the Start button when you're ready to begin.",
            "Binding complete.\n\nYou will still need to manually set menu navigation bindings if you have not already."
        );

        protected override void CheckForModeSwitch(string key, GameMode mode)
        {

        }
    }
}