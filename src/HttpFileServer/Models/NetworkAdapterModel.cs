using System;

namespace HttpFileServer.Models
{
    public class NetworkAdapterModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string IPv4 { get; set; }
        public string IPv6 { get; set; }
        public override string ToString()
        {
            return Name;
        }
    }
}
