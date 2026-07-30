using Machines.BbcMicro;
using Xunit;

namespace Machines.BbcMicro.Tests;

public class BbcMicroMemoryTests
{
    [Fact]
    public void Ram_ReadWrite_WorksInRange()
    {
        byte[] osRom = new byte[0x4000];
        var machine = new BbcMicroMachine(osRom);

        machine.Bus.Write(0x1000, 0x42);
        Assert.Equal(0x42, machine.Bus.Read(0x1000));
    }

    [Fact]
    public void SidewaysRomBank_SelectsBankCorrectly()
    {
        var bank = new BbcSidewaysRomBank();
        byte[] bank0Data = new byte[0x4000];
        byte[] bank15Data = new byte[0x4000];
        bank0Data[0] = 0xAA;
        bank15Data[0] = 0xBB;

        bank.SetBankRom(0, bank0Data);
        bank.SetBankRom(15, bank15Data);

        bank.SelectBank(0);
        Assert.Equal(0xAA, bank.Read(0x8000));

        bank.SelectBank(15);
        Assert.Equal(0xBB, bank.Read(0x8000));
    }

    [Fact]
    public void SheilaBus_RoutesRomSelectLatch()
    {
        var bank = new BbcSidewaysRomBank();
        var sheila = new BbcSheilaBus(bank);

        sheila.Write(0xFE30, 0x05);
        Assert.Equal(5, bank.ActiveBank);
    }
}
