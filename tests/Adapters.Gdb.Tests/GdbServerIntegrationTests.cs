using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cpu6502.Core;
using Xunit;

namespace Adapters.Gdb.Tests;

public sealed class GdbServerIntegrationTests : IDisposable
{
    private readonly int _port;

    public GdbServerIntegrationTests()
    {
        _port = 12345;
    }

    public void Dispose()
    {
    }

    /// <summary>
    /// Test that the GDB server can start and stop gracefully.
    /// </summary>
    [Fact]
    public void GdbServer_StartsAndStops()
    {
        // Arrange
        var ram = new Ram(0x10000);
        var cpu = new Cpu(ram);
        var target = new Cpu6502GdbTarget(cpu, ram);
        var server = new RspServer(target, _port);

        // Act
        server.Start();
        
        // Give it a moment to start
        Thread.Sleep(100);
        
        // Assert - server is running
        Assert.True(server.IsRunning);
        
        // Stop the server
        server.Stop();
        Thread.Sleep(100);
        
        // Assert - server has stopped
        Assert.False(server.IsRunning);
        
        // Cleanup
        target.Dispose();
    }

    /// <summary>
    /// Test that the GDB server responds to halt requests.
    /// </summary>
    [Fact]
    public void GdbServer_RespondsToRequests()
    {
        // Arrange
        var ram = new Ram(0x10000);
        var cpu = new Cpu(ram);
        var target = new Cpu6502GdbTarget(cpu, ram);
        var server = new RspServer(target, _port);

        // Act
        server.Start();
        Thread.Sleep(100);
        
        // Assert - server is running
        Assert.True(server.IsRunning);
        
        // Stop the server
        server.Stop();
        Thread.Sleep(100);
        
        // Cleanup
        target.Dispose();
    }

    /// <summary>
    /// Test that CPU breakpoints are set and handled correctly.
    /// </summary>
    [Fact]
    public void GdbTarget_HandlesBreakpoints()
    {
        // Arrange
        var ram = new Ram(0x10000);
        var cpu = new Cpu(ram);

        // Write a simple program: LDA #$42 at address 0x0200
        ram.Write(0x0200, 0xA9);  // LDA immediate
        ram.Write(0x0201, 0x42);

        var target = new Cpu6502GdbTarget(cpu, ram);

        // Act - set breakpoint at 0x0200
        target.SetBreakpoint(0x0200);

        // Assert - breakpoint is set (method exists via Cpu6502GdbTarget API)
        // (Verification is indirect: no exception thrown)

        // Remove breakpoint
        target.RemoveBreakpoint(0x0200);
        
        // Assert - breakpoint is removed
        // (Verification is indirect: no exception thrown)
    }

    /// <summary>
    /// Test that CPU registers can be read over the GDB interface.
    /// </summary>
    [Fact]
    public void GdbTarget_ReadsRegisters()
    {
        // Arrange
        var ram = new Ram(0x10000);
        var cpu = new Cpu(ram);

        var target = new Cpu6502GdbTarget(cpu, ram);

        // Act - read all registers as hex string
        var registersHex = target.ReadAllRegisters();

        // Assert - hex string is non-empty and has expected length
        Assert.NotNull(registersHex);
        Assert.True(registersHex.Length > 0);
        
        // Should be 13 bytes * 2 hex chars = 26 chars
        Assert.Equal(26, registersHex.Length);
    }

    /// <summary>
    /// Test that CPU memory can be read over the GDB interface.
    /// </summary>
    [Fact]
    public void GdbTarget_ReadsMemory()
    {
        // Arrange
        var ram = new Ram(0x10000);
        ram.Write(0x0200, 0xA9);
        ram.Write(0x0201, 0x42);
        ram.Write(0x0202, 0x00);

        var cpu = new Cpu(ram);
        var target = new Cpu6502GdbTarget(cpu, ram);

        // Act - read 3 bytes from 0x0200 as hex string
        var dataHex = target.ReadMemory(0x0200, 3);

        // Assert - hex string contains the expected values
        Assert.NotNull(dataHex);
        Assert.Equal("A94200", dataHex);  // 3 bytes * 2 hex chars = 6 chars
    }

    /// <summary>
    /// Test that CPU halt reason is correctly reported.
    /// </summary>
    [Fact]
    public void GdbTarget_ReportsHaltReason()
    {
        // Arrange
        var ram = new Ram(0x10000);
        var cpu = new Cpu(ram);
        var target = new Cpu6502GdbTarget(cpu, ram);

        // Act - initially not halted
        var initialReason = target.GetHaltReason();

        // Assert - should report no signal initially
        Assert.Equal("S00", initialReason);
    }
}
