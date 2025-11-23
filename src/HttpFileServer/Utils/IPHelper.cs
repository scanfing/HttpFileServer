using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HttpFileServer.Utils
{
    public class IPHelper
    {
        #region Methods

        public static IPAddress[] GetAllLocalIP()
        {
            var lst = new List<IPAddress>();
            try
            {
                string HostName = Dns.GetHostName();
                IPHostEntry IpEntry = Dns.GetHostEntry(HostName);
                foreach (var ip in IpEntry.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork || ip.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        lst.Add(ip);
                    }
                }
            }
            catch (Exception)
            {
            }
            return lst.ToArray();
        }

        #endregion Methods
    }
}