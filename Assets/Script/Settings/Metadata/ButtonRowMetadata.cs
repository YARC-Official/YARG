using System.Collections.Generic;
using System.Linq;

namespace YARG.Settings.Metadata
{
    public class ButtonRowMetadata : AbstractMetadata
    {
        public string[] Buttons { get; private set; }

        public ButtonRowMetadata(string button)
        {
            Buttons = new[]
            {
                button
            };
        }

        public ButtonRowMetadata(params string[] buttons)
        {
            Buttons = buttons;
        }
    }
}