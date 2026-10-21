using System.Collections.Generic;
using YARG.Core.Input;
using YARG.Helpers;

namespace YARG.Input.Bindings
{

    public static partial class ReusableBindingSetTemplates
    {
        public static Dictionary<string, InputActionInfo> MENU = new()
        {
            { ActionStrings.MENU_START,            new(ActionStrings.MENU_START,          BindingType.Button, (int)MenuAction.Start) },
            { ActionStrings.MENU_SELECT,           new(ActionStrings.MENU_SELECT,         BindingType.Button, (int)MenuAction.Select) },
            { ActionStrings.MENU_GREEN,            new(ActionStrings.MENU_GREEN,          BindingType.Button, (int)MenuAction.Green) },
            { ActionStrings.MENU_RED,              new(ActionStrings.MENU_RED,            BindingType.Button, (int)MenuAction.Red) },
            { ActionStrings.MENU_YELLOW,           new(ActionStrings.MENU_YELLOW,         BindingType.Button, (int)MenuAction.Yellow) },
            { ActionStrings.MENU_BLUE,             new(ActionStrings.MENU_BLUE,           BindingType.Button, (int)MenuAction.Blue) },
            { ActionStrings.MENU_ORANGE,           new(ActionStrings.MENU_ORANGE,         BindingType.Button, (int)MenuAction.Orange) },
            { ActionStrings.MENU_UP,               new(ActionStrings.MENU_UP,             BindingType.Button, (int)MenuAction.Up) },
            { ActionStrings.MENU_DOWN,             new(ActionStrings.MENU_DOWN,           BindingType.Button, (int)MenuAction.Down) },
            { ActionStrings.MENU_LEFT,             new(ActionStrings.MENU_LEFT,           BindingType.Button, (int)MenuAction.Left) },
            { ActionStrings.MENU_RIGHT,            new(ActionStrings.MENU_RIGHT,          BindingType.Button, (int)MenuAction.Right) },
            { ActionStrings.MENU_SEARCH,           new(ActionStrings.MENU_SEARCH,         BindingType.Button, (int)MenuAction.Search) },
            { ActionStrings.MENU_SELECT_ARTIST,    new(ActionStrings.MENU_SELECT_ARTIST,  BindingType.Button, (int)MenuAction.SelectArtist) },
        };
    }
}
