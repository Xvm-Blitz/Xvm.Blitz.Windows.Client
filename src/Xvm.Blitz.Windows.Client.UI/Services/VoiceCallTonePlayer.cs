using System.Runtime.InteropServices;

namespace Xvm.Blitz.Windows.Client.UI.Services;

public sealed class VoiceCallTonePlayer : IDisposable
{
    private const uint SndAsync = 0x0001;
    private const uint SndNoDefault = 0x0002;
    private const uint SndMemory = 0x0004;
    private const uint SndLoop = 0x0008;
    private const uint SndPurge = 0x0040;
    private const int SampleRate = 22050;

    private readonly Lock _lock = new();
    private GCHandle _pin;
    private byte[]? _buffer;
    private ToneKind _kind = ToneKind.None;

    public void PlayIncoming() => Play(ToneKind.Incoming, BuildIncoming(), loop: true);

    public void PlayRingback() => Play(ToneKind.Ringback, BuildRingback(), loop: true);

    public void PlayBusy() => Play(ToneKind.Busy, BuildBusy(), loop: false);

    public void Stop()
    {
        lock (_lock)
            StopLocked();
    }

    public void Dispose() => Stop();

    private void Play(ToneKind kind, byte[] wav, bool loop)
    {
        lock (_lock)
        {
            if (_kind == kind && kind != ToneKind.Busy)
                return;

            StopLocked();
            _kind = kind;
            _buffer = wav;
            _pin = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
            var flags = SndAsync | SndMemory | SndNoDefault;
            if (loop)
                flags |= SndLoop;

            PlaySound(_pin.AddrOfPinnedObject(), IntPtr.Zero, flags);
        }
    }

    private void StopLocked()
    {
        PlaySound(IntPtr.Zero, IntPtr.Zero, SndPurge);
        if (_pin.IsAllocated)
            _pin.Free();

        _buffer = null;
        _kind = ToneKind.None;
    }

    private static byte[] BuildIncoming() =>
        BuildCycleWav(
        [
            new ToneSegment(880, 400),
            new ToneSegment(0, 140),
            new ToneSegment(880, 400),
            new ToneSegment(0, 1_200),
        ]);

    private static byte[] BuildRingback() =>
        BuildCycleWav(
        [
            new ToneSegment(425, 1_000),
            new ToneSegment(0, 2_000),
        ]);

    private static byte[] BuildBusy() =>
        BuildCycleWav(
        [
            new ToneSegment(425, 350),
            new ToneSegment(0, 350),
            new ToneSegment(425, 350),
            new ToneSegment(0, 350),
            new ToneSegment(425, 350),
            new ToneSegment(0, 400),
        ]);

    private static byte[] BuildCycleWav(IReadOnlyList<ToneSegment> segments)
    {
        var samples = segments.Sum(segment => Math.Max(1, SampleRate * segment.DurationMs / 1_000));
        using var stream = new MemoryStream(44 + samples * 2);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + samples * 2);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(samples * 2);

        var index = 0;
        foreach (var segment in segments)
        {
            var count = Math.Max(1, SampleRate * segment.DurationMs / 1_000);
            for (var sample = 0; sample < count; sample++, index++)
            {
                var amplitude = segment.FrequencyHz <= 0
                    ? 0d
                    : Math.Sin(2 * Math.PI * segment.FrequencyHz * sample / SampleRate) * 0.28;
                writer.Write((short)(amplitude * short.MaxValue));
            }
        }

        return stream.ToArray();
    }

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", SetLastError = true)]
    private static extern bool PlaySound(IntPtr sound, IntPtr hmod, uint fdwsound);

    private enum ToneKind
    {
        None,
        Incoming,
        Ringback,
        Busy,
    }

    private readonly record struct ToneSegment(double FrequencyHz, int DurationMs);
}
