using System;

namespace YARG.Networking.STUN
{
    /// <summary>
    /// Represents failures encountered while probing STUN servers.
    /// </summary>
    public sealed class StunException : Exception
    {
        public StunException(string message) : base(message)
        {
        }

        public StunException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
