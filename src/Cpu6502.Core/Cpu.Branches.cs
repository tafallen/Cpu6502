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

    private void Branch(bool condition)
    {
        sbyte offset = (sbyte)Fetch();  // signed relative offset
        TotalCycles += (ulong)GetCycleInfo(AddressingMode.Relative, AccessType.Read).BaseCycles;

        if (!condition) return;

        ushort newPc = (ushort)(PC + offset);
        TotalCycles++;                              // +1 for taken branch
        if (PageCrossed(PC, newPc)) TotalCycles++; // +1 for page cross
        PC = newPc;
    }
}
