using System.Runtime.CompilerServices;

namespace Cpu6502.Core;

public sealed partial class Cpu
{
    private void BCC() => Branch(!C);
    private void BCS() => Branch(C);
    private void BEQ() => Branch(Z);
    private void BNE() => Branch(!Z);
    private void BMI() => Branch(N);
    private void BPL() => Branch(!N);
    private void BVS() => Branch(V);
    private void BVC() => Branch(!V);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Branch(bool condition)
    {
        sbyte offset = (sbyte)Fetch();  // signed relative offset
        TotalCycles += 2;               // base cycles for branch instruction

        if (!condition) return;

        ushort pcAfterFetch = PC;
        ushort target = (ushort)(pcAfterFetch + offset);
        TotalCycles++;                  // +1 for taken branch
        if ((pcAfterFetch & 0xFF00) != (target & 0xFF00))
            TotalCycles++;              // +1 for page cross boundary
        PC = target;
    }
}
