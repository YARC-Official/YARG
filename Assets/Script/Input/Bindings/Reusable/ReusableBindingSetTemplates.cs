using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core;

namespace YARG.Input
{
    public static partial class ReusableBindingSetTemplates
    {

        public static Dictionary<string, BindingType> GetTemplateForGameMode(GameMode? mode)
        {
            return mode switch {
                GameMode.FiveFretGuitar => FiveFretGuitar,
                _ => new()
            };
        }
    }

    public enum BindingType
    {
        Button,
        Axis,
        Integer
    }
}
