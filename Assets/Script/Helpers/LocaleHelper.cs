using System.Globalization;
using UnityEngine.Localization;

namespace YARG.Helpers
{
    public static class LocaleHelper
    {
        public static readonly LocalizedString EmptyString = new();

        public static readonly NumberFormatInfo PercentFormat = new()
        {
            // Display as "100%" instead of "100 %"
            PercentPositivePattern = 1,
            PercentNegativePattern = 1,
        };


    }
}