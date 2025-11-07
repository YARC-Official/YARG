namespace YARG.Menu.Dialogs
{
    public class InputBindingDialog : ImageDialog
    {

        public override void Initialize()
        {
            // TODO: Figure out how to get the button presses even when they aren't yet bound
            base.Initialize();
        }

        protected override void OnBeforeClose()
        {
            // TODO: Undo whatever you did to get the button presses
            base.OnBeforeClose();
        }

    }
}