using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;

namespace Core.Utils;

public class IpHelper
{
    public static IPAddress[] GetAllLocalIp()
    {
        var list = new List<IPAddress>();
        try
        {
            string HostName = Dns.GetHostName();
            IPHostEntry ipEntry = Dns.GetHostEntry(HostName);
            foreach (var ip in ipEntry.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork || ip.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    list.Add(ip);
                }
            }
        }
        catch (Exception)
        {
        }

        return list.ToArray();
    }
}