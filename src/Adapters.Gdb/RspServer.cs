using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Adapters.Gdb;

/// <summary>
/// GDB Remote Serial Protocol (RSP) server for remote debugging over TCP.
/// Listens on localhost:port and handles GDB commands.
/// </summary>
public sealed class RspServer : IDisposable
{
    private readonly IGdbTarget _target;
    private readonly int _port;
    private TcpListener? _listener;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private readonly Thread _serverThread;
    private volatile bool _running;
    private readonly object _lock = new();

    public RspServer(IGdbTarget target, int port = 1234)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _port = port;
        _serverThread = new Thread(ServerLoop) { IsBackground = true };
        _running = false;
    }

    /// <summary>Start listening for GDB connections.</summary>
    public void Start()
    {
        if (_running)
            throw new InvalidOperationException("Server already running");

        _running = true;
        _serverThread.Start();
    }

    /// <summary>Stop server and close connection.</summary>
    public void Stop()
    {
        _running = false;
        _serverThread.Join(TimeSpan.FromSeconds(5));
        Dispose();
    }

    private void ServerLoop()
    {
        try
        {
            _listener = new TcpListener(IPAddress.Loopback, _port);
            _listener.Start();
            Console.WriteLine($"[GDB] RSP server listening on localhost:{_port}");

            while (_running)
            {
                _client = _listener.AcceptTcpClient();
                _stream = _client.GetStream();
                Console.WriteLine("[GDB] Client connected");

                try
                {
                    HandleClient();
                }
                finally
                {
                    _stream?.Dispose();
                    _client?.Dispose();
                    Console.WriteLine("[GDB] Client disconnected");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when shutting down
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GDB] Server error: {ex.Message}");
        }
        finally
        {
            _listener?.Stop();
        }
    }

    private void HandleClient()
    {
        while (_running && _client?.Connected == true)
        {
            try
            {
                string? line = ReadLine();
                if (line == null)
                    break;

                if (line == "+")
                    continue;  // ACK from GDB
                if (line == "-")
                    continue;  // NACK from GDB (resend)

                RspPacket request = RspPacket.Parse(line);
                string response = ProcessCommand(request);

                WriteLine(response);
                Send("+");  // ACK
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GDB] Error processing command: {ex.Message}");
                Send("-");  // NACK
            }
        }
    }

    private string ProcessCommand(RspPacket packet)
    {
        lock (_lock)
        {
            return packet.Command switch
            {
                '?' => GetStopReason(),
                'g' => GetAllRegisters(),
                'G' => SetAllRegisters(packet.Arguments),
                'p' => GetRegister(packet.Arguments),
                'P' => SetRegister(packet.Arguments),
                'm' => ReadMemory(packet.Arguments),
                'M' => WriteMemory(packet.Arguments),
                's' => Step(),
                'S' => StepWithSignal(packet.Arguments),
                'c' => Continue(),
                'C' => ContinueWithSignal(packet.Arguments),
                'z' => RemoveBreakpoint(packet.Arguments),
                'Z' => SetBreakpoint(packet.Arguments),
                'k' => Kill(),
                'q' => QueryCommand(packet.Arguments),
                'v' => VerboseCommand(packet.Arguments),
                _ => "E01",  // Unsupported command
            };
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Command Implementations
    // ─────────────────────────────────────────────────────────────────────

    private string GetStopReason() => _target.GetHaltReason();

    private string GetAllRegisters() => _target.ReadAllRegisters();

    private string SetAllRegisters(string data)
    {
        _target.WriteAllRegisters(data);
        return "OK";
    }

    private string GetRegister(string data)
    {
        if (!int.TryParse(data, System.Globalization.NumberStyles.HexNumber, null, out int regNum))
            return "E01";
        return _target.ReadRegister(regNum);
    }

    private string SetRegister(string data)
    {
        int eqIndex = data.IndexOf('=');
        if (eqIndex < 0)
            return "E01";

        string regStr = data[..eqIndex];
        string valStr = data[(eqIndex + 1)..];

        if (!int.TryParse(regStr, System.Globalization.NumberStyles.HexNumber, null, out int regNum))
            return "E01";

        _target.WriteRegister(regNum, valStr);
        return "OK";
    }

    private string ReadMemory(string args)
    {
        // Format: m<addr>,<length>
        int commaIndex = args.IndexOf(',');
        if (commaIndex < 0)
            return "E01";

        if (!ushort.TryParse(args[..commaIndex], System.Globalization.NumberStyles.HexNumber, null, out ushort addr))
            return "E01";
        if (!int.TryParse(args[(commaIndex + 1)..], System.Globalization.NumberStyles.HexNumber, null, out int len))
            return "E01";

        return _target.ReadMemory(addr, len);
    }

    private string WriteMemory(string args)
    {
        // Format: M<addr>,<length>:<data>
        int commaIndex = args.IndexOf(',');
        int colonIndex = args.IndexOf(':');

        if (commaIndex < 0 || colonIndex < 0)
            return "E01";

        if (!ushort.TryParse(args[..commaIndex], System.Globalization.NumberStyles.HexNumber, null, out ushort addr))
            return "E01";
        if (!int.TryParse(args[(commaIndex + 1)..colonIndex], System.Globalization.NumberStyles.HexNumber, null, out int len))
            return "E01";

        string data = args[(colonIndex + 1)..];
        _target.WriteMemory(addr, data);
        return "OK";
    }

    private string Step()
    {
        bool hitBreakpoint = _target.Step();
        return hitBreakpoint ? "S05" : "S00";  // SIGTRAP or SIGSTOP
    }

    private string StepWithSignal(string args)
    {
        // Ignore signal, just step
        return Step();
    }

    private string Continue()
    {
        _target.Continue();
        return "S05";  // Will be updated when breakpoint hits
    }

    private string ContinueWithSignal(string args)
    {
        // Ignore signal, just continue
        return Continue();
    }

    private string SetBreakpoint(string args)
    {
        // Format: Z<type>,<addr>,<kind>
        // Type: 0=soft, 1=hard, 2=write, 3=read, 4=access
        int firstComma = args.IndexOf(',');
        int secondComma = args.LastIndexOf(',');

        if (firstComma < 0 || secondComma < 0 || firstComma == secondComma)
            return "E01";

        if (!int.TryParse(args[..firstComma], System.Globalization.NumberStyles.HexNumber, null, out int type))
            return "E01";
        if (type != 1)  // Only support hardware breakpoints
            return "";

        string addrStr = args[(firstComma + 1)..secondComma];
        if (!ushort.TryParse(addrStr, System.Globalization.NumberStyles.HexNumber, null, out ushort addr))
            return "E01";

        _target.SetBreakpoint(addr);
        return "OK";
    }

    private string RemoveBreakpoint(string args)
    {
        // Format: z<type>,<addr>,<kind>
        int firstComma = args.IndexOf(',');
        int secondComma = args.LastIndexOf(',');

        if (firstComma < 0 || secondComma < 0 || firstComma == secondComma)
            return "E01";

        if (!int.TryParse(args[..firstComma], System.Globalization.NumberStyles.HexNumber, null, out int type))
            return "E01";
        if (type != 1)
            return "";

        string addrStr = args[(firstComma + 1)..secondComma];
        if (!ushort.TryParse(addrStr, System.Globalization.NumberStyles.HexNumber, null, out ushort addr))
            return "E01";

        _target.RemoveBreakpoint(addr);
        return "OK";
    }

    private string Kill()
    {
        _running = false;
        return "OK";
    }

    private string QueryCommand(string args)
    {
        if (args.StartsWith("Supported"))
            return "qSupported:;";  // Empty feature list for now
        if (args.StartsWith("Offset"))
            return "TextSeg=0";  // No offset
        if (args.StartsWith("TStatus"))
            return "";  // Trace not supported
        return "";
    }

    private string VerboseCommand(string args)
    {
        // Not implemented
        return "";
    }

    // ─────────────────────────────────────────────────────────────────────
    // I/O Helpers
    // ─────────────────────────────────────────────────────────────────────

    private string? ReadLine()
    {
        if (_stream == null)
            return null;

        StringBuilder sb = new();
        while (true)
        {
            int b = _stream.ReadByte();
            if (b < 0)
                return null;

            char c = (char)b;
            if (c == '\n')
                return sb.ToString();
            if (c != '\r')
                sb.Append(c);
        }
    }

    private void WriteLine(string data)
    {
        Send(data + "\n");
    }

    private void Send(string data)
    {
        if (_stream == null)
            return;

        byte[] bytes = Encoding.UTF8.GetBytes(data);
        _stream.Write(bytes, 0, bytes.Length);
        _stream.Flush();
    }

    public void Dispose()
    {
        _stream?.Dispose();
        _client?.Dispose();
        _listener?.Stop();
    }
}
