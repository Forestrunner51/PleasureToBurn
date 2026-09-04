using Godot;

namespace PleasureToBurn;

/// <summary>
/// Placeholder sounds generated in code so the prototype has audio feedback without assets.
/// Replace with real samples later; keep the call sites.
/// </summary>
public static class ProceduralAudio
{
    private static AudioStreamWav? _whoomp;

    /// <summary>A short low "whoomp": filtered noise burst with a sub thump. Used for ignition.</summary>
    public static AudioStreamWav Whoomp()
    {
        if (_whoomp is not null)
            return _whoomp;

        const int rate = 22050;
        const float seconds = 0.4f;
        var samples = (int)(rate * seconds);
        var data = new byte[samples * 2];
        var rng = new Random(7);
        var lowPass = 0f;

        for (var i = 0; i < samples; i++)
        {
            var t = i / (float)rate;
            var envelope = Mathf.Exp(-t * 11f);
            var noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            lowPass += (noise - lowPass) * 0.09f;
            var thump = Mathf.Sin(Mathf.Tau * (70f + 40f * envelope) * t);
            var sample = Mathf.Clamp(lowPass * 1.6f * envelope + thump * 0.45f * envelope, -1f, 1f);
            var value = (short)(sample * short.MaxValue);
            data[i * 2] = (byte)(value & 0xFF);
            data[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
        }

        _whoomp = new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = rate,
            Stereo = false,
            Data = data,
        };
        return _whoomp;
    }
}
