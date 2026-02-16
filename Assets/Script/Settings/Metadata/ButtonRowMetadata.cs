namespace YARG.Settings.Metadata
{
    public sealed class ButtonRowMetadata : AbstractMetadata
    {
        public override string[] UnlocalizedSearchNames { get; }

        public string[] Buttons { get; private set; }

        public ButtonRowMetadata(string button, bool advanced = false)
            : base(advanced)
        {
            UnlocalizedSearchNames = new[] { $"Button.{button}" };
            Buttons = new[] { button };
        }

        public ButtonRowMetadata(bool advanced, params string[] buttons)
            : base(advanced)
        {
            UnlocalizedSearchNames = new string[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                UnlocalizedSearchNames[i] = $"Button.{buttons[i]}";
            }

            Buttons = buttons;
        }

        public ButtonRowMetadata(params string[] buttons)
            : this(false, buttons)
        {
        }
    }
}