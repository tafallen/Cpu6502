namespace Cpu6502.Core;

/// <summary>
/// Optional interface for IBus implementations to provide address validation.
///
/// Devices like Ram and Rom can implement this interface to catch configuration errors
/// at development time. Validation is called during DEBUG builds only via AddressDecoder,
/// providing zero performance impact in release builds.
///
/// Example:
///   public sealed class CustomPeripheral : IBusValidator
///   {
///       private readonly ushort _size = 0x100;
///       
///       public void ValidateAddress(ushort address)
///       {
///           if (address >= _size)
///               throw new InvalidOperationException(
///                   $"CustomPeripheral access at 0x{address:X4} exceeds size 0x{_size:X4}");
///       }
///       
///       public byte Read(ushort address) => /* implementation */;
///       public void Write(ushort address, byte value) => /* implementation */;
///   }
/// </summary>
public interface IBusValidator : IBus
{
    /// <summary>
    /// Validate that the given address is within the device's accessible range.
    /// Throws InvalidOperationException with a descriptive error message if out of bounds.
    /// </summary>
    /// <param name="address">The address to validate (zero-based offset within the device's mapped range)</param>
    /// <exception cref="InvalidOperationException">Thrown if address is out of bounds</exception>
    void ValidateAddress(ushort address);
}
