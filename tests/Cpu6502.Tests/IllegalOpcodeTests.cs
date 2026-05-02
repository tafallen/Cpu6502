namespace Cpu6502.Tests;

public class IllegalOpcodeTests : CpuFixture
{
    [Fact]
    public void USBC_Matches_SBC_ForRepresentativeStates()
    {
        byte[] samples = [0x00, 0x01, 0x09, 0x10, 0x50, 0x80, 0x99, 0xFF];

        foreach (byte a in samples)
        {
            foreach (byte operand in samples)
            {
                foreach (bool carry in new[] { false, true })
                {
                    foreach (bool decimalMode in new[] { false, true })
                    {
                        foreach (bool overflowSet in new[] { false, true })
                        {
                            var sbc = RunSubtract(opcode: 0xE9, a, operand, carry, decimalMode, overflowSet);
                            var usbc = RunSubtract(opcode: 0xEB, a, operand, carry, decimalMode, overflowSet);

                            Assert.Equal(sbc.A, usbc.A);
                            Assert.Equal(sbc.C, usbc.C);
                            Assert.Equal(sbc.Z, usbc.Z);
                            Assert.Equal(sbc.N, usbc.N);
                            Assert.Equal(sbc.V, usbc.V);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void SHA_IndirectY_UsesZeroPageWrapForPointerFetch()
    {
        // Arrange pointer at $FF/$00 so high-byte must wrap in zero page.
        // Correct base = $1234, incorrect non-wrapped base = $5634.
        Ram.Write(0x00FF, 0x34);
        Ram.Write(0x0000, 0x12);
        Ram.Write(0x0100, 0x56);

        Load(0x0200,
            0xA2, 0x1F,       // LDX #$1F
            0xA9, 0x3F,       // LDA #$3F  => A & X = $1F
            0xA0, 0x01,       // LDY #$01
            0x93, 0xFF);      // SHA ($FF),Y

        Step(4);

        // Stored value is A & X & (addr_hi + 1).
        // With wrapped base hi=$12 => mask $13 => $1F & $13 = $13.
        Assert.Equal(0x13, Ram.Read(0x1235));

        // Non-wrapped path would write to $5635.
        Assert.Equal(0x00, Ram.Read(0x5635));
    }

    [Fact]
    public void SHY_AbsoluteX_StoresYMaskedByBaseHighPlusOne()
    {
        Load(0x0200,
            0xA2, 0x01,       // LDX #$01
            0xA0, 0xFF,       // LDY #$FF
            0x9C, 0xFF, 0x12  // SHY $12FF,X -> $1300, mask uses base hi ($12)+1 = $13
        );

        Step(3);
        Assert.Equal(0x13, Ram.Read(0x1300));
    }

    [Fact]
    public void SHX_AbsoluteY_StoresXMaskedByBaseHighPlusOne()
    {
        Load(0x0200,
            0xA0, 0x01,       // LDY #$01
            0xA2, 0xFF,       // LDX #$FF
            0x9E, 0xFF, 0x12  // SHX $12FF,Y -> $1300, mask uses base hi ($12)+1 = $13
        );

        Step(3);
        Assert.Equal(0x13, Ram.Read(0x1300));
    }

    [Fact]
    public void SHA_AbsoluteY_StoresAAndXMaskedByBaseHighPlusOne()
    {
        Load(0x0200,
            0xA0, 0x01,       // LDY #$01
            0xA2, 0x1F,       // LDX #$1F
            0xA9, 0x3F,       // LDA #$3F
            0x9F, 0xFF, 0x12  // SHA $12FF,Y -> $1300, value ($3F & $1F & $13) = $13
        );

        Step(4);
        Assert.Equal(0x13, Ram.Read(0x1300));
    }

    [Fact]
    public void TAS_AbsoluteY_SetsSpAndStoresMaskedValue()
    {
        Load(0x0200,
            0xA0, 0x01,       // LDY #$01
            0xA2, 0x1F,       // LDX #$1F
            0xA9, 0x3F,       // LDA #$3F -> A&X = $1F
            0x9B, 0xFF, 0x12  // TAS $12FF,Y -> $1300, stored ($1F & $13) = $13
        );

        Step(4);
        Assert.Equal(0x1F, Cpu.SP);
        Assert.Equal(0x13, Ram.Read(0x1300));
    }

    private (byte A, bool C, bool Z, bool N, bool V) RunSubtract(
        byte opcode, byte a, byte operand, bool carry, bool decimalMode, bool overflowSet)
    {
        Ram.Write(0x0010, 0x40); // BIT source to set V when needed.

        Load(0x0200,
            decimalMode ? (byte)0xF8 : (byte)0xD8, // SED / CLD
            carry ? (byte)0x38 : (byte)0x18,       // SEC / CLC
            0xA9, a,                               // LDA #a
            overflowSet ? (byte)0x24 : (byte)0xB8, // BIT $10 / CLV
            overflowSet ? (byte)0x10 : (byte)0xEA, // zp operand / NOP pad
            opcode, operand);                       // SBC/USBC #imm

        Step(5);
        return (Cpu.A, Cpu.C, Cpu.Z, Cpu.N, Cpu.V);
    }
}

