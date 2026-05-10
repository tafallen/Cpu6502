using System.Net;
using System.Net.Sockets;
using System.Text;
using Adapters.Gdb;

namespace Adapters.Gdb.Tests;

public sealed class RspServerTests
{
    [Fact]
    public void Server_DispatchesCommandsAndSendsAcks()
    {
        var target = new RecordingTarget
        {
            HaltReason = "S11",
            Registers = "00112233445566778899AABBCC",
            StepResult = true,
            MemoryValue = "DEADBEEF",
        };

        int port = GetFreePort();
        using var server = new RspServer(target, port);
        server.Start();

        using var client = ConnectWithRetry(port);
        using NetworkStream stream = client.GetStream();

        Assert.Equal("S11", Exchange(stream, "?"));
        Assert.Equal("00112233445566778899AABBCC", Exchange(stream, "g"));
        Assert.Equal("OK", Exchange(stream, "G0102030405060708090A0B0C0D"));
        Assert.Equal("01", Exchange(stream, "p1"));
        Assert.Equal("OK", Exchange(stream, "P1=AA"));
        Assert.Equal("DEADBEEF", Exchange(stream, "m1000,4"));
        Assert.Equal("OK", Exchange(stream, "M1000,4:01020304"));
        Assert.Equal("S05", Exchange(stream, "s"));
        Assert.Equal("S05", Exchange(stream, "S99"));
        Assert.Equal("S05", Exchange(stream, "c"));
        Assert.Equal("S05", Exchange(stream, "C11"));
        Assert.Equal("OK", Exchange(stream, "Z1,1000,1"));
        Assert.Equal("OK", Exchange(stream, "z1,1000,1"));
        Assert.Equal("", Exchange(stream, "Z0,1000,1"));
        Assert.Equal("", Exchange(stream, "z0,1000,1"));
        Assert.Equal("PacketSize=4000;hwbreak+;swbreak+", Exchange(stream, "qSupported"));
        Assert.Equal("TextSeg=0", Exchange(stream, "qOffset"));
        Assert.Equal("", Exchange(stream, "qTStatus"));
        Assert.Equal("", Exchange(stream, "v"));
        Assert.Equal("E01", Exchange(stream, "x"));
        Assert.Equal("OK", Exchange(stream, "k"));

        client.Close();
        server.Stop();

        Assert.Equal("0102030405060708090A0B0C0D", target.LastWriteAllRegisters);

        Assert.Single(target.WrittenRegisters);
        Assert.Equal(1, target.WrittenRegisters[0].RegNum);
        Assert.Equal("AA", target.WrittenRegisters[0].Value);

        Assert.Single(target.MemoryReads);
        Assert.Equal((ushort)0x1000, target.MemoryReads[0].Address);
        Assert.Equal(4, target.MemoryReads[0].Length);

        Assert.Single(target.MemoryWrites);
        Assert.Equal((ushort)0x1000, target.MemoryWrites[0].Address);
        Assert.Equal("01020304", target.MemoryWrites[0].HexData);

        Assert.True(target.ContinueCalled);

        Assert.Single(target.BreakpointsSet);
        Assert.Equal((ushort)0x1000, target.BreakpointsSet[0]);

        Assert.Single(target.BreakpointsRemoved);
        Assert.Equal((ushort)0x1000, target.BreakpointsRemoved[0]);
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static TcpClient ConnectWithRetry(int port)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var client = new TcpClient();
                client.Connect(IPAddress.Loopback, port);
                return client;
            }
            catch (SocketException)
            {
                Thread.Sleep(20);
            }
        }

        throw new TimeoutException($"Could not connect to server on port {port}");
    }

    private static string Exchange(NetworkStream stream, string command)
    {
        SendPacket(stream, command);

        string response = ReadLine(stream);
        int ack = stream.ReadByte();
        Assert.Equal('+', ack);
        return response;
    }

    private static void SendPacket(NetworkStream stream, string command)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(new RspPacket(command).Encode() + "\n");
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }

    private static string ReadLine(NetworkStream stream)
    {
        var bytes = new List<byte>();

        while (true)
        {
            int value = stream.ReadByte();
            if (value < 0)
                throw new EndOfStreamException("Unexpected end of stream");

            if (value == '\n')
                return Encoding.UTF8.GetString(bytes.ToArray());

            if (value != '\r')
                bytes.Add((byte)value);
        }
    }

    private sealed class RecordingTarget : IGdbTarget
    {
        public string HaltReason { get; set; } = "S00";
        public string Registers { get; set; } = string.Empty;
        public string? LastWriteAllRegisters { get; private set; }
        public List<(int RegNum, string Value)> WrittenRegisters { get; } = new();
        public List<(ushort Address, int Length)> MemoryReads { get; } = new();
        public List<(ushort Address, string HexData)> MemoryWrites { get; } = new();
        public List<ushort> BreakpointsSet { get; } = new();
        public List<ushort> BreakpointsRemoved { get; } = new();
        public bool StepResult { get; set; }
        public bool ContinueCalled { get; private set; }
        public bool PauseCalled { get; private set; }
        public bool IsHalted { get; private set; }

        public string ReadAllRegisters() => Registers;

        public void WriteAllRegisters(string hexData)
        {
            LastWriteAllRegisters = hexData;
        }

        public string ReadRegister(int regNum) => regNum.ToString("X2");

        public void WriteRegister(int regNum, string hexData)
        {
            WrittenRegisters.Add((regNum, hexData));
        }

        public string ReadMemory(ushort address, int length)
        {
            MemoryReads.Add((address, length));
            return MemoryValue;
        }

        public string MemoryValue { get; set; } = string.Empty;

        public void WriteMemory(ushort address, string hexData)
        {
            MemoryWrites.Add((address, hexData));
        }

        public ushort GetProgramCounter() => 0;

        public void SetProgramCounter(ushort pc)
        {
        }

        public bool Step() => StepResult;

        public void Continue()
        {
            ContinueCalled = true;
            HaltReason = "S05";
            IsHalted = true;
        }

        public void Pause()
        {
            PauseCalled = true;
            IsHalted = true;
        }

        public void SetBreakpoint(ushort address)
        {
            BreakpointsSet.Add(address);
        }

        public void RemoveBreakpoint(ushort address)
        {
            BreakpointsRemoved.Add(address);
        }

        public string GetHaltReason() => HaltReason;
    }
}
