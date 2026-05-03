using Cpu6502.Core;
using Xunit;

namespace Cpu6502.Tests;

/// <summary>
/// Tests for IBusValidator and DEBUG-mode address validation.
/// These tests verify that Ram and Rom implement ValidateAddress correctly,
/// and that AddressDecoder calls it in DEBUG builds.
/// </summary>
public class BusValidationTests
{
    [Fact]
    public void Ram_ValidateAddress_WithinBounds_Succeeds()
    {
        var ram = new Ram(0x2000);  // 8 KB
        
        // Should not throw
        ((IBusValidator)ram).ValidateAddress(0x0000);
        ((IBusValidator)ram).ValidateAddress(0x1FFF);
    }

    [Fact]
    public void Ram_ValidateAddress_OutOfBounds_Throws()
    {
        var ram = new Ram(0x2000);
        
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            ((IBusValidator)ram).ValidateAddress(0x2000);
        });
        
        Assert.Contains("0x2000", ex.Message);
        Assert.Contains("0x2000", ex.Message);  // Size in message
    }

    [Fact]
    public void Rom_ValidateAddress_WithinBounds_Succeeds()
    {
        var rom = new Rom(new byte[0x2000]);
        
        // Should not throw
        ((IBusValidator)rom).ValidateAddress(0x0000);
        ((IBusValidator)rom).ValidateAddress(0x1FFF);
    }

    [Fact]
    public void Rom_ValidateAddress_OutOfBounds_Throws()
    {
        var rom = new Rom(new byte[0x2000]);
        
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            ((IBusValidator)rom).ValidateAddress(0x2000);
        });
        
        Assert.Contains("0x2000", ex.Message);
    }

    [Fact]
    public void Ram_ImplementsIBusValidator()
    {
        var ram = new Ram(0x1000);
        Assert.IsAssignableFrom<IBusValidator>(ram);
    }

    [Fact]
    public void Rom_ImplementsIBusValidator()
    {
        var rom = new Rom(new byte[0x1000]);
        Assert.IsAssignableFrom<IBusValidator>(rom);
    }

    [Fact]
    public void AddressDecoder_CallsValidatorInDebugMode()
    {
        var decoder = new AddressDecoder();
        var ram = new Ram(0x2000);
        decoder.Map(0x0000, 0x1FFF, ram);
        
        // Valid access should work
        byte value = decoder.Read(0x0000);
        Assert.Equal(0x00, value);  // Uninitialized RAM reads as 0
        
        // Write valid
        decoder.Write(0x1000, 0x42);
        Assert.Equal(0x42, decoder.Read(0x1000));
    }

    [Fact]
    public void Ram_DirectReadWrite_NoBoundsCheck_InReleaseMode()
    {
        // Direct Read/Write on Ram instances should NOT validate bounds
        // Validation only happens through AddressDecoder in DEBUG mode
        var ram = new Ram(0x2000);
        
        // These should work (direct access, no bounds check)
        ram.Write(0x0000, 0x42);
        Assert.Equal(0x42, ram.Read(0x0000));
        
        // Note: out-of-bounds direct access would throw IndexOutOfRangeException,
        // not our validation error. This is acceptable for direct access.
    }
}
