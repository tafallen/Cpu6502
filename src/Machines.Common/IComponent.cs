namespace Machines.Common;

/// <summary>
/// Component that requires initialization validation.
/// Implement to validate that all required dependencies are properly wired before execution.
/// </summary>
public interface IComponent
{
    /// <summary>
    /// Validate that all required dependencies are initialized.
    /// Throws <see cref="InvalidOperationException"/> if validation fails.
    /// </summary>
    /// <exception cref="InvalidOperationException">When required dependencies are not properly initialized.</exception>
    void ValidateInitialization();
}
