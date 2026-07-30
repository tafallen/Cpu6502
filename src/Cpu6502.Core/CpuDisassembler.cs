using System.Text;

namespace Cpu6502.Core;

/// <summary>
/// 6502 opcode disassembler supporting optional symbol map lookups.
/// Converts raw binary code bytes into standard MOS 6502 assembly mnemonics.
/// </summary>
public static class CpuDisassembler
{
    private static readonly string[] Mnemonics = new string[256];

    static CpuDisassembler()
    {
        Array.Fill(Mnemonics, "NOP");

        // Load/Store
        Mnemonics[0xA9] = "LDA"; Mnemonics[0xA5] = "LDA"; Mnemonics[0xB5] = "LDA"; Mnemonics[0xAD] = "LDA"; Mnemonics[0xBD] = "LDA"; Mnemonics[0xB9] = "LDA"; Mnemonics[0xA1] = "LDA"; Mnemonics[0xB1] = "LDA";
        Mnemonics[0xA2] = "LDX"; Mnemonics[0xA6] = "LDX"; Mnemonics[0xB6] = "LDX"; Mnemonics[0xAE] = "LDX"; Mnemonics[0xBE] = "LDX";
        Mnemonics[0xA0] = "LDY"; Mnemonics[0xA4] = "LDY"; Mnemonics[0xB4] = "LDY"; Mnemonics[0xAC] = "LDY"; Mnemonics[0xBC] = "LDY";
        Mnemonics[0x85] = "STA"; Mnemonics[0x95] = "STA"; Mnemonics[0x8D] = "STA"; Mnemonics[0x9D] = "STA"; Mnemonics[0x99] = "STA"; Mnemonics[0x81] = "STA"; Mnemonics[0x91] = "STA";
        Mnemonics[0x86] = "STX"; Mnemonics[0x96] = "STX"; Mnemonics[0x8E] = "STX";
        Mnemonics[0x84] = "STY"; Mnemonics[0x94] = "STY"; Mnemonics[0x8C] = "STY";

        // Arithmetic
        Mnemonics[0x69] = "ADC"; Mnemonics[0x65] = "ADC"; Mnemonics[0x75] = "ADC"; Mnemonics[0x6D] = "ADC"; Mnemonics[0x7D] = "ADC"; Mnemonics[0x79] = "ADC"; Mnemonics[0x61] = "ADC"; Mnemonics[0x71] = "ADC";
        Mnemonics[0xE9] = "SBC"; Mnemonics[0xE5] = "SBC"; Mnemonics[0xF5] = "SBC"; Mnemonics[0xED] = "SBC"; Mnemonics[0xFD] = "SBC"; Mnemonics[0xF9] = "SBC"; Mnemonics[0xE1] = "SBC"; Mnemonics[0xF1] = "SBC";

        // Branches & Jumps
        Mnemonics[0x90] = "BCC"; Mnemonics[0xB0] = "BCS"; Mnemonics[0xF0] = "BEQ"; Mnemonics[0xD0] = "BNE";
        Mnemonics[0x30] = "BMI"; Mnemonics[0x10] = "BPL"; Mnemonics[0x50] = "BVC"; Mnemonics[0x70] = "BVS";
        Mnemonics[0x4C] = "JMP"; Mnemonics[0x6C] = "JMP"; Mnemonics[0x20] = "JSR"; Mnemonics[0x60] = "RTS"; Mnemonics[0x40] = "RTI";

        // Flags & Register Transfers
        Mnemonics[0x18] = "CLC"; Mnemonics[0x38] = "SEC"; Mnemonics[0x58] = "CLI"; Mnemonics[0x78] = "SEI"; Mnemonics[0xB8] = "CLV"; Mnemonics[0xD8] = "CLD"; Mnemonics[0xF8] = "SED";
        Mnemonics[0xAA] = "TAX"; Mnemonics[0x8A] = "TXA"; Mnemonics[0xA8] = "TAY"; Mnemonics[0x98] = "TYA"; Mnemonics[0x9A] = "TXS"; Mnemonics[0xBA] = "TSX";
        Mnemonics[0x48] = "PHA"; Mnemonics[0x68] = "PLA"; Mnemonics[0x08] = "PHP"; Mnemonics[0x28] = "PLP";
        Mnemonics[0xE8] = "INX"; Mnemonics[0xCA] = "DEX"; Mnemonics[0xC8] = "INY"; Mnemonics[0x88] = "DEY";
        Mnemonics[0xEA] = "NOP"; Mnemonics[0x00] = "BRK";
    }

    /// <summary>
    /// Disassembles a single instruction at <paramref name="pc"/> from <paramref name="bus"/>.
    /// Returns the assembly string and total instruction byte length.
    /// </summary>
    public static (string Mnemonic, int Length) Disassemble(IBus bus, ushort pc, IDictionary<ushort, string>? symbols = null)
    {
        byte opcode = bus.Read(pc);
        string name = Mnemonics[opcode];

        string labelStr = symbols is not null && symbols.TryGetValue(pc, out var sym) ? $"{sym}: " : "";

        // Immediate (#$xx)
        if (opcode is 0xA9 or 0xA2 or 0xA0 or 0x69 or 0xE9 or 0xC9 or 0xE0 or 0xC0 or 0x49 or 0x09 or 0x29)
        {
            byte imm = bus.Read((ushort)(pc + 1));
            return ($"{labelStr}{name} #${imm:X2}", 2);
        }

        // Relative branch
        if (opcode is 0x90 or 0xB0 or 0xF0 or 0xD0 or 0x30 or 0x10 or 0x50 or 0x70)
        {
            sbyte offset = (sbyte)bus.Read((ushort)(pc + 1));
            ushort target = (ushort)(pc + 2 + offset);
            string targetStr = symbols is not null && symbols.TryGetValue(target, out var targetSym) ? targetSym : $"${target:X4}";
            return ($"{labelStr}{name} {targetStr}", 2);
        }

        // Absolute ($xxxx)
        if (opcode is 0x4C or 0x20 or 0xAD or 0xAE or 0xAC or 0x8D or 0x8E or 0x8C or 0x6D or 0xED or 0xCD or 0xEC or 0xCC or 0x4D or 0x0D or 0x2D or 0x2E or 0x0E or 0x4E or 0x6E or 0xCE or 0xEE)
        {
            byte lo = bus.Read((ushort)(pc + 1));
            byte hi = bus.Read((ushort)(pc + 2));
            ushort target = (ushort)((hi << 8) | lo);
            string targetStr = symbols is not null && symbols.TryGetValue(target, out var targetSym) ? targetSym : $"${target:X4}";
            return ($"{labelStr}{name} {targetStr}", 3);
        }

        // Zero page ($xx)
        if (opcode is 0xA5 or 0xA6 or 0xA4 or 0x85 or 0x86 or 0x84 or 0x65 or 0xE5 or 0xC5 or 0xE4 or 0xC4 or 0x45 or 0x05 or 0x25 or 0x26 or 0x06 or 0x46 or 0x66 or 0xC6 or 0xE6)
        {
            byte zp = bus.Read((ushort)(pc + 1));
            return ($"{labelStr}{name} ${zp:X2}", 2);
        }

        // Single byte instructions
        return ($"{labelStr}{name}", 1);
    }
}
