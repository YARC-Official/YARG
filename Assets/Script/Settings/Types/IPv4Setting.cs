using System;
using System.Net;
using System.Net.Sockets;

namespace YARG.Settings.Types
{
    public class IPv4Setting : AbstractSetting<string>
    {
        public override string AddressableName => "Setting/IPv4";

        public IPv4Setting(string value, Action<string> onChange = null) : base(onChange)
        {
            _value = value;
        }

        protected override void SetValue(string value)
        {
            if (!IsValidIPv4(value))
            {
                // If it's invalid, just default to this
                _value = "255.255.255.255";
            }
            else
            {
                _value = value;
            }
        }

        public override bool ValueEquals(string value)
        {
            return value == Value;
        }

        public static bool IsValidIPv4(string ip)
        {
            if (string.IsNullOrEmpty(ip))
            {
                return false;
            }

            if (!IPAddress.TryParse(ip, out var ipAddress))
            {
                return false;
            }

            return IsValidIPv4(ipAddress);
        }

        public static bool IsValidIPv4(IPAddress ip)
        {
            if (ip.AddressFamily != AddressFamily.InterNetwork)
            {
                return false;
            }

            return true;
        }
    }
}
