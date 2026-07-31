using Machines.Oric;
using Xunit;

namespace Machines.Oric.Tests;

public class OricTapeTests
{
    [Fact]
    public void LoadTape_ParsesHeaderAndLoadsPayloadToRam()
    {
        byte[] osRom = new byte[0x4000];
        var machine = new OricMachine(osRom);

        // Build valid Oric TAP byte array
        var ms = new MemoryStream();
        ms.Write([0x16, 0x16, 0x16, 0x24]); // Sync & marker
        ms.WriteByte(0x80); // Machine code
        ms.WriteByte(0xC7); // Auto-run
        ms.WriteByte(0x05); // End address high ($0503)
        ms.WriteByte(0x03); // End address low
        ms.WriteByte(0x05); // Load address high ($0501)
        ms.WriteByte(0x01); // Load address low
        ms.WriteByte(0x00); // Reserved byte
        ms.Write(System.Text.Encoding.ASCII.GetBytes("DEMO\0")); // Filename
        ms.Write([0xAA, 0xBB, 0xCC]); // Payload (3 bytes: $0501, $0502, $0503)

        byte[] tapBytes = ms.ToArray();

        var header = OricTapeLoader.LoadTape(tapBytes, machine.Ram);

        Assert.Equal("DEMO", header.Name);
        Assert.Equal(0x0501, header.LoadAddress);
        Assert.Equal(0x0503, header.EndAddress);

        Assert.Equal(0xAA, machine.Ram.Read(0x0501));
        Assert.Equal(0xBB, machine.Ram.Read(0x0502));
        Assert.Equal(0xCC, machine.Ram.Read(0x0503));
    }
}
