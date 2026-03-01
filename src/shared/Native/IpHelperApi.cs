using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using WfpTrafficControl.Shared.Ipc;

namespace WfpTrafficControl.Shared.Native;

/// <summary>
/// P/Invoke wrappers for IP Helper API (iphlpapi.dll) functions.
/// </summary>
public static class IpHelperApi
{
    private const int AF_INET = 2;
    private const int AF_INET6 = 23;

    private const int TCP_TABLE_OWNER_PID_ALL = 5;
    private const int UDP_TABLE_OWNER_PID = 1;

    private const int ERROR_INSUFFICIENT_BUFFER = 122;
    private const int NO_ERROR = 0;

    /// <summary>
    /// TCP connection states.
    /// </summary>
    public enum TcpState
    {
        Closed = 1,
        Listen = 2,
        SynSent = 3,
        SynReceived = 4,
        Established = 5,
        FinWait1 = 6,
        FinWait2 = 7,
        CloseWait = 8,
        Closing = 9,
        LastAck = 10,
        TimeWait = 11,
        DeleteTcb = 12
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW_OWNER_PID
    {
        public uint dwState;
        public uint dwLocalAddr;
        public uint dwLocalPort;
        public uint dwRemoteAddr;
        public uint dwRemotePort;
        public uint dwOwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPTABLE_OWNER_PID
    {
        public uint dwNumEntries;
        // Followed by MIB_TCPROW_OWNER_PID entries
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_UDPROW_OWNER_PID
    {
        public uint dwLocalAddr;
        public uint dwLocalPort;
        public uint dwOwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_UDPTABLE_OWNER_PID
    {
        public uint dwNumEntries;
        // Followed by MIB_UDPROW_OWNER_PID entries
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr pTcpTable,
        ref int pdwSize,
        bool bOrder,
        int ulAf,
        int TableClass,
        uint Reserved);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedUdpTable(
        IntPtr pUdpTable,
        ref int pdwSize,
        bool bOrder,
        int ulAf,
        int TableClass,
        uint Reserved);

    // Process name cache to avoid repeated queries
    private static readonly Dictionary<int, (string? Name, string? Path, DateTime CachedAt)> _processCache = new();
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromSeconds(30);
    private static readonly object _cacheLock = new();

    /// <summary>
    /// Gets all active TCP and UDP connections.
    /// </summary>
    public static List<ConnectionDto> GetConnections(bool includeTcp = true, bool includeUdp = true)
    {
        var connections = new List<ConnectionDto>();

        if (includeTcp)
        {
            connections.AddRange(GetTcpConnections());
        }

        if (includeUdp)
        {
            connections.AddRange(GetUdpConnections());
        }

        return connections;
    }

    /// <summary>
    /// Gets all active TCP connections with process information.
    /// </summary>
    public static List<ConnectionDto> GetTcpConnections()
    {
        var connections = new List<ConnectionDto>();
        var tableSize = 0;

        // First call to get required buffer size
        var result = GetExtendedTcpTable(IntPtr.Zero, ref tableSize, true, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
        if (result != ERROR_INSUFFICIENT_BUFFER && result != NO_ERROR)
        {
            return connections;
        }

        var tableBuffer = Marshal.AllocHGlobal(tableSize);
        try
        {
            result = GetExtendedTcpTable(tableBuffer, ref tableSize, true, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
            if (result != NO_ERROR)
            {
                return connections;
            }

            // Read the number of entries
            var table = Marshal.PtrToStructure<MIB_TCPTABLE_OWNER_PID>(tableBuffer);
            var rowPtr = tableBuffer + Marshal.SizeOf<MIB_TCPTABLE_OWNER_PID>();
            var rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();

            for (var i = 0; i < table.dwNumEntries; i++)
            {
                var row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(rowPtr);
                var (processName, processPath) = GetProcessInfo((int)row.dwOwningPid);

                connections.Add(new ConnectionDto
                {
                    Protocol = "tcp",
                    State = GetTcpStateString((TcpState)row.dwState),
                    LocalIp = FormatIpAddress(row.dwLocalAddr),
                    LocalPort = (int)NetworkToHostPort(row.dwLocalPort),
                    RemoteIp = FormatIpAddress(row.dwRemoteAddr),
                    RemotePort = (int)NetworkToHostPort(row.dwRemotePort),
                    ProcessId = (int)row.dwOwningPid,
                    ProcessName = processName,
                    ProcessPath = processPath
                });

                rowPtr += rowSize;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(tableBuffer);
        }

        return connections;
    }

    /// <summary>
    /// Gets all active UDP listeners with process information.
    /// </summary>
    public static List<ConnectionDto> GetUdpConnections()
    {
        var connections = new List<ConnectionDto>();
        var tableSize = 0;

        // First call to get required buffer size
        var result = GetExtendedUdpTable(IntPtr.Zero, ref tableSize, true, AF_INET, UDP_TABLE_OWNER_PID, 0);
        if (result != ERROR_INSUFFICIENT_BUFFER && result != NO_ERROR)
        {
            return connections;
        }

        var tableBuffer = Marshal.AllocHGlobal(tableSize);
        try
        {
            result = GetExtendedUdpTable(tableBuffer, ref tableSize, true, AF_INET, UDP_TABLE_OWNER_PID, 0);
            if (result != NO_ERROR)
            {
                return connections;
            }

            // Read the number of entries
            var table = Marshal.PtrToStructure<MIB_UDPTABLE_OWNER_PID>(tableBuffer);
            var rowPtr = tableBuffer + Marshal.SizeOf<MIB_UDPTABLE_OWNER_PID>();
            var rowSize = Marshal.SizeOf<MIB_UDPROW_OWNER_PID>();

            for (var i = 0; i < table.dwNumEntries; i++)
            {
                var row = Marshal.PtrToStructure<MIB_UDPROW_OWNER_PID>(rowPtr);
                var (processName, processPath) = GetProcessInfo((int)row.dwOwningPid);

                connections.Add(new ConnectionDto
                {
                    Protocol = "udp",
                    State = "*", // UDP is connectionless
                    LocalIp = FormatIpAddress(row.dwLocalAddr),
                    LocalPort = (int)NetworkToHostPort(row.dwLocalPort),
                    RemoteIp = "*",
                    RemotePort = 0,
                    ProcessId = (int)row.dwOwningPid,
                    ProcessName = processName,
                    ProcessPath = processPath
                });

                rowPtr += rowSize;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(tableBuffer);
        }

        return connections;
    }

    private static string GetTcpStateString(TcpState state)
    {
        return state switch
        {
            TcpState.Closed => "CLOSED",
            TcpState.Listen => "LISTEN",
            TcpState.SynSent => "SYN_SENT",
            TcpState.SynReceived => "SYN_RCVD",
            TcpState.Established => "ESTABLISHED",
            TcpState.FinWait1 => "FIN_WAIT1",
            TcpState.FinWait2 => "FIN_WAIT2",
            TcpState.CloseWait => "CLOSE_WAIT",
            TcpState.Closing => "CLOSING",
            TcpState.LastAck => "LAST_ACK",
            TcpState.TimeWait => "TIME_WAIT",
            TcpState.DeleteTcb => "DELETE_TCB",
            _ => state.ToString()
        };
    }

    private static string FormatIpAddress(uint address)
    {
        return new IPAddress(address).ToString();
    }

    private static uint NetworkToHostPort(uint port)
    {
        // Port numbers are in network byte order (big-endian)
        return ((port & 0xFF) << 8) | ((port >> 8) & 0xFF);
    }

    private static (string? Name, string? Path) GetProcessInfo(int processId)
    {
        if (processId == 0)
        {
            return ("System Idle Process", null);
        }

        if (processId == 4)
        {
            return ("System", null);
        }

        lock (_cacheLock)
        {
            // Check cache
            if (_processCache.TryGetValue(processId, out var cached) &&
                DateTime.UtcNow - cached.CachedAt < CacheExpiry)
            {
                return (cached.Name, cached.Path);
            }

            // Query process
            try
            {
                using var process = Process.GetProcessById(processId);
                var name = process.ProcessName;
                string? path = null;

                try
                {
                    path = process.MainModule?.FileName;
                }
                catch
                {
                    // Access denied for some processes
                }

                _processCache[processId] = (name, path, DateTime.UtcNow);
                return (name, path);
            }
            catch
            {
                // Process may have exited
                _processCache[processId] = (null, null, DateTime.UtcNow);
                return (null, null);
            }
        }
    }

    /// <summary>
    /// Clears the process info cache.
    /// </summary>
    public static void ClearProcessCache()
    {
        lock (_cacheLock)
        {
            _processCache.Clear();
        }
    }
}
