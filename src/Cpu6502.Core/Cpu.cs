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

    // ── Status flags ──────────────────────────────────────────────────────────
    public bool C { get; private set; }   // Carry
    public bool Z { get; private set; }   // Zero
    public bool I { get; private set; }   // Interrupt disable
    public bool D { get; private set; }   // Decimal
    public bool V { get; private set; }   // Overflow
    public bool N { get; private set; }   // Negative

    // ── Cycle counter ─────────────────────────────────────────────────────────
    public ulong TotalCycles { get; private set; }

    // ── Hardware ─────────────────────────────────────────────────────────────
    private readonly IBus _bus;

    // ── Dispatch table ───────────────────────────────────────────────────────
    private readonly Action[] _ops = new Action[256];

    // ── Pending interrupt state ───────────────────────────────────────────────
    private bool _nmiPending;
    private bool _irqPending;

    // ── Execution tracing ────────────────────────────────────────────────────
    private IExecutionTrace _trace = NullTrace.Instance;
    private ulong _memoryAccessCount;  // Counter for sampling

    public Cpu(IBus bus)
    {
        _bus = bus;
        BuildDispatchTable();
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
        C  = false;
        Z  = false;
        D  = false;
        V  = false;
        N  = false;
        I  = true;   // Interrupts disabled after reset

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
            byte opcode = Fetch();
            
            _trace.OnInstructionFetched(pcBefore, opcode);
            
            _ops[opcode]();
            
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
    public byte GetStatus(bool breakFlag = false)
    {
        byte s = (byte)StatusFlags.Unused;
        if (C) s |= (byte)StatusFlags.Carry;
        if (Z) s |= (byte)StatusFlags.Zero;
        if (I) s |= (byte)StatusFlags.InterruptDisable;
        if (D) s |= (byte)StatusFlags.Decimal;
        if (breakFlag) s |= (byte)StatusFlags.Break;
        if (V) s |= (byte)StatusFlags.Overflow;
        if (N) s |= (byte)StatusFlags.Negative;
        return s;
    }

    private void SetStatus(byte value)
    {
        C = (value & (byte)StatusFlags.Carry)            != 0;
        Z = (value & (byte)StatusFlags.Zero)             != 0;
        I = (value & (byte)StatusFlags.InterruptDisable) != 0;
        D = (value & (byte)StatusFlags.Decimal)          != 0;
        V = (value & (byte)StatusFlags.Overflow)         != 0;
        N = (value & (byte)StatusFlags.Negative)         != 0;
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

    private void SetZN(byte v) { Z = v == 0; N = (v & CpuConstants.BIT_7_MASK) != 0; }

    // ─────────────────────────────────────────────────────────────────────────
    // Dispatch table builder
    // ─────────────────────────────────────────────────────────────────────────

    private void BuildDispatchTable()
    {
        // Fill with illegal-opcode handler
        for (int i = 0; i < 256; i++)
            _ops[i] = IllegalOpcode;

        // NOP
        _ops[0xEA] = () => { TotalCycles += 2; };

        // ── Load / Store ──────────────────────────────────────────────────────
        // Consolidated using ExecuteLoad/ExecuteStore helpers to eliminate 31 duplicate methods
        // Each addressing mode is now handled by the generic helper with a parameter
        
        // LDA (Load Accumulator) - 8 addressing modes
        _ops[0xA9] = LDA_Immediate;
        _ops[0xA5] = LDA_ZeroPage;
        _ops[0xB5] = LDA_ZeroPageX;
        _ops[0xAD] = LDA_Absolute;
        _ops[0xBD] = LDA_AbsoluteX;
        _ops[0xB9] = LDA_AbsoluteY;
        _ops[0xA1] = LDA_IndirectX;
        _ops[0xB1] = LDA_IndirectY;

        // LDX (Load X Register) - 5 addressing modes
        _ops[0xA2] = LDX_Immediate;
        _ops[0xA6] = LDX_ZeroPage;
        _ops[0xB6] = LDX_ZeroPageY;
        _ops[0xAE] = LDX_Absolute;
        _ops[0xBE] = LDX_AbsoluteY;

        // LDY (Load Y Register) - 5 addressing modes
        _ops[0xA0] = LDY_Immediate;
        _ops[0xA4] = LDY_ZeroPage;
        _ops[0xB4] = LDY_ZeroPageX;
        _ops[0xAC] = LDY_Absolute;
        _ops[0xBC] = LDY_AbsoluteX;

        // STA (Store Accumulator) - 7 addressing modes
        _ops[0x85] = STA_ZeroPage;
        _ops[0x95] = STA_ZeroPageX;
        _ops[0x8D] = STA_Absolute;
        _ops[0x9D] = STA_AbsoluteX;
        _ops[0x99] = STA_AbsoluteY;
        _ops[0x81] = STA_IndirectX;
        _ops[0x91] = STA_IndirectY;

        // STX (Store X Register) - 3 addressing modes
        _ops[0x86] = STX_ZeroPage;
        _ops[0x96] = STX_ZeroPageY;
        _ops[0x8E] = STX_Absolute;

        // STY (Store Y Register) - 3 addressing modes
        _ops[0x84] = STY_ZeroPage;
        _ops[0x94] = STY_ZeroPageX;
        _ops[0x8C] = STY_Absolute;

        // ── Register transfers ────────────────────────────────────────────────
        _ops[0xAA] = TAX; _ops[0x8A] = TXA;
        _ops[0xA8] = TAY; _ops[0x98] = TYA;
        _ops[0xBA] = TSX; _ops[0x9A] = TXS;

        // ── Stack ─────────────────────────────────────────────────────────────
        _ops[0x48] = PHA; _ops[0x68] = PLA;
        _ops[0x08] = PHP; _ops[0x28] = PLP;

        // ── Arithmetic ────────────────────────────────────────────────────────
        // Consolidated using ExecuteArithmetic helper to eliminate 16 duplicate methods
        
        // ADC (Add with Carry) - 8 addressing modes
        _ops[0x69] = ADC_Immediate;
        _ops[0x65] = ADC_ZeroPage;
        _ops[0x75] = ADC_ZeroPageX;
        _ops[0x6D] = ADC_Absolute;
        _ops[0x7D] = ADC_AbsoluteX;
        _ops[0x79] = ADC_AbsoluteY;
        _ops[0x61] = ADC_IndirectX;
        _ops[0x71] = ADC_IndirectY;

        // SBC (Subtract with Carry) - 8 addressing modes
        _ops[0xE9] = SBC_Immediate;
        _ops[0xE5] = SBC_ZeroPage;
        _ops[0xF5] = SBC_ZeroPageX;
        _ops[0xED] = SBC_Absolute;
        _ops[0xFD] = SBC_AbsoluteX;
        _ops[0xF9] = SBC_AbsoluteY;
        _ops[0xE1] = SBC_IndirectX;
        _ops[0xF1] = SBC_IndirectY;

        _ops[0xE6] = () => ExecuteRMW(v => (byte)(v + 1), AddressingMode.ZeroPage);
        _ops[0xF6] = () => ExecuteRMW(v => (byte)(v + 1), AddressingMode.ZeroPageX);
        _ops[0xEE] = () => ExecuteRMW(v => (byte)(v + 1), AddressingMode.Absolute);
        _ops[0xFE] = () => ExecuteRMW(v => (byte)(v + 1), AddressingMode.AbsoluteX);
        _ops[0xE8] = INX;      _ops[0xC8] = INY;

        _ops[0xC6] = DEC_ZeroPage;
        _ops[0xD6] = DEC_ZeroPageX;
        _ops[0xCE] = DEC_Absolute;
        _ops[0xDE] = DEC_AbsoluteX;
        _ops[0xCA] = DEX;      _ops[0x88] = DEY;

        // ── Logic ─────────────────────────────────────────────────────────────
        // Consolidated using ExecuteLogic helper to eliminate 26 duplicate methods
        
        // AND (Bitwise AND) - 8 addressing modes
        _ops[0x29] = AND_Immediate;
        _ops[0x25] = AND_ZeroPage;
        _ops[0x35] = AND_ZeroPageX;
        _ops[0x2D] = AND_Absolute;
        _ops[0x3D] = AND_AbsoluteX;
        _ops[0x39] = AND_AbsoluteY;
        _ops[0x21] = AND_IndirectX;
        _ops[0x31] = AND_IndirectY;

        // ORA (Bitwise OR) - 8 addressing modes
        _ops[0x09] = ORA_Immediate;
        _ops[0x05] = ORA_ZeroPage;
        _ops[0x15] = ORA_ZeroPageX;
        _ops[0x0D] = ORA_Absolute;
        _ops[0x1D] = ORA_AbsoluteX;
        _ops[0x19] = ORA_AbsoluteY;
        _ops[0x01] = ORA_IndirectX;
        _ops[0x11] = ORA_IndirectY;

        // EOR (Bitwise XOR) - 8 addressing modes
        _ops[0x49] = EOR_Immediate;
        _ops[0x45] = EOR_ZeroPage;
        _ops[0x55] = EOR_ZeroPageX;
        _ops[0x4D] = EOR_Absolute;
        _ops[0x5D] = EOR_AbsoluteX;
        _ops[0x59] = EOR_AbsoluteY;
        _ops[0x41] = EOR_IndirectX;
        _ops[0x51] = EOR_IndirectY;

        // BIT (Test bits) - 2 addressing modes (handled separately as it doesn't store in A)
        _ops[0x24] = BIT_ZeroPage;
        _ops[0x2C] = BIT_Absolute;

        // ── Shifts & Rotates ──────────────────────────────────────────────────
        // Consolidated using ExecuteShiftAccumulator and ExecuteShiftMemory to eliminate 20 duplicate methods
        
        // ASL (Arithmetic Shift Left) - accumulator + 4 memory addressing modes
        _ops[0x0A] = ASL_Accumulator;
        _ops[0x06] = ASL_ZeroPage;
        _ops[0x16] = ASL_ZeroPageX;
        _ops[0x0E] = ASL_Absolute;
        _ops[0x1E] = ASL_AbsoluteX;

        // LSR (Logical Shift Right) - accumulator + 4 memory addressing modes
        _ops[0x4A] = LSR_Accumulator;
        _ops[0x46] = LSR_ZeroPage;
        _ops[0x56] = LSR_ZeroPageX;
        _ops[0x4E] = LSR_Absolute;
        _ops[0x5E] = LSR_AbsoluteX;

        // ROL (Rotate Left through Carry) - accumulator + 4 memory addressing modes
        _ops[0x2A] = ROL_Accumulator;
        _ops[0x26] = ROL_ZeroPage;
        _ops[0x36] = ROL_ZeroPageX;
        _ops[0x2E] = ROL_Absolute;
        _ops[0x3E] = ROL_AbsoluteX;

        // ROR (Rotate Right through Carry) - accumulator + 4 memory addressing modes
        _ops[0x6A] = ROR_Accumulator;
        _ops[0x66] = ROR_ZeroPage;
        _ops[0x76] = ROR_ZeroPageX;
        _ops[0x6E] = ROR_Absolute;
        _ops[0x7E] = ROR_AbsoluteX;

        // ── Compare ───────────────────────────────────────────────────────────
        // Consolidated using ExecuteCompare helper to eliminate 13 duplicate methods
        
        // CMP (Compare Accumulator) - 8 addressing modes
        _ops[0xC9] = CMP_Immediate;
        _ops[0xC5] = CMP_ZeroPage;
        _ops[0xD5] = CMP_ZeroPageX;
        _ops[0xCD] = CMP_Absolute;
        _ops[0xDD] = CMP_AbsoluteX;
        _ops[0xD9] = CMP_AbsoluteY;
        _ops[0xC1] = CMP_IndirectX;
        _ops[0xD1] = CMP_IndirectY;

        // CPX (Compare X Register) - 3 addressing modes
        _ops[0xE0] = CPX_Immediate;
        _ops[0xE4] = CPX_ZeroPage;
        _ops[0xEC] = CPX_Absolute;

        // CPY (Compare Y Register) - 3 addressing modes
        _ops[0xC0] = CPY_Immediate;
        _ops[0xC4] = CPY_ZeroPage;
        _ops[0xCC] = CPY_Absolute;

        // ── Branches ──────────────────────────────────────────────────────────
        _ops[0x90] = BCC; _ops[0xB0] = BCS;
        _ops[0xF0] = BEQ; _ops[0xD0] = BNE;
        _ops[0x30] = BMI; _ops[0x10] = BPL;
        _ops[0x70] = BVS; _ops[0x50] = BVC;

        // ── Jumps / Calls ─────────────────────────────────────────────────────
        _ops[0x4C] = JMP_Abs; _ops[0x6C] = JMP_Ind;
        _ops[0x20] = JSR;     _ops[0x60] = RTS;
        _ops[0x00] = BRK;     _ops[0x40] = RTI;

        // ── Flag instructions ─────────────────────────────────────────────────
        _ops[0x18] = CLC; _ops[0x38] = SEC;
        _ops[0x58] = CLI; _ops[0x78] = SEI;
        _ops[0xD8] = CLD; _ops[0xF8] = SED;
        _ops[0xB8] = CLV;

        // ── Illegal / undocumented (NMOS 6502) ───────────────────────────────
        // NOP variants — consume operand bytes and cycles, discard result
        // Implied 1-byte 2-cycle
        _ops[0x1A] = _ops[0x3A] = _ops[0x5A] = _ops[0x7A] =
        _ops[0xDA] = _ops[0xFA] = () => { TotalCycles += 2; };
        // Immediate 2-byte 2-cycle
        _ops[0x80] = _ops[0x82] = _ops[0x89] = _ops[0xC2] = _ops[0xE2] =
            () => { Fetch(); TotalCycles += 2; };
        // Zero-page 2-byte 3-cycle
        _ops[0x04] = _ops[0x44] = _ops[0x64] =
            () => { ReadByte((ushort)Fetch()); TotalCycles += 3; };
        // Zero-page,X 2-byte 4-cycle
        _ops[0x14] = _ops[0x34] = _ops[0x54] = _ops[0x74] =
        _ops[0xD4] = _ops[0xF4] =
            () => { ReadByte((ushort)((Fetch() + X) & 0xFF)); TotalCycles += 4; };
        // Absolute 3-byte 4-cycle
        _ops[0x0C] = () => { ushort a = (ushort)(Fetch() | (Fetch() << 8)); ReadByte(a); TotalCycles += 4; };
        // Absolute,X 3-byte 4-cycle (no page-cross penalty on NOPs)
        Action nopAbsX = () =>
        {
            ushort b = (ushort)(Fetch() | (Fetch() << 8));
            ReadByte((ushort)(b + X));
            TotalCycles += 4;
        };
        _ops[0x1C] = _ops[0x3C] = _ops[0x5C] = _ops[0x7C] =
        _ops[0xDC] = _ops[0xFC] = nopAbsX;

        // LAX — LDA + LDX same value (sets Z and N)
        _ops[0xA3] = () => { byte v = ReadByte(AddrIndexedIndirect());   A = X = v; SetZN(v); TotalCycles += 6; };
        _ops[0xA7] = () => { byte v = ReadByte(AddrZeroPage());          A = X = v; SetZN(v); TotalCycles += 3; };
        _ops[0xAF] = () => { byte v = ReadByte(AddrAbsolute());          A = X = v; SetZN(v); TotalCycles += 4; };
        _ops[0xB3] = () => { byte v = ReadByte(AddrIndirectIndexed());   A = X = v; SetZN(v); TotalCycles += 5; };
        _ops[0xB7] = () => { byte v = ReadByte(AddrZeroPageY());         A = X = v; SetZN(v); TotalCycles += 4; };
        _ops[0xBF] = () => { byte v = ReadByte(AddrAbsoluteY());         A = X = v; SetZN(v); TotalCycles += 4; };

        // SAX — store A & X (no flags)
        _ops[0x83] = () => { WriteByte(AddrIndexedIndirect(), (byte)(A & X)); TotalCycles += 6; };
        _ops[0x87] = () => { WriteByte(AddrZeroPage(),        (byte)(A & X)); TotalCycles += 3; };
        _ops[0x8F] = () => { WriteByte(AddrAbsolute(),        (byte)(A & X)); TotalCycles += 4; };
        _ops[0x97] = () => { WriteByte(AddrZeroPageY(),       (byte)(A & X)); TotalCycles += 4; };

        // DCP — DEC memory then CMP A (sets C, Z, N like CMP)
        // DCP — Decrement then Compare (all addressing modes)
        _ops[0xC3] = () => ExecuteIllegalRMW(v => (byte)(v - 1), result => DoCMP(A, result), AddressingMode.IndirectX);
        _ops[0xC7] = () => ExecuteIllegalRMW(v => (byte)(v - 1), result => DoCMP(A, result), AddressingMode.ZeroPage);
        _ops[0xCF] = () => ExecuteIllegalRMW(v => (byte)(v - 1), result => DoCMP(A, result), AddressingMode.Absolute);
        _ops[0xD3] = () => ExecuteIllegalRMW(v => (byte)(v - 1), result => DoCMP(A, result), AddressingMode.IndirectY);
        _ops[0xD7] = () => ExecuteIllegalRMW(v => (byte)(v - 1), result => DoCMP(A, result), AddressingMode.ZeroPageX);
        _ops[0xDB] = () => ExecuteIllegalRMW(v => (byte)(v - 1), result => DoCMP(A, result), AddressingMode.AbsoluteY);
        _ops[0xDF] = () => ExecuteIllegalRMW(v => (byte)(v - 1), result => DoCMP(A, result), AddressingMode.AbsoluteX);

        // ISB/ISC — INC memory then SBC A
        // ISB — Increment then SBC (all addressing modes)
        _ops[0xE3] = () => ExecuteIllegalRMW(v => (byte)(v + 1), result => SbcCore(result), AddressingMode.IndirectX);
        _ops[0xE7] = () => ExecuteIllegalRMW(v => (byte)(v + 1), result => SbcCore(result), AddressingMode.ZeroPage);
        _ops[0xEF] = () => ExecuteIllegalRMW(v => (byte)(v + 1), result => SbcCore(result), AddressingMode.Absolute);
        _ops[0xF3] = () => ExecuteIllegalRMW(v => (byte)(v + 1), result => SbcCore(result), AddressingMode.IndirectY);
        _ops[0xF7] = () => ExecuteIllegalRMW(v => (byte)(v + 1), result => SbcCore(result), AddressingMode.ZeroPageX);
        _ops[0xFB] = () => ExecuteIllegalRMW(v => (byte)(v + 1), result => SbcCore(result), AddressingMode.AbsoluteY);
        _ops[0xFF] = () => ExecuteIllegalRMW(v => (byte)(v + 1), result => SbcCore(result), AddressingMode.AbsoluteX);

        // SLO — ASL memory then ORA A
        // SLO — Shift Left then ORA (all addressing modes)
        _ops[0x03] = () => ExecuteIllegalRMW(v => { C = (v & 0x80) != 0; return (byte)(v << 1); }, result => { A |= result; SetZN(A); }, AddressingMode.IndirectX);
        _ops[0x07] = () => ExecuteIllegalRMW(v => { C = (v & 0x80) != 0; return (byte)(v << 1); }, result => { A |= result; SetZN(A); }, AddressingMode.ZeroPage);
        _ops[0x0F] = () => ExecuteIllegalRMW(v => { C = (v & 0x80) != 0; return (byte)(v << 1); }, result => { A |= result; SetZN(A); }, AddressingMode.Absolute);
        _ops[0x13] = () => ExecuteIllegalRMW(v => { C = (v & 0x80) != 0; return (byte)(v << 1); }, result => { A |= result; SetZN(A); }, AddressingMode.IndirectY);
        _ops[0x17] = () => ExecuteIllegalRMW(v => { C = (v & 0x80) != 0; return (byte)(v << 1); }, result => { A |= result; SetZN(A); }, AddressingMode.ZeroPageX);
        _ops[0x1B] = () => ExecuteIllegalRMW(v => { C = (v & 0x80) != 0; return (byte)(v << 1); }, result => { A |= result; SetZN(A); }, AddressingMode.AbsoluteY);
        _ops[0x1F] = () => ExecuteIllegalRMW(v => { C = (v & 0x80) != 0; return (byte)(v << 1); }, result => { A |= result; SetZN(A); }, AddressingMode.AbsoluteX);

        // RLA — ROL memory then AND A
        // RLA — Rotate Left then AND (all addressing modes)
        _ops[0x23] = () => ExecuteIllegalRMW(v => { byte c = C ? (byte)1 : (byte)0; C = (v & 0x80) != 0; return (byte)((v << 1) | c); }, result => { A &= result; SetZN(A); }, AddressingMode.IndirectX);
        _ops[0x27] = () => ExecuteIllegalRMW(v => { byte c = C ? (byte)1 : (byte)0; C = (v & 0x80) != 0; return (byte)((v << 1) | c); }, result => { A &= result; SetZN(A); }, AddressingMode.ZeroPage);
        _ops[0x2F] = () => ExecuteIllegalRMW(v => { byte c = C ? (byte)1 : (byte)0; C = (v & 0x80) != 0; return (byte)((v << 1) | c); }, result => { A &= result; SetZN(A); }, AddressingMode.Absolute);
        _ops[0x33] = () => ExecuteIllegalRMW(v => { byte c = C ? (byte)1 : (byte)0; C = (v & 0x80) != 0; return (byte)((v << 1) | c); }, result => { A &= result; SetZN(A); }, AddressingMode.IndirectY);
        _ops[0x37] = () => ExecuteIllegalRMW(v => { byte c = C ? (byte)1 : (byte)0; C = (v & 0x80) != 0; return (byte)((v << 1) | c); }, result => { A &= result; SetZN(A); }, AddressingMode.ZeroPageX);
        _ops[0x3B] = () => ExecuteIllegalRMW(v => { byte c = C ? (byte)1 : (byte)0; C = (v & 0x80) != 0; return (byte)((v << 1) | c); }, result => { A &= result; SetZN(A); }, AddressingMode.AbsoluteY);
        _ops[0x3F] = () => ExecuteIllegalRMW(v => { byte c = C ? (byte)1 : (byte)0; C = (v & 0x80) != 0; return (byte)((v << 1) | c); }, result => { A &= result; SetZN(A); }, AddressingMode.AbsoluteX);

        // SRE — LSR memory then EOR A
        // SRE — Shift Right then EOR (all addressing modes)
        _ops[0x43] = () => ExecuteIllegalRMW(v => { C = (v & 1) != 0; return (byte)(v >> 1); }, result => { A ^= result; SetZN(A); }, AddressingMode.IndirectX);
        _ops[0x47] = () => ExecuteIllegalRMW(v => { C = (v & 1) != 0; return (byte)(v >> 1); }, result => { A ^= result; SetZN(A); }, AddressingMode.ZeroPage);
        _ops[0x4F] = () => ExecuteIllegalRMW(v => { C = (v & 1) != 0; return (byte)(v >> 1); }, result => { A ^= result; SetZN(A); }, AddressingMode.Absolute);
        _ops[0x53] = () => ExecuteIllegalRMW(v => { C = (v & 1) != 0; return (byte)(v >> 1); }, result => { A ^= result; SetZN(A); }, AddressingMode.IndirectY);
        _ops[0x57] = () => ExecuteIllegalRMW(v => { C = (v & 1) != 0; return (byte)(v >> 1); }, result => { A ^= result; SetZN(A); }, AddressingMode.ZeroPageX);
        _ops[0x5B] = () => ExecuteIllegalRMW(v => { C = (v & 1) != 0; return (byte)(v >> 1); }, result => { A ^= result; SetZN(A); }, AddressingMode.AbsoluteY);
        _ops[0x5F] = () => ExecuteIllegalRMW(v => { C = (v & 1) != 0; return (byte)(v >> 1); }, result => { A ^= result; SetZN(A); }, AddressingMode.AbsoluteX);

        // RRA — ROR memory then ADC A
        // RRA — Rotate Right then ADC (all addressing modes)
        _ops[0x63] = () => ExecuteIllegalRMW(v => { byte c = C ? (byte)0x80 : (byte)0; C = (v & 1) != 0; return (byte)((v >> 1) | c); }, result => AdcCore(result), AddressingMode.IndirectX);
        _ops[0x67] = () => ExecuteIllegalRMW(v => { byte c = C ? (byte)0x80 : (byte)0; C = (v & 1) != 0; return (byte)((v >> 1) | c); }, result => AdcCore(result), AddressingMode.ZeroPage);
        _ops[0x6F] = () => ExecuteIllegalRMW(v => { byte c = C ? (byte)0x80 : (byte)0; C = (v & 1) != 0; return (byte)((v >> 1) | c); }, result => AdcCore(result), AddressingMode.Absolute);
        _ops[0x73] = () => ExecuteIllegalRMW(v => { byte c = C ? (byte)0x80 : (byte)0; C = (v & 1) != 0; return (byte)((v >> 1) | c); }, result => AdcCore(result), AddressingMode.IndirectY);
        _ops[0x77] = () => ExecuteIllegalRMW(v => { byte c = C ? (byte)0x80 : (byte)0; C = (v & 1) != 0; return (byte)((v >> 1) | c); }, result => AdcCore(result), AddressingMode.ZeroPageX);
        _ops[0x7B] = () => ExecuteIllegalRMW(v => { byte c = C ? (byte)0x80 : (byte)0; C = (v & 1) != 0; return (byte)((v >> 1) | c); }, result => AdcCore(result), AddressingMode.AbsoluteY);
        _ops[0x7F] = () => ExecuteIllegalRMW(v => { byte c = C ? (byte)0x80 : (byte)0; C = (v & 1) != 0; return (byte)((v >> 1) | c); }, result => AdcCore(result), AddressingMode.AbsoluteX);

        // ANC ($0B/$2B) — AND imm, carry = bit 7 of result
        _ops[0x0B] = _ops[0x2B] = () =>
        {
            A &= Fetch(); SetZN(A); C = (A & 0x80) != 0; TotalCycles += 2;
        };

        // ALR ($4B) — AND imm then LSR accumulator
        _ops[0x4B] = () =>
        {
            A &= Fetch(); C = (A & 0x01) != 0; A >>= 1; SetZN(A); TotalCycles += 2;
        };

        // ARR ($6B) — AND imm then ROR accumulator (complex flags)
        _ops[0x6B] = () =>
        {
            byte imm = Fetch();
            A &= imm;
            byte r = (byte)((A >> 1) | (C ? 0x80 : 0));
            C = (r & 0x40) != 0;
            V = ((r ^ (r << 1)) & 0x40) != 0;
            A = r; SetZN(A); TotalCycles += 2;
        };

        // SBX/AXS ($CB) — (A & X) - imm → X; sets C,Z,N like CMP
        _ops[0xCB] = () =>
        {
            byte imm = Fetch();
            int r = (A & X) - imm;
            C = r >= 0; X = (byte)r; SetZN(X); TotalCycles += 2;
        };

        // USBC ($EB) — SBC immediate (duplicate of $E9)
        _ops[0xEB] = () => { SbcCore(Fetch()); TotalCycles += 2; };

        // LAS/LAR ($BB) — (SP & mem) → A, X, SP; abs,Y
        _ops[0xBB] = () =>
        {
            byte v = (byte)(ReadByte(AddrAbsoluteY()) & SP);
            A = X = SP = v; SetZN(v); TotalCycles += 4;
        };

        // XAA/ANE ($8B) — unstable; treat as A = (A | 0xEE) & X & imm
        _ops[0x8B] = () => { A = (byte)((A | 0xEE) & X & Fetch()); SetZN(A); TotalCycles += 2; };

        // LXA/OAL ($AB) — A = X = (A | 0xEE) & imm (unstable; common magic = 0xFF)
        _ops[0xAB] = () => { byte v = (byte)((A | 0xEE) & Fetch()); A = X = v; SetZN(v); TotalCycles += 2; };

        // TAS/XAS ($9B) — SP = A & X; store A & X & (addr_hi+1) abs,Y
        _ops[0x9B] = () =>
        {
            ushort base16 = (ushort)(Fetch() | (Fetch() << 8));
            SP = (byte)(A & X);
            WriteByte((ushort)(base16 + Y), (byte)(SP & ((base16 >> 8) + 1)));
            TotalCycles += 5;
        };

        // SHY ($9C) — Store Y & (addr_hi + 1), abs,X
        _ops[0x9C] = () =>
        {
            ushort base16 = (ushort)(Fetch() | (Fetch() << 8));
            ushort ea     = (ushort)(base16 + X);
            WriteByte(ea, (byte)(Y & ((base16 >> 8) + 1)));
            TotalCycles += 5;
        };

        // SHX ($9E) — Store X & (addr_hi + 1), abs,Y
        _ops[0x9E] = () =>
        {
            ushort base16 = (ushort)(Fetch() | (Fetch() << 8));
            ushort ea     = (ushort)(base16 + Y);
            WriteByte(ea, (byte)(X & ((base16 >> 8) + 1)));
            TotalCycles += 5;
        };

        // SHA ($9F abs,Y; $93 (ind),Y) — Store A & X & (addr_hi + 1)
        _ops[0x9F] = () =>
        {
            ushort base16 = (ushort)(Fetch() | (Fetch() << 8));
            ushort ea     = (ushort)(base16 + Y);
            WriteByte(ea, (byte)(A & X & ((base16 >> 8) + 1)));
            TotalCycles += 5;
        };
        _ops[0x93] = () =>
        {
            byte zp       = Fetch();
            ushort base16 = ReadWordBug(zp);
            ushort ea     = (ushort)(base16 + Y);
            WriteByte(ea, (byte)(A & X & ((base16 >> 8) + 1)));
            TotalCycles += 6;
        };
    }

    private void IllegalOpcode()
    {
        byte opcode = _bus.Read((ushort)(PC - 1));
        throw new InvalidOperationException(
            $"Illegal opcode 0x{opcode:X2} at PC=0x{PC - 1:X4}");
    }
}
