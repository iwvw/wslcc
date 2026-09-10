using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace WSLCC.Core.Services;

public static class WslcNetworkDiagnostics
{
    public static IReadOnlyList<string> DetectProxyTunInterfaces()
    {
        var result = new List<string>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                    continue;
                foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;
                    var ip = addr.Address.GetAddressBytes();
                    if (IsInCidr(ip, 198, 18, 0, 0, 15) || IsInCidr(ip, 100, 64, 0, 0, 10))
                    {
                        result.Add($"{ni.Name}（{addr.Address}）");
                        break;
                    }
                }
            }
        }
        catch
        {
        }
        return result;
    }

    private static bool IsInCidr(byte[] ip, byte a, byte b, byte c, byte d, int prefix)
    {
        byte[] network = [a, b, c, d];
        for (var i = 0; i < prefix; i++)
        {
            var byteIndex = i / 8;
            var bit = 7 - (i % 8);
            var ipBit = (ip[byteIndex] >> bit) & 1;
            var netBit = (network[byteIndex] >> bit) & 1;
            if (ipBit != netBit)
                return false;
        }
        return true;
    }
}