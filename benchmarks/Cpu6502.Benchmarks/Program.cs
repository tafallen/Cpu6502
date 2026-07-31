using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Cpu6502.Core;
using Machines.Atom;
using Machines.Common;
using Machines.Vic20;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Cpu6502.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        var config = ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(Job.ShortRun.WithToolchain(InProcessEmitToolchain.Instance));

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
    }
}

[MemoryDiagnoser]
public class CpuInstructionBenchmarks
{
    private Cpu _cpu = null!;
    private Ram _ram = null!;
    private AddressDecoder _bus = null!;

    [GlobalSetup]
    public void Setup()
    {
        _ram = new Ram(0x10000);
        _bus = new AddressDecoder();
        _bus.Map(0x0000, 0xFFFF, _ram);
        _cpu = new Cpu(_bus);

        // Fill memory at 0x0200 with NOPs (0xEA)
        for (int i = 0x0200; i < 0xFA00; i++)
            _ram.Write((ushort)i, 0xEA);

        // RESET vector -> 0x0200
        _ram.Write(0xFFFC, 0x00);
        _ram.Write(0xFFFD, 0x02);
    }

    [Benchmark(OperationsPerInvoke = 100_000)]
    public void Step_NOP_100k()
    {
        _cpu.Reset();
        for (int i = 0; i < 100_000; i++)
        {
            _cpu.Step();
        }
    }

    [Benchmark(OperationsPerInvoke = 100_000)]
    public void Step_ADC_Immediate_100k()
    {
        // Program at 0x0200: ADC #$01 (0x69 0x01) repeating
        for (int i = 0x0200; i < 0xFA00; i += 2)
        {
            _ram.Write((ushort)i, 0x69);
            _ram.Write((ushort)(i + 1), 0x01);
        }

        _cpu.Reset();
        for (int i = 0; i < 100_000; i++)
        {
            _cpu.Step();
        }
    }

    [Benchmark(OperationsPerInvoke = 100_000)]
    public void Step_RMW_INC_ZeroPage_100k()
    {
        // Program at 0x0200: INC $10 (0xE6 0x10) repeating
        for (int i = 0x0200; i < 0xFA00; i += 2)
        {
            _ram.Write((ushort)i, 0xE6);
            _ram.Write((ushort)(i + 1), 0x10);
        }

        _cpu.Reset();
        for (int i = 0; i < 100_000; i++)
        {
            _cpu.Step();
        }
    }

    [Benchmark(OperationsPerInvoke = 100_000)]
    public void Step_Mix_LoadStoreBranch_100k()
    {
        // Program at 0x0200:
        // LDA #$05 (0xA9 0x05)
        // STA $10   (0x85 0x10)
        // DEX       (0xCA)
        // BNE -5    (0xD0 0xFB)
        ushort pc = 0x0200;
        while (pc < 0xF000)
        {
            _ram.Write(pc++, 0xA9); _ram.Write(pc++, 0x05);
            _ram.Write(pc++, 0x85); _ram.Write(pc++, 0x10);
            _ram.Write(pc++, 0xCA);
            _ram.Write(pc++, 0xD0); _ram.Write(pc++, 0xFB);
        }

        _cpu.Reset();
        for (int i = 0; i < 100_000; i++)
        {
            _cpu.Step();
        }
    }
}

[MemoryDiagnoser]
public class AddressDecoderBenchmarks
{
    private AddressDecoder _bus = null!;
    private Ram _ram = null!;

    [GlobalSetup]
    public void Setup()
    {
        _ram = new Ram(0x10000);
        _bus = new AddressDecoder();
        _bus.Map(0x0000, 0xFFFF, _ram);
    }

    [Benchmark(OperationsPerInvoke = 1_000_000)]
    public byte Read_RAM_1M()
    {
        byte sum = 0;
        for (ushort addr = 0; addr < 10_000; addr++)
        {
            for (int i = 0; i < 100; i++)
            {
                sum ^= _bus.Read(addr);
            }
        }
        return sum;
    }

    [Benchmark(OperationsPerInvoke = 1_000_000)]
    public void Write_RAM_1M()
    {
        for (ushort addr = 0; addr < 10_000; addr++)
        {
            for (int i = 0; i < 100; i++)
            {
                _bus.Write(addr, (byte)i);
            }
        }
    }
}

[MemoryDiagnoser]
public class VideoRenderBenchmarks
{
    private Mc6847 _vdg = null!;
    private VicI _vic = null!;
    private Machines.Oric.OricUlaVideo _oricVideo = null!;
    private Machines.Pet.PetVideo _petVideo = null!;
    private Machines.BbcMicro.Saa5050 _saa5050 = null!;
    private Ram _oricRam = null!;
    private Ram _petVram = null!;
    private DummyVideoSink _sink = null!;

    private class DummyVideoSink : IVideoSink
    {
        public void SubmitFrame(ReadOnlySpan<uint> pixels, int width, int height) { }
    }

    [GlobalSetup]
    public void Setup()
    {
        var vram = new byte[0x2000];
        Array.Fill(vram, (byte)0x41); // 'A'
        _vdg = new Mc6847(vram);
        _vic = new VicI();
        _sink = new DummyVideoSink();

        _oricRam = new Ram(0x10000);
        Array.Fill(_oricRam.DirectWriteBuffer, (byte)0x41);
        _oricVideo = new Machines.Oric.OricUlaVideo();

        _petVram = new Ram(0x0800);
        Array.Fill(_petVram.DirectWriteBuffer, (byte)0x41);
        _petVideo = new Machines.Pet.PetVideo();
        _saa5050 = new Machines.BbcMicro.Saa5050();
    }

    [Benchmark(OperationsPerInvoke = 100)]
    public void Mc6847_RenderFrame_100()
    {
        for (int i = 0; i < 100; i++)
        {
            _vdg.RenderFrame(_sink);
        }
    }

    [Benchmark(OperationsPerInvoke = 100)]
    public void VicI_RenderFrame_100()
    {
        for (int i = 0; i < 100; i++)
        {
            _vic.RenderFrame(_sink);
        }
    }

    [Benchmark(OperationsPerInvoke = 100)]
    public void OricUla_RenderFrame_100()
    {
        for (int i = 0; i < 100; i++)
        {
            _oricVideo.RenderFrame(_oricRam, _sink);
        }
    }

    [Benchmark(OperationsPerInvoke = 100)]
    public void PetVideo_RenderFrame_100()
    {
        for (int i = 0; i < 100; i++)
        {
            _petVideo.RenderFrame(_petVram, _sink);
        }
    }

    [Benchmark(OperationsPerInvoke = 100)]
    public void Saa5050_RenderMode7_100()
    {
        for (int i = 0; i < 100; i++)
        {
            _saa5050.RenderMode7(_oricRam, 0x7C00, _sink);
        }
    }
}

[MemoryDiagnoser]
public class PixelConversionBenchmarks
{
    private uint[] _src = null!;
    private uint[] _dst = null!;

    [GlobalSetup]
    public void Setup()
    {
        _src = new uint[256 * 192];
        _dst = new uint[256 * 192];
        for (int i = 0; i < _src.Length; i++)
            _src[i] = 0xFF123456u;
    }

    [Benchmark(OperationsPerInvoke = 100)]
    public void Convert_ARGB_to_RGBA_SIMD_100()
    {
        for (int iter = 0; iter < 100; iter++)
        {
            int count = _src.Length;
            int i = 0;

            if (Vector256.IsHardwareAccelerated && count >= Vector256<uint>.Count)
            {
                var maskAG = Vector256.Create(0xFF00FF00u);
                var maskR  = Vector256.Create(0x00FF0000u);
                var maskB  = Vector256.Create(0x000000FFu);

                int vectorCount = count - (count % Vector256<uint>.Count);
                ref readonly uint srcRef = ref MemoryMarshal.GetReference((ReadOnlySpan<uint>)_src);
                ref uint dstRef = ref MemoryMarshal.GetArrayDataReference(_dst);

                for (; i < vectorCount; i += Vector256<uint>.Count)
                {
                    var v  = Vector256.LoadUnsafe(in srcRef, (uint)i);
                    var ag = Vector256.BitwiseAnd(v, maskAG);
                    var r  = Vector256.ShiftRightLogical(Vector256.BitwiseAnd(v, maskR), 16);
                    var b  = Vector256.ShiftLeft(Vector256.BitwiseAnd(v, maskB), 16);
                    var rgba = Vector256.BitwiseOr(ag, Vector256.BitwiseOr(r, b));
                    rgba.StoreUnsafe(ref dstRef, (uint)i);
                }
            }

            for (; i < count; i++)
            {
                uint argb = _src[i];
                _dst[i] = (argb & 0xFF00FF00u) | ((argb & 0x00FF0000u) >> 16) | ((argb & 0x000000FFu) << 16);
            }
        }
    }
}
