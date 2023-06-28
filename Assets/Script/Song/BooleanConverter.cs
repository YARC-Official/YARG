using EasySharpIni.Converters;

namespace YARG.Song
{
    public class BooleanConverter : Converter<bool>
    {
        public override string GetDefaultName()
        {
            return "Boolean";
        }

        public override bool GetDefaultValue()
        {
            return false;
        }

        public override bool Parse(string arg, out bool result)
        {
            arg = arg.ToLower();
            switch (arg)
            {
                case "true":
                case "1":
                    result = true;
                    break;

                case "false":
                case "0":
                    result = false;
                    break;

                default:
                    result = false;
                    return false;
            }

            ;

            return true;
        }
    }
}