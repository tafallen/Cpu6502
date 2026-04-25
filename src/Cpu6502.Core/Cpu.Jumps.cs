namespace Cpu6502.Core;

public sealed partial class Cpu
{
    // ── JMP ───────────────────────────────────────────────────────────────────
    private void JMP_Abs()
    {
        PC = AddrAbsolute();
        TotalCycles += 3;
    }

    private void JMP_Ind()
    {
        ushort ptr = AddrAbsolute();
        PC = ReadWordBug(ptr);  // 6502 page-wrap bug: $xxFF wraps hi byte to $xx00
        TotalCycles += 5;
    }

    // ── JSR / RTS ─────────────────────────────────────────────────────────────
    private void JSR()
    {
        ushort target = AddrAbsolute();
        StackPushWord((ushort)(PC - 1));  // push return address - 1 (6502 quirk)
        PC = target;
        TotalCycles += 6;
    }

    private void RTS()
    {
        PC = (ushort)(StackPullWord() + 1);  // add 1 to the stored address
        TotalCycles += 6;
    }

    // ── BRK ───────────────────────────────────────────────────────────────────
    private void BRK()
    {
        PC++;                               // skip padding byte
        StackPushWord(PC);
        StackPush(GetStatus(breakFlag: true));
        I  = true;
        PC = ReadWord(0xFFFE);             // IRQ/BRK vector
        TotalCycles += 7;
    }

    // ── RTI ───────────────────────────────────────────────────────────────────
    private void RTI()
    {
        SetStatus(StackPull());            // pull P (B and Unused bits discarded by SetStatus)
        PC = StackPullWord();              // pull PC — no +1 unlike RTS
        TotalCycles += 6;
    }
}
