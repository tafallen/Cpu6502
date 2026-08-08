using System.Runtime.CompilerServices;

namespace Cpu6502.Core;

/// <summary>
/// MOS 6502 CPU. Executes one instruction per Step() call.
/// All 151 legal opcodes implemented with cycle-accurate timing including page-cross penalties.
/// </summary>
public sealed partial class Cpu
{
    // ── Registers ────────────────────────────────────────────────────────────
    public byte   A  { get; private set; }
    public byte   X  { get; private set; }
    public byte   Y  { get; private set; }
    public byte   SP { get; private set; }
    public ushort PC { get; private set; }

    // ── Processor Status Register (P) ─────────────────────────────────────────
    public byte P { get; private set; }

    public bool C
    {
        get => (P & (byte)StatusFlags.Carry) != 0;
        private set => P = value ? (byte)(P | (byte)StatusFlags.Carry) : (byte)(P & ~(byte)StatusFlags.Carry);
    }
    public bool Z
    {
        get => (P & (byte)StatusFlags.Zero) != 0;
        private set => P = value ? (byte)(P | (byte)StatusFlags.Zero) : (byte)(P & ~(byte)StatusFlags.Zero);
    }
    public bool I
    {
        get => (P & (byte)StatusFlags.InterruptDisable) != 0;
        private set => P = value ? (byte)(P | (byte)StatusFlags.InterruptDisable) : (byte)(P & ~(byte)StatusFlags.InterruptDisable);
    }
    public bool D
    {
        get => (P & (byte)StatusFlags.Decimal) != 0;
        private set => P = value ? (byte)(P | (byte)StatusFlags.Decimal) : (byte)(P & ~(byte)StatusFlags.Decimal);
    }
    public bool V
    {
        get => (P & (byte)StatusFlags.Overflow) != 0;
        private set => P = value ? (byte)(P | (byte)StatusFlags.Overflow) : (byte)(P & ~(byte)StatusFlags.Overflow);
    }
    public bool N
    {
        get => (P & (byte)StatusFlags.Negative) != 0;
        private set => P = value ? (byte)(P | (byte)StatusFlags.Negative) : (byte)(P & ~(byte)StatusFlags.Negative);
    }

    // ── Cycle counter ─────────────────────────────────────────────────────────
    public ulong TotalCycles { get; private set; }

    // ── Hardware ─────────────────────────────────────────────────────────────
    private readonly IBus _bus;

    // ── Pending interrupt state ───────────────────────────────────────────────
    private bool _nmiPending;
    private bool _irqPending;

    // ── Execution tracing ────────────────────────────────────────────────────
    private IExecutionTrace _trace = NullTrace.Instance;
    private ulong _memoryAccessCount;  // Counter for sampling

    // ── Precomputed ZN Lookup Table ─────────────────────────────────────────
    private static readonly byte[] ZnTable = BuildZnTable();

    private static byte[] BuildZnTable()
    {
        var table = new byte[256];
        table[0] = (byte)StatusFlags.Zero;
        for (int i = 1; i < 256; i++)
        {
            byte flags = 0;
            if ((i & 0x80) != 0) flags |= (byte)StatusFlags.Negative;
            table[i] = flags;
        }
        return table;
    }

    public Cpu(IBus bus)
    {
        _bus = bus;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Performs a hardware reset: reads RESET vector ($FFFC/$FFFD), sets PC, clears most flags.
    /// Takes 7 cycles like real hardware.
    /// </summary>
    public void Reset()
    {
        A  = 0;
        X  = 0;
        Y  = 0;
        SP = CpuConstants.INITIAL_STACK_POINTER;
        P  = (byte)(StatusFlags.Unused | StatusFlags.InterruptDisable);

        PC = ReadWord(CpuConstants.RESET_VECTOR);
        TotalCycles += 7;
    }

    /// <summary>Executes one instruction (fetches opcode, dispatches, updates cycles).</summary>
    public void Step()
    {
        ulong before = TotalCycles;

        // Service interrupts before executing the next instruction
        if (_nmiPending)
            ServiceNmi();
        else if (_irqPending && !I)
            ServiceIrq();
        else
        {
            ushort pcBefore = PC;
            byte opcode = Fetch();  // Fetch opcode (increments PC and records memory access)
            
            _trace.OnInstructionFetched(pcBefore, opcode);
            
            // Check for conditional breakpoint (before execution)
            if (_trace.ShouldBreak(pcBefore, opcode, A))
            {
                PC = pcBefore;
                throw new BreakException(pcBefore, opcode, A);
            }
            
            ExecuteOpcode(opcode);
            
            int consumed = (int)(TotalCycles - before);
            _trace.OnInstructionExecuted(pcBefore, opcode, consumed, A, GetStatus());
        }

        int totalConsumed = (int)(TotalCycles - before);
        OnCyclesConsumed?.Invoke(totalConsumed);
    }

    /// <summary>Request a maskable interrupt (IRQ). Serviced before next instruction if I=0.</summary>
    public void Irq() => _irqPending = true;

    /// <summary>Called at the moment an IRQ is serviced (before PC changes). Parameter = interrupted PC.</summary>
    public Action<ushort>? OnIrqServiced { get; set; }

    /// <summary>Request a non-maskable interrupt (NMI). Always serviced before next instruction.</summary>
    public void Nmi() => _nmiPending = true;

    /// <summary>Called after each Step() with the exact number of cycles consumed by that step.</summary>
    public Action<int>? OnCyclesConsumed { get; set; }

    /// <summary>
    /// Execution trace sink for debugging, profiling, and test instrumentation.
    /// Defaults to NullTrace (zero-cost no-op); set to custom IExecutionTrace to enable tracing.
    /// Setting to null coerces to NullTrace.Instance.
    /// </summary>
    public IExecutionTrace? Trace
    {
        get => _trace;
        set => _trace = value ?? NullTrace.Instance;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Status register helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the processor status byte.
    /// Bit 5 (Unused) is always set. Bit 4 (Break) reflects <paramref name="breakFlag"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte GetStatus(bool breakFlag = false)
    {
        return (byte)(P | (byte)StatusFlags.Unused | (breakFlag ? (byte)StatusFlags.Break : 0));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetStatus(byte value)
    {
        P = (byte)((value & ~(byte)StatusFlags.Break) | (byte)StatusFlags.Unused);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Interrupt service routines
    // ─────────────────────────────────────────────────────────────────────────

    private void ServiceNmi()
    {
        _nmiPending = false;
        StackPushWord(PC);
        StackPush(GetStatus(breakFlag: false));
        I  = true;
        ushort handlerAddress = ReadWord(CpuConstants.NMI_VECTOR);
        _trace.OnInterrupt(InterruptType.Nmi, handlerAddress);
        PC = handlerAddress;
        TotalCycles += 7;
    }

    private void ServiceIrq()
    {
        OnIrqServiced?.Invoke(PC);
        _irqPending = false;
        StackPushWord(PC);
        StackPush(GetStatus(breakFlag: false));
        I  = true;
        ushort handlerAddress = ReadWord(CpuConstants.IRQ_VECTOR);
        _trace.OnInterrupt(InterruptType.Irq, handlerAddress);
        PC = handlerAddress;
        TotalCycles += 7;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Bus helpers
    // ─────────────────────────────────────────────────────────────────────────

    private byte   Peek() => _bus.Read(PC);  // Read opcode without incrementing PC or tracing

    // ─────────────────────────────────────────────────────────────────────────
    // Debugging helpers (for GDB adapter)
    // ─────────────────────────────────────────────────────────────────────────

    internal void SetRegisterA(byte value) => A = value;
    internal void SetRegisterX(byte value) => X = value;
    internal void SetRegisterY(byte value) => Y = value;
    internal void SetStackPointer(byte value) => SP = value;
    internal void SetProgramCounter(ushort value) => PC = value;
    internal void SetFlagC(bool value) => C = value;
    internal void SetFlagZ(bool value) => Z = value;
    internal void SetFlagI(bool value) => I = value;
    internal void SetFlagD(bool value) => D = value;
    internal void SetFlagV(bool value) => V = value;
    internal void SetFlagN(bool value) => N = value;

    private byte   Fetch()
    {
        byte value = _bus.Read(PC++);
        ushort fetchAddr = (ushort)(PC - 1);
        if (_trace.ShouldRecordMemoryAccess(fetchAddr, isWrite: false) && ++_memoryAccessCount % (ulong)_trace.MemoryAccessSampleRate == 0)
            _trace.OnMemoryAccess(fetchAddr, value, isWrite: false, cycles: TotalCycles);
        return value;
    }

    private byte   ReadByte(ushort a)
    {
        byte value = _bus.Read(a);
        if (_trace.ShouldRecordMemoryAccess(a, isWrite: false) && ++_memoryAccessCount % (ulong)_trace.MemoryAccessSampleRate == 0)
            _trace.OnMemoryAccess(a, value, isWrite: false, cycles: TotalCycles);
        return value;
    }

    private void   WriteByte(ushort a, byte v)
    {
        if (_trace.ShouldRecordMemoryAccess(a, isWrite: true) && ++_memoryAccessCount % (ulong)_trace.MemoryAccessSampleRate == 0)
            _trace.OnMemoryAccess(a, v, isWrite: true, cycles: TotalCycles);
        _bus.Write(a, v);
    }

    private ushort ReadWord(ushort a) =>
        (ushort)(ReadByte(a) | (ReadByte((ushort)(a + 1)) << 8));

    // 6502 page-wrap bug: $xxFF wraps to $xx00 for the high byte
    private ushort ReadWordBug(ushort a)
    {
        ushort hi = (ushort)((a & CpuConstants.PAGE_MASK) | ((a + 1) & 0x00FF));
        return (ushort)(ReadByte(a) | (ReadByte(hi) << 8));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Stack helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void  StackPush(byte v)       => WriteByte((ushort)(CpuConstants.STACK_PAGE_BASE | SP--), v);
    private byte  StackPull()             => ReadByte((ushort)(CpuConstants.STACK_PAGE_BASE | ++SP));
    private void  StackPushWord(ushort v) { StackPush((byte)(v >> 8)); StackPush((byte)(v & 0xFF)); }
    private ushort StackPullWord()        { byte lo = StackPull(); byte hi = StackPull(); return (ushort)((hi << 8) | lo); }

    // ─────────────────────────────────────────────────────────────────────────
    // Flag helpers
    // ─────────────────────────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetZN(byte v) => P = (byte)((P & ~0x82) | ZnTable[v]);

    // ─────────────────────────────────────────────────────────────────────────
    // Native Jump-Table Opcode Dispatcher
    // ─────────────────────────────────────────────────────────────────────────

    private void ExecuteOpcode(byte opcode)
    {
        switch (opcode)
        {
            // NOP
            case 0xEA: TotalCycles += 2; break;

            // LDA
            case 0xA9: LDA_Immediate(); break;
            case 0xA5: LDA_ZeroPage(); break;
            case 0xB5: LDA_ZeroPageX(); break;
            case 0xAD: LDA_Absolute(); break;
            case 0xBD: LDA_AbsoluteX(); break;
            case 0xB9: LDA_AbsoluteY(); break;
            case 0xA1: LDA_IndirectX(); break;
            case 0xB1: LDA_IndirectY(); break;
            case 0xB2: LDA_ZeroPageIndirect(); break;

            // LDX
            case 0xA2: LDX_Immediate(); break;
            case 0xA6: LDX_ZeroPage(); break;
            case 0xB6: LDX_ZeroPageY(); break;
            case 0xAE: LDX_Absolute(); break;
            case 0xBE: LDX_AbsoluteY(); break;

            // LDY
            case 0xA0: LDY_Immediate(); break;
            case 0xA4: LDY_ZeroPage(); break;
            case 0xB4: LDY_ZeroPageX(); break;
            case 0xAC: LDY_Absolute(); break;
            case 0xBC: LDY_AbsoluteX(); break;

            // STA
            case 0x85: STA_ZeroPage(); break;
            case 0x95: STA_ZeroPageX(); break;
            case 0x8D: STA_Absolute(); break;
            case 0x9D: STA_AbsoluteX(); break;
            case 0x99: STA_AbsoluteY(); break;
            case 0x81: STA_IndirectX(); break;
            case 0x91: STA_IndirectY(); break;

            // STX
            case 0x86: STX_ZeroPage(); break;
            case 0x96: STX_ZeroPageY(); break;
            case 0x8E: STX_Absolute(); break;

            // STY
            case 0x84: STY_ZeroPage(); break;
            case 0x94: STY_ZeroPageX(); break;
            case 0x8C: STY_Absolute(); break;

            // Register transfers
            case 0xAA: TAX(); break;
            case 0x8A: TXA(); break;
            case 0xA8: TAY(); break;
            case 0x98: TYA(); break;
            case 0xBA: TSX(); break;
            case 0x9A: TXS(); break;

            // Stack
            case 0x48: PHA(); break;
            case 0x68: PLA(); break;
            case 0x08: PHP(); break;
            case 0x28: PLP(); break;

            // ADC
            case 0x69: ADC_Immediate(); break;
            case 0x65: ADC_ZeroPage(); break;
            case 0x75: ADC_ZeroPageX(); break;
            case 0x6D: ADC_Absolute(); break;
            case 0x7D: ADC_AbsoluteX(); break;
            case 0x79: ADC_AbsoluteY(); break;
            case 0x61: ADC_IndirectX(); break;
            case 0x71: ADC_IndirectY(); break;

            // SBC
            case 0xE9: SBC_Immediate(); break;
            case 0xE5: SBC_ZeroPage(); break;
            case 0xF5: SBC_ZeroPageX(); break;
            case 0xED: SBC_Absolute(); break;
            case 0xFD: SBC_AbsoluteX(); break;
            case 0xF9: SBC_AbsoluteY(); break;
            case 0xE1: SBC_IndirectX(); break;
            case 0xF1: SBC_IndirectY(); break;

            // INC
            case 0xE6: INC_Zp(); break;
            case 0xF6: INC_ZpX(); break;
            case 0xEE: INC_Abs(); break;
            case 0xFE: INC_AbsX(); break;
            case 0xE8: INX(); break;
            case 0xC8: INY(); break;

            // DEC
            case 0xC6: DEC_Zp(); break;
            case 0xD6: DEC_ZpX(); break;
            case 0xCE: DEC_Abs(); break;
            case 0xDE: DEC_AbsX(); break;
            case 0xCA: DEX(); break;
            case 0x88: DEY(); break;

            // AND
            case 0x29: AND_Immediate(); break;
            case 0x25: AND_ZeroPage(); break;
            case 0x35: AND_ZeroPageX(); break;
            case 0x2D: AND_Absolute(); break;
            case 0x3D: AND_AbsoluteX(); break;
            case 0x39: AND_AbsoluteY(); break;
            case 0x21: AND_IndirectX(); break;
            case 0x31: AND_IndirectY(); break;

            // ORA
            case 0x09: ORA_Immediate(); break;
            case 0x05: ORA_ZeroPage(); break;
            case 0x15: ORA_ZeroPageX(); break;
            case 0x0D: ORA_Absolute(); break;
            case 0x1D: ORA_AbsoluteX(); break;
            case 0x19: ORA_AbsoluteY(); break;
            case 0x01: ORA_IndirectX(); break;
            case 0x11: ORA_IndirectY(); break;

            // EOR
            case 0x49: EOR_Immediate(); break;
            case 0x45: EOR_ZeroPage(); break;
            case 0x55: EOR_ZeroPageX(); break;
            case 0x4D: EOR_Absolute(); break;
            case 0x5D: EOR_AbsoluteX(); break;
            case 0x59: EOR_AbsoluteY(); break;
            case 0x41: EOR_IndirectX(); break;
            case 0x51: EOR_IndirectY(); break;

            // BIT
            case 0x24: BIT_ZeroPage(); break;
            case 0x2C: BIT_Absolute(); break;

            // Shifts
            case 0x0A: ASL_Acc(); break;
            case 0x06: ASL_Zp(); break;
            case 0x16: ASL_ZpX(); break;
            case 0x0E: ASL_Abs(); break;
            case 0x1E: ASL_AbsX(); break;

            case 0x4A: LSR_Acc(); break;
            case 0x46: LSR_Zp(); break;
            case 0x56: LSR_ZpX(); break;
            case 0x4E: LSR_Abs(); break;
            case 0x5E: LSR_AbsX(); break;

            case 0x2A: ROL_Acc(); break;
            case 0x26: ROL_Zp(); break;
            case 0x36: ROL_ZpX(); break;
            case 0x2E: ROL_Abs(); break;
            case 0x3E: ROL_AbsX(); break;

            case 0x6A: ROR_Acc(); break;
            case 0x66: ROR_Zp(); break;
            case 0x76: ROR_ZpX(); break;
            case 0x6E: ROR_Abs(); break;
            case 0x7E: ROR_AbsX(); break;

            // Compare
            case 0xC9: CMP_Immediate(); break;
            case 0xC5: CMP_ZeroPage(); break;
            case 0xD5: CMP_ZeroPageX(); break;
            case 0xCD: CMP_Absolute(); break;
            case 0xDD: CMP_AbsoluteX(); break;
            case 0xD9: CMP_AbsoluteY(); break;
            case 0xC1: CMP_IndirectX(); break;
            case 0xD1: CMP_IndirectY(); break;

            case 0xE0: CPX_Immediate(); break;
            case 0xE4: CPX_ZeroPage(); break;
            case 0xEC: CPX_Absolute(); break;

            case 0xC0: CPY_Immediate(); break;
            case 0xC4: CPY_ZeroPage(); break;
            case 0xCC: CPY_Absolute(); break;

            // Branches
            case 0x90: BCC(); break;
            case 0xB0: BCS(); break;
            case 0xF0: BEQ(); break;
            case 0xD0: BNE(); break;
            case 0x30: BMI(); break;
            case 0x10: BPL(); break;
            case 0x70: BVS(); break;
            case 0x50: BVC(); break;
            case 0x80: Branch(true); break; // BRA (65C02 Branch Always)

            // Jumps / Calls
            case 0x4C: JMP_Abs(); break;
            case 0x6C: JMP_Ind(); break;
            case 0x7C: { ushort b = (ushort)(Fetch() | (Fetch() << 8)); PC = ReadWord((ushort)(b + X)); TotalCycles += 6; break; } // JMP (abs,X) (65C02)
            case 0x20: JSR(); break;
            case 0x60: RTS(); break;
            case 0x00: BRK(); break;
            case 0x40: RTI(); break;

            // 65C02 Stack / Register opcodes
            case 0x1A: A++; SetZN(A); TotalCycles += 2; break; // INC A (INA)
            case 0x3A: A--; SetZN(A); TotalCycles += 2; break; // DEC A (DEA)
            case 0x5A: StackPush(Y); TotalCycles += 3; break;  // PHY
            case 0x7A: Y = StackPull(); SetZN(Y); TotalCycles += 4; break; // PLY
            case 0xDA: StackPush(X); TotalCycles += 3; break;  // PHX
            case 0xFA: X = StackPull(); SetZN(X); TotalCycles += 4; break; // PLX

            // 65C02 STZ (Store Zero)
            case 0x64: WriteByte((ushort)Fetch(), 0); TotalCycles += 3; break; // STZ zp
            case 0x74: WriteByte((ushort)((Fetch() + X) & 0xFF), 0); TotalCycles += 4; break; // STZ zp,X

            // 65C02 (zp) Indirect Opcodes
            case 0x12: { byte zp = Fetch(); A |= ReadByte(ReadWord(zp)); SetZN(A); TotalCycles += 5; break; } // ORA (zp)
            case 0x32: { byte zp = Fetch(); A &= ReadByte(ReadWord(zp)); SetZN(A); TotalCycles += 5; break; } // AND (zp)
            case 0x52: { byte zp = Fetch(); A ^= ReadByte(ReadWord(zp)); SetZN(A); TotalCycles += 5; break; } // EOR (zp)
            case 0x72: { byte zp = Fetch(); AdcCore(ReadByte(ReadWord(zp))); TotalCycles += 5; break; }       // ADC (zp)
            case 0x92: { byte zp = Fetch(); WriteByte(ReadWord(zp), A); TotalCycles += 5; break; }            // STA (zp)
            case 0xD2: { byte zp = Fetch(); DoCMP(A, ReadByte(ReadWord(zp))); TotalCycles += 5; break; }       // CMP (zp)
            case 0xF2: { byte zp = Fetch(); SbcCore(ReadByte(ReadWord(zp))); TotalCycles += 5; break; }       // SBC (zp)

            // Flags
            case 0x18: CLC(); break;
            case 0x38: SEC(); break;
            case 0x58: CLI(); break;
            case 0x78: SEI(); break;
            case 0xD8: CLD(); break;
            case 0xF8: SED(); break;
            case 0xB8: CLV(); break;

            // Illegal opcodes
            case 0x82: case 0x89: case 0xC2: case 0xE2: Fetch(); TotalCycles += 2; break;
            case 0x04: case 0x44: ReadByte((ushort)Fetch()); TotalCycles += 3; break;
            case 0x14: case 0x34: case 0x54: case 0xF4: ReadByte((ushort)((Fetch() + X) & 0xFF)); TotalCycles += 4; break;
            case 0x0C: { ushort a = (ushort)(Fetch() | (Fetch() << 8)); ReadByte(a); TotalCycles += 4; break; }
            case 0x1C: case 0x3C: case 0x5C: case 0xDC: case 0xFC:
                { ushort b = (ushort)(Fetch() | (Fetch() << 8)); ReadByte((ushort)(b + X)); TotalCycles += 4; break; }

            // LAX
            case 0xA3: { byte v = ReadByte(AddrIndexedIndirect()); A = X = v; SetZN(v); TotalCycles += 6; break; }
            case 0xA7: { byte v = ReadByte(AddrZeroPage()); A = X = v; SetZN(v); TotalCycles += 3; break; }
            case 0xAF: { byte v = ReadByte(AddrAbsolute()); A = X = v; SetZN(v); TotalCycles += 4; break; }
            case 0xB3: { byte v = ReadByte(AddrIndirectIndexed()); A = X = v; SetZN(v); TotalCycles += 5; break; }
            case 0xB7: { byte v = ReadByte(AddrZeroPageY()); A = X = v; SetZN(v); TotalCycles += 4; break; }
            case 0xBF: { byte v = ReadByte(AddrAbsoluteY()); A = X = v; SetZN(v); TotalCycles += 4; break; }

            // SAX
            case 0x83: WriteByte(AddrIndexedIndirect(), (byte)(A & X)); TotalCycles += 6; break;
            case 0x87: WriteByte(AddrZeroPage(), (byte)(A & X)); TotalCycles += 3; break;
            case 0x8F: WriteByte(AddrAbsolute(), (byte)(A & X)); TotalCycles += 4; break;
            case 0x97: WriteByte(AddrZeroPageY(), (byte)(A & X)); TotalCycles += 4; break;

            // DCP
            case 0xC3: DcpAt(AddrIndexedIndirect()); TotalCycles += 8; break;
            case 0xC7: DcpAt(AddrZeroPage()); TotalCycles += 5; break;
            case 0xCF: DcpAt(AddrAbsolute()); TotalCycles += 6; break;
            case 0xD3: DcpAt(AddrIndirectIndexed()); TotalCycles += 8; break;
            case 0xD7: DcpAt(AddrZeroPageX()); TotalCycles += 6; break;
            case 0xDB: DcpAt(AddrAbsoluteY()); TotalCycles += 7; break;
            case 0xDF: DcpAt(AddrAbsoluteX()); TotalCycles += 7; break;

            // ISB
            case 0xE3: IsbAt(AddrIndexedIndirect()); TotalCycles += 8; break;
            case 0xE7: IsbAt(AddrZeroPage()); TotalCycles += 5; break;
            case 0xEF: IsbAt(AddrAbsolute()); TotalCycles += 6; break;
            case 0xF3: IsbAt(AddrIndirectIndexed()); TotalCycles += 8; break;
            case 0xF7: IsbAt(AddrZeroPageX()); TotalCycles += 6; break;
            case 0xFB: IsbAt(AddrAbsoluteY()); TotalCycles += 7; break;
            case 0xFF: IsbAt(AddrAbsoluteX()); TotalCycles += 7; break;

            // SLO
            case 0x03: SloAt(AddrIndexedIndirect()); TotalCycles += 8; break;
            case 0x07: SloAt(AddrZeroPage()); TotalCycles += 5; break;
            case 0x0F: SloAt(AddrAbsolute()); TotalCycles += 6; break;
            case 0x13: SloAt(AddrIndirectIndexed()); TotalCycles += 8; break;
            case 0x17: SloAt(AddrZeroPageX()); TotalCycles += 6; break;
            case 0x1B: SloAt(AddrAbsoluteY()); TotalCycles += 7; break;
            case 0x1F: SloAt(AddrAbsoluteX()); TotalCycles += 7; break;

            // RLA
            case 0x23: RlaAt(AddrIndexedIndirect()); TotalCycles += 8; break;
            case 0x27: RlaAt(AddrZeroPage()); TotalCycles += 5; break;
            case 0x2F: RlaAt(AddrAbsolute()); TotalCycles += 6; break;
            case 0x33: RlaAt(AddrIndirectIndexed()); TotalCycles += 8; break;
            case 0x37: RlaAt(AddrZeroPageX()); TotalCycles += 6; break;
            case 0x3B: RlaAt(AddrAbsoluteY()); TotalCycles += 7; break;
            case 0x3F: RlaAt(AddrAbsoluteX()); TotalCycles += 7; break;

            // SRE
            case 0x43: SreAt(AddrIndexedIndirect()); TotalCycles += 8; break;
            case 0x47: SreAt(AddrZeroPage()); TotalCycles += 5; break;
            case 0x4F: SreAt(AddrAbsolute()); TotalCycles += 6; break;
            case 0x53: SreAt(AddrIndirectIndexed()); TotalCycles += 8; break;
            case 0x57: SreAt(AddrZeroPageX()); TotalCycles += 6; break;
            case 0x5B: SreAt(AddrAbsoluteY()); TotalCycles += 7; break;
            case 0x5F: SreAt(AddrAbsoluteX()); TotalCycles += 7; break;

            // RRA
            case 0x63: RraAt(AddrIndexedIndirect()); TotalCycles += 8; break;
            case 0x67: RraAt(AddrZeroPage()); TotalCycles += 5; break;
            case 0x6F: RraAt(AddrAbsolute()); TotalCycles += 6; break;
            case 0x73: RraAt(AddrIndirectIndexed()); TotalCycles += 8; break;
            case 0x77: RraAt(AddrZeroPageX()); TotalCycles += 6; break;
            case 0x7B: RraAt(AddrAbsoluteY()); TotalCycles += 7; break;
            case 0x7F: RraAt(AddrAbsoluteX()); TotalCycles += 7; break;

            // ANC
            case 0x0B: case 0x2B: A &= Fetch(); SetZN(A); C = (A & 0x80) != 0; TotalCycles += 2; break;

            // ALR
            case 0x4B: A &= Fetch(); C = (A & 0x01) != 0; A >>= 1; SetZN(A); TotalCycles += 2; break;

            // ARR
            case 0x6B:
                {
                    byte imm = Fetch();
                    A &= imm;
                    byte r = (byte)((A >> 1) | (C ? 0x80 : 0));
                    C = (r & 0x40) != 0;
                    V = ((r ^ (r << 1)) & 0x40) != 0;
                    A = r; SetZN(A); TotalCycles += 2;
                    break;
                }

            // SBX
            case 0xCB:
                {
                    byte imm = Fetch();
                    int r = (A & X) - imm;
                    C = r >= 0; X = (byte)r; SetZN(X); TotalCycles += 2;
                    break;
                }

            // USBC
            case 0xEB: SbcCore(Fetch()); TotalCycles += 2; break;

            // LAS
            case 0xBB:
                {
                    byte v = (byte)(ReadByte(AddrAbsoluteY()) & SP);
                    A = X = SP = v; SetZN(v); TotalCycles += 4;
                    break;
                }

            // XAA
            case 0x8B: A = (byte)((A | 0xEE) & X & Fetch()); SetZN(A); TotalCycles += 2; break;

            // LXA
            case 0xAB: { byte v = (byte)((A | 0xEE) & Fetch()); A = X = v; SetZN(v); TotalCycles += 2; break; }

            // TAS
            case 0x9B:
                {
                    ushort base16 = (ushort)(Fetch() | (Fetch() << 8));
                    SP = (byte)(A & X);
                    WriteByte((ushort)(base16 + Y), (byte)(SP & ((base16 >> 8) + 1)));
                    TotalCycles += 5;
                    break;
                }

            // SHY (illegal NMOS 6502 / STZ abs on 65C02)
            case 0x9C:
                {
                    ushort base16 = (ushort)(Fetch() | (Fetch() << 8));
                    ushort ea = (ushort)(base16 + X);
                    WriteByte(ea, (byte)(Y & ((base16 >> 8) + 1)));
                    TotalCycles += 5;
                    break;
                }

            // SHX (illegal NMOS 6502 / STZ abs,X on 65C02)
            case 0x9E:
                {
                    ushort base16 = (ushort)(Fetch() | (Fetch() << 8));
                    ushort ea = (ushort)(base16 + Y);
                    WriteByte(ea, (byte)(X & ((base16 >> 8) + 1)));
                    TotalCycles += 5;
                    break;
                }

            // SHA
            case 0x9F:
                {
                    ushort base16 = (ushort)(Fetch() | (Fetch() << 8));
                    ushort ea = (ushort)(base16 + Y);
                    WriteByte(ea, (byte)(A & X & ((base16 >> 8) + 1)));
                    TotalCycles += 5;
                    break;
                }
            case 0x93:
                {
                    byte zp = Fetch();
                    ushort base16 = ReadWordBug(zp);
                    ushort ea = (ushort)(base16 + Y);
                    WriteByte(ea, (byte)(A & X & ((base16 >> 8) + 1)));
                    TotalCycles += 6;
                    break;
                }

            default:
                IllegalOpcode();
                break;
        }
    }

    private void IllegalOpcode()
    {
        byte opcode = _bus.Read((ushort)(PC - 1));
        throw new InvalidOperationException(
            $"Illegal opcode 0x{opcode:X2} at PC=0x{PC - 1:X4}");
    }
}
