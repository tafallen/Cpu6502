namespace Cpu6502.Core;

public sealed partial class Cpu
{
    // ── Register transfers ────────────────────────────────────────────────────
    private void TAX() { X = A; SetZN(X); TotalCycles += 2; }
    private void TXA() { A = X; SetZN(A); TotalCycles += 2; }
    private void TAY() { Y = A; SetZN(Y); TotalCycles += 2; }
    private void TYA() { A = Y; SetZN(A); TotalCycles += 2; }
    private void TSX() { X = SP; SetZN(X); TotalCycles += 2; }
    private void TXS() { SP = X;          TotalCycles += 2; }   // TXS does NOT set flags

    // ── Stack ─────────────────────────────────────────────────────────────────
    private void PHA() { StackPush(A);              TotalCycles += 3; }
    private void PLA() { A = StackPull(); SetZN(A); TotalCycles += 4; }

    private void PHP()
    {
        // PHP always pushes B=1 and Unused=1
        StackPush(GetStatus(breakFlag: true));
        TotalCycles += 3;
    }

    private void PLP()
    {
        // PLP restores all flags; B and Unused bits from the stack are discarded
        SetStatus(StackPull());
        TotalCycles += 4;
    }
}
