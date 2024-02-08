using System.Net;

namespace Haukcode.sACN
{
    public class SendData
    {
        public IPEndPoint EndPoint { get; set; }

        public byte[] Data { get; set; }
    }
}
