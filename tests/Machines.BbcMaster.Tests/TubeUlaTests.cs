using Machines.Common;
using Xunit;

namespace Machines.BbcMaster.Tests;

public class TubeUlaTests
{
    [Fact]
    public void HostWrite_ParasiteRead_StreamsBytesCorrectly()
    {
        var tube = new TubeUla();

        // Host writes 0x42 to R1 Data Register ($FEE1)
        tube.Write(0x01, 0x42);

        // Parasite status should show data available (bit 7 set)
        byte parasiteStatus = tube.ReadParasite(0);
        Assert.Equal(0x80, parasiteStatus & 0x80);

        // Parasite reads data byte from R1 ($FEF1)
        byte data = tube.ReadParasite(1);
        Assert.Equal(0x42, data);
    }
}
