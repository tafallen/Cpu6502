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
    /// </summary>
    public IExecutionTrace Trace
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

    private static bool PageCrossed(ushort a, ushort b) => (a & CpuConstants.PAGE_MASK) != (b & CpuConstants.PAGE_MASK);

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
        _ops[0xA9] = () => A = ExecuteLoad(AddressingMode.Immediate);
        _ops[0xA5] = () => A = ExecuteLoad(AddressingMode.ZeroPage);
        _ops[0xB5] = () => A = ExecuteLoad(AddressingMode.ZeroPageX);
        _ops[0xAD] = () => A = ExecuteLoad(AddressingMode.Absolute);
        _ops[0xBD] = () => A = ExecuteLoad(AddressingMode.AbsoluteX);
        _ops[0xB9] = () => A = ExecuteLoad(AddressingMode.AbsoluteY);
        _ops[0xA1] = () => A = ExecuteLoad(AddressingMode.IndirectX);
        _ops[0xB1] = () => A = ExecuteLoad(AddressingMode.IndirectY);

        // LDX (Load X Register) - 5 addressing modes
        _ops[0xA2] = () => X = ExecuteLoad(AddressingMode.Immediate);
        _ops[0xA6] = () => X = ExecuteLoad(AddressingMode.ZeroPage);
        _ops[0xB6] = () => X = ExecuteLoad(AddressingMode.ZeroPageY);
        _ops[0xAE] = () => X = ExecuteLoad(AddressingMode.Absolute);
        _ops[0xBE] = () => X = ExecuteLoad(AddressingMode.AbsoluteY);

        // LDY (Load Y Register) - 5 addressing modes
        _ops[0xA0] = () => Y = ExecuteLoad(AddressingMode.Immediate);
        _ops[0xA4] = () => Y = ExecuteLoad(AddressingMode.ZeroPage);
        _ops[0xB4] = () => Y = ExecuteLoad(AddressingMode.ZeroPageX);
        _ops[0xAC] = () => Y = ExecuteLoad(AddressingMode.Absolute);
        _ops[0xBC] = () => Y = ExecuteLoad(AddressingMode.AbsoluteX);

        // STA (Store Accumulator) - 7 addressing modes
        _ops[0x85] = () => ExecuteStore(A, AddressingMode.ZeroPage);
        _ops[0x95] = () => ExecuteStore(A, AddressingMode.ZeroPageX);
        _ops[0x8D] = () => ExecuteStore(A, AddressingMode.Absolute);
        _ops[0x9D] = () => ExecuteStore(A, AddressingMode.AbsoluteX);
        _ops[0x99] = () => ExecuteStore(A, AddressingMode.AbsoluteY);
        _ops[0x81] = () => ExecuteStore(A, AddressingMode.IndirectX);
        _ops[0x91] = () => ExecuteStore(A, AddressingMode.IndirectY);

        // STX (Store X Register) - 3 addressing modes
        _ops[0x86] = () => ExecuteStore(X, AddressingMode.ZeroPage);
        _ops[0x96] = () => ExecuteStore(X, AddressingMode.ZeroPageY);
        _ops[0x8E] = () => ExecuteStore(X, AddressingMode.Absolute);

        // STY (Store Y Register) - 3 addressing modes
        _ops[0x84] = () => ExecuteStore(Y, AddressingMode.ZeroPage);
        _ops[0x94] = () => ExecuteStore(Y, AddressingMode.ZeroPageX);
        _ops[0x8C] = () => ExecuteStore(Y, AddressingMode.Absolute);

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
        _ops[0x69] = () => ExecuteArithmetic(Cpu.ArithmeticOp.ADC, AddressingMode.Immediate);
        _ops[0x65] = () => ExecuteArithmetic(Cpu.ArithmeticOp.ADC, AddressingMode.ZeroPage);
        _ops[0x75] = () => ExecuteArithmetic(Cpu.ArithmeticOp.ADC, AddressingMode.ZeroPageX);
        _ops[0x6D] = () => ExecuteArithmetic(Cpu.ArithmeticOp.ADC, AddressingMode.Absolute);
        _ops[0x7D] = () => ExecuteArithmetic(Cpu.ArithmeticOp.ADC, AddressingMode.AbsoluteX);
        _ops[0x79] = () => ExecuteArithmetic(Cpu.ArithmeticOp.ADC, AddressingMode.AbsoluteY);
        _ops[0x61] = () => ExecuteArithmetic(Cpu.ArithmeticOp.ADC, AddressingMode.IndirectX);
        _ops[0x71] = () => ExecuteArithmetic(Cpu.ArithmeticOp.ADC, AddressingMode.IndirectY);

        // SBC (Subtract with Carry) - 8 addressing modes
        _ops[0xE9] = () => ExecuteArithmetic(Cpu.ArithmeticOp.SBC, AddressingMode.Immediate);
        _ops[0xE5] = () => ExecuteArithmetic(Cpu.ArithmeticOp.SBC, AddressingMode.ZeroPage);
        _ops[0xF5] = () => ExecuteArithmetic(Cpu.ArithmeticOp.SBC, AddressingMode.ZeroPageX);
        _ops[0xED] = () => ExecuteArithmetic(Cpu.ArithmeticOp.SBC, AddressingMode.Absolute);
        _ops[0xFD] = () => ExecuteArithmetic(Cpu.ArithmeticOp.SBC, AddressingMode.AbsoluteX);
        _ops[0xF9] = () => ExecuteArithmetic(Cpu.ArithmeticOp.SBC, AddressingMode.AbsoluteY);
        _ops[0xE1] = () => ExecuteArithmetic(Cpu.ArithmeticOp.SBC, AddressingMode.IndirectX);
        _ops[0xF1] = () => ExecuteArithmetic(Cpu.ArithmeticOp.SBC, AddressingMode.IndirectY);

        _ops[0xE6] = INC_Zp;   _ops[0xF6] = INC_ZpX;
        _ops[0xEE] = INC_Abs;  _ops[0xFE] = INC_AbsX;
        _ops[0xE8] = INX;      _ops[0xC8] = INY;

        _ops[0xC6] = DEC_Zp;   _ops[0xD6] = DEC_ZpX;
        _ops[0xCE] = DEC_Abs;  _ops[0xDE] = DEC_AbsX;
        _ops[0xCA] = DEX;      _ops[0x88] = DEY;

        // ── Logic ─────────────────────────────────────────────────────────────
        // Consolidated using ExecuteLogic helper to eliminate 26 duplicate methods
        
        // AND (Bitwise AND) - 8 addressing modes
        _ops[0x29] = () => A = ExecuteLogic(Cpu.LogicOp.AND, AddressingMode.Immediate);
        _ops[0x25] = () => A = ExecuteLogic(Cpu.LogicOp.AND, AddressingMode.ZeroPage);
        _ops[0x35] = () => A = ExecuteLogic(Cpu.LogicOp.AND, AddressingMode.ZeroPageX);
        _ops[0x2D] = () => A = ExecuteLogic(Cpu.LogicOp.AND, AddressingMode.Absolute);
        _ops[0x3D] = () => A = ExecuteLogic(Cpu.LogicOp.AND, AddressingMode.AbsoluteX);
        _ops[0x39] = () => A = ExecuteLogic(Cpu.LogicOp.AND, AddressingMode.AbsoluteY);
        _ops[0x21] = () => A = ExecuteLogic(Cpu.LogicOp.AND, AddressingMode.IndirectX);
        _ops[0x31] = () => A = ExecuteLogic(Cpu.LogicOp.AND, AddressingMode.IndirectY);

        // ORA (Bitwise OR) - 8 addressing modes
        _ops[0x09] = () => A = ExecuteLogic(Cpu.LogicOp.ORA, AddressingMode.Immediate);
        _ops[0x05] = () => A = ExecuteLogic(Cpu.LogicOp.ORA, AddressingMode.ZeroPage);
        _ops[0x15] = () => A = ExecuteLogic(Cpu.LogicOp.ORA, AddressingMode.ZeroPageX);
        _ops[0x0D] = () => A = ExecuteLogic(Cpu.LogicOp.ORA, AddressingMode.Absolute);
        _ops[0x1D] = () => A = ExecuteLogic(Cpu.LogicOp.ORA, AddressingMode.AbsoluteX);
        _ops[0x19] = () => A = ExecuteLogic(Cpu.LogicOp.ORA, AddressingMode.AbsoluteY);
        _ops[0x01] = () => A = ExecuteLogic(Cpu.LogicOp.ORA, AddressingMode.IndirectX);
        _ops[0x11] = () => A = ExecuteLogic(Cpu.LogicOp.ORA, AddressingMode.IndirectY);

        // EOR (Bitwise XOR) - 8 addressing modes
        _ops[0x49] = () => A = ExecuteLogic(Cpu.LogicOp.EOR, AddressingMode.Immediate);
        _ops[0x45] = () => A = ExecuteLogic(Cpu.LogicOp.EOR, AddressingMode.ZeroPage);
        _ops[0x55] = () => A = ExecuteLogic(Cpu.LogicOp.EOR, AddressingMode.ZeroPageX);
        _ops[0x4D] = () => A = ExecuteLogic(Cpu.LogicOp.EOR, AddressingMode.Absolute);
        _ops[0x5D] = () => A = ExecuteLogic(Cpu.LogicOp.EOR, AddressingMode.AbsoluteX);
        _ops[0x59] = () => A = ExecuteLogic(Cpu.LogicOp.EOR, AddressingMode.AbsoluteY);
        _ops[0x41] = () => A = ExecuteLogic(Cpu.LogicOp.EOR, AddressingMode.IndirectX);
        _ops[0x51] = () => A = ExecuteLogic(Cpu.LogicOp.EOR, AddressingMode.IndirectY);

        // BIT (Test bits) - 2 addressing modes (handled separately as it doesn't store in A)
        _ops[0x24] = () => ExecuteBit(AddressingMode.ZeroPage);
        _ops[0x2C] = () => ExecuteBit(AddressingMode.Absolute);

        // ── Shifts & Rotates ──────────────────────────────────────────────────
        // Consolidated using ExecuteShiftAccumulator and ExecuteShiftMemory to eliminate 20 duplicate methods
        
        // ASL (Arithmetic Shift Left) - accumulator + 4 memory addressing modes
        _ops[0x0A] = () => ExecuteShiftAccumulator(Cpu.ShiftOp.ASL);
        _ops[0x06] = () => ExecuteShiftMemory(Cpu.ShiftOp.ASL, AddressingMode.ZeroPage);
        _ops[0x16] = () => ExecuteShiftMemory(Cpu.ShiftOp.ASL, AddressingMode.ZeroPageX);
        _ops[0x0E] = () => ExecuteShiftMemory(Cpu.ShiftOp.ASL, AddressingMode.Absolute);
        _ops[0x1E] = () => ExecuteShiftMemory(Cpu.ShiftOp.ASL, AddressingMode.AbsoluteX);

        // LSR (Logical Shift Right) - accumulator + 4 memory addressing modes
        _ops[0x4A] = () => ExecuteShiftAccumulator(Cpu.ShiftOp.LSR);
        _ops[0x46] = () => ExecuteShiftMemory(Cpu.ShiftOp.LSR, AddressingMode.ZeroPage);
        _ops[0x56] = () => ExecuteShiftMemory(Cpu.ShiftOp.LSR, AddressingMode.ZeroPageX);
        _ops[0x4E] = () => ExecuteShiftMemory(Cpu.ShiftOp.LSR, AddressingMode.Absolute);
        _ops[0x5E] = () => ExecuteShiftMemory(Cpu.ShiftOp.LSR, AddressingMode.AbsoluteX);

        // ROL (Rotate Left through Carry) - accumulator + 4 memory addressing modes
        _ops[0x2A] = () => ExecuteShiftAccumulator(Cpu.ShiftOp.ROL);
        _ops[0x26] = () => ExecuteShiftMemory(Cpu.ShiftOp.ROL, AddressingMode.ZeroPage);
        _ops[0x36] = () => ExecuteShiftMemory(Cpu.ShiftOp.ROL, AddressingMode.ZeroPageX);
        _ops[0x2E] = () => ExecuteShiftMemory(Cpu.ShiftOp.ROL, AddressingMode.Absolute);
        _ops[0x3E] = () => ExecuteShiftMemory(Cpu.ShiftOp.ROL, AddressingMode.AbsoluteX);

        // ROR (Rotate Right through Carry) - accumulator + 4 memory addressing modes
        _ops[0x6A] = () => ExecuteShiftAccumulator(Cpu.ShiftOp.ROR);
        _ops[0x66] = () => ExecuteShiftMemory(Cpu.ShiftOp.ROR, AddressingMode.ZeroPage);
        _ops[0x76] = () => ExecuteShiftMemory(Cpu.ShiftOp.ROR, AddressingMode.ZeroPageX);
        _ops[0x6E] = () => ExecuteShiftMemory(Cpu.ShiftOp.ROR, AddressingMode.Absolute);
        _ops[0x7E] = () => ExecuteShiftMemory(Cpu.ShiftOp.ROR, AddressingMode.AbsoluteX);

        // ── Compare ───────────────────────────────────────────────────────────
        _ops[0xC9] = CMP_Imm;  _ops[0xC5] = CMP_Zp;   _ops[0xD5] = CMP_ZpX;
        _ops[0xCD] = CMP_Abs;  _ops[0xDD] = CMP_AbsX; _ops[0xD9] = CMP_AbsY;
        _ops[0xC1] = CMP_IndX; _ops[0xD1] = CMP_IndY;

        _ops[0xE0] = CPX_Imm;  _ops[0xE4] = CPX_Zp;   _ops[0xEC] = CPX_Abs;
        _ops[0xC0] = CPY_Imm;  _ops[0xC4] = CPY_Zp;   _ops[0xCC] = CPY_Abs;

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
        _ops[0xC3] = () => { DcpAt(AddrIndexedIndirect());  TotalCycles += 8; };
        _ops[0xC7] = () => { DcpAt(AddrZeroPage());         TotalCycles += 5; };
        _ops[0xCF] = () => { DcpAt(AddrAbsolute());         TotalCycles += 6; };
        _ops[0xD3] = () => { DcpAt(AddrIndirectIndexed(true)); TotalCycles += 8; };
        _ops[0xD7] = () => { DcpAt(AddrZeroPageX());        TotalCycles += 6; };
        _ops[0xDB] = () => { DcpAt(AddrAbsoluteY(true));    TotalCycles += 7; };
        _ops[0xDF] = () => { DcpAt(AddrAbsoluteX(true));    TotalCycles += 7; };

        // ISB/ISC — INC memory then SBC A
        _ops[0xE3] = () => { IsbAt(AddrIndexedIndirect());  TotalCycles += 8; };
        _ops[0xE7] = () => { IsbAt(AddrZeroPage());         TotalCycles += 5; };
        _ops[0xEF] = () => { IsbAt(AddrAbsolute());         TotalCycles += 6; };
        _ops[0xF3] = () => { IsbAt(AddrIndirectIndexed(true)); TotalCycles += 8; };
        _ops[0xF7] = () => { IsbAt(AddrZeroPageX());        TotalCycles += 6; };
        _ops[0xFB] = () => { IsbAt(AddrAbsoluteY(true));    TotalCycles += 7; };
        _ops[0xFF] = () => { IsbAt(AddrAbsoluteX(true));    TotalCycles += 7; };

        // SLO — ASL memory then ORA A
        _ops[0x03] = () => { SloAt(AddrIndexedIndirect());  TotalCycles += 8; };
        _ops[0x07] = () => { SloAt(AddrZeroPage());         TotalCycles += 5; };
        _ops[0x0F] = () => { SloAt(AddrAbsolute());         TotalCycles += 6; };
        _ops[0x13] = () => { SloAt(AddrIndirectIndexed(true)); TotalCycles += 8; };
        _ops[0x17] = () => { SloAt(AddrZeroPageX());        TotalCycles += 6; };
        _ops[0x1B] = () => { SloAt(AddrAbsoluteY(true));    TotalCycles += 7; };
        _ops[0x1F] = () => { SloAt(AddrAbsoluteX(true));    TotalCycles += 7; };

        // RLA — ROL memory then AND A
        _ops[0x23] = () => { RlaAt(AddrIndexedIndirect());  TotalCycles += 8; };
        _ops[0x27] = () => { RlaAt(AddrZeroPage());         TotalCycles += 5; };
        _ops[0x2F] = () => { RlaAt(AddrAbsolute());         TotalCycles += 6; };
        _ops[0x33] = () => { RlaAt(AddrIndirectIndexed(true)); TotalCycles += 8; };
        _ops[0x37] = () => { RlaAt(AddrZeroPageX());        TotalCycles += 6; };
        _ops[0x3B] = () => { RlaAt(AddrAbsoluteY(true));    TotalCycles += 7; };
        _ops[0x3F] = () => { RlaAt(AddrAbsoluteX(true));    TotalCycles += 7; };

        // SRE — LSR memory then EOR A
        _ops[0x43] = () => { SreAt(AddrIndexedIndirect());  TotalCycles += 8; };
        _ops[0x47] = () => { SreAt(AddrZeroPage());         TotalCycles += 5; };
        _ops[0x4F] = () => { SreAt(AddrAbsolute());         TotalCycles += 6; };
        _ops[0x53] = () => { SreAt(AddrIndirectIndexed(true)); TotalCycles += 8; };
        _ops[0x57] = () => { SreAt(AddrZeroPageX());        TotalCycles += 6; };
        _ops[0x5B] = () => { SreAt(AddrAbsoluteY(true));    TotalCycles += 7; };
        _ops[0x5F] = () => { SreAt(AddrAbsoluteX(true));    TotalCycles += 7; };

        // RRA — ROR memory then ADC A
        _ops[0x63] = () => { RraAt(AddrIndexedIndirect());  TotalCycles += 8; };
        _ops[0x67] = () => { RraAt(AddrZeroPage());         TotalCycles += 5; };
        _ops[0x6F] = () => { RraAt(AddrAbsolute());         TotalCycles += 6; };
        _ops[0x73] = () => { RraAt(AddrIndirectIndexed(true)); TotalCycles += 8; };
        _ops[0x77] = () => { RraAt(AddrZeroPageX());        TotalCycles += 6; };
        _ops[0x7B] = () => { RraAt(AddrAbsoluteY(true));    TotalCycles += 7; };
        _ops[0x7F] = () => { RraAt(AddrAbsoluteX(true));    TotalCycles += 7; };

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
