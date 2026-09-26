using System.Collections.Generic;
using YARG.Core.Input;

namespace YARG.Input.Bindings
{

    public static partial class ReusableBindingSetTemplates
    {
        public static Dictionary<string, InputActionInfo> MENU = new()
        {
            { ControlStrings.MENU_START,            new(ControlStrings.MENU_START,          BindingType.Button, (int)MenuAction.Start) },
            { ControlStrings.MENU_SELECT,           new(ControlStrings.MENU_SELECT,         BindingType.Button, (int)MenuAction.Select) },
            { ControlStrings.MENU_GREEN,            new(ControlStrings.MENU_GREEN,          BindingType.Button, (int)MenuAction.Green) },
            { ControlStrings.MENU_RED,              new(ControlStrings.MENU_RED,            BindingType.Button, (int)MenuAction.Red) },
            { ControlStrings.MENU_YELLOW,           new(ControlStrings.MENU_YELLOW,         BindingType.Button, (int)MenuAction.Yellow) },
            { ControlStrings.MENU_BLUE,             new(ControlStrings.MENU_BLUE,           BindingType.Button, (int)MenuAction.Blue) },
            { ControlStrings.MENU_ORANGE,           new(ControlStrings.MENU_ORANGE,         BindingType.Button, (int)MenuAction.Orange) },
            { ControlStrings.MENU_UP,               new(ControlStrings.MENU_UP,             BindingType.Button, (int)MenuAction.Up) },
            { ControlStrings.MENU_DOWN,             new(ControlStrings.MENU_DOWN,           BindingType.Button, (int)MenuAction.Down) },
            { ControlStrings.MENU_LEFT,             new(ControlStrings.MENU_LEFT,           BindingType.Button, (int)MenuAction.Left) },
            { ControlStrings.MENU_RIGHT,            new(ControlStrings.MENU_RIGHT,          BindingType.Button, (int)MenuAction.Right) },
            { ControlStrings.MENU_SEARCH,           new(ControlStrings.MENU_SEARCH,         BindingType.Button, (int)MenuAction.Search) },
            { ControlStrings.MENU_SELECT_ARTIST,    new(ControlStrings.MENU_SELECT_ARTIST,  BindingType.Button, (int)MenuAction.SelectArtist) },
        };
    }
}
