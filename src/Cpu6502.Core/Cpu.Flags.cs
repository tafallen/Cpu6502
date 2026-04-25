namespace Cpu6502.Core;

public sealed partial class Cpu
{
    private void CLC() { C = false; TotalCycles += 2; }
    private void SEC() { C = true;  TotalCycles += 2; }
    private void CLI() { I = false; TotalCycles += 2; }
    private void SEI() { I = true;  TotalCycles += 2; }
    private void CLD() { D = false; TotalCycles += 2; }
    private void SED() { D = true;  TotalCycles += 2; }
    private void CLV() { V = false; TotalCycles += 2; }
}
