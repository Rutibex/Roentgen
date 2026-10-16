using Roentgen.Contracts;

namespace Roentgen.World;

public static class DeterministicTerrainNoise
{
    private const int Unit = 65_535;

    public static int FractalSigned(DomainSeed seed, int x, int z, int basePeriod, int octaves)
    {
        if (basePeriod <= 0 || octaves is < 1 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(basePeriod));
        }

        long total = 0;
        long weightTotal = 0;
        int period = basePeriod;
        int weight = 256;
        for (int octave = 0; octave < octaves; octave++)
        {
            total += (long)ValueSigned(seed.Value + (ulong)octave * 0x9E3779B97F4A7C15UL, x, z, Math.Max(1, period)) * weight;
            weightTotal += weight;
            period = Math.Max(1, period / 2);
            weight = Math.Max(1, weight / 2);
        }

        return checked((int)(total / weightTotal));
    }

    public static int FractalUnsigned(DomainSeed seed, int x, int z, int basePeriod, int octaves) =>
        Math.Clamp((FractalSigned(seed, x, z, basePeriod, octaves) + Unit) / 2, 0, Unit);

    public static int ValueSigned(ulong seed, int x, int z, int period)
    {
        int cellX = WorldUnits.FloorDivide(x, period);
        int cellZ = WorldUnits.FloorDivide(z, period);
        int localX = WorldUnits.PositiveModulo(x, period);
        int localZ = WorldUnits.PositiveModulo(z, period);
        int tx = Smooth(localX, period);
        int tz = Smooth(localZ, period);
        int a = HashSigned(seed, cellX, cellZ);
        int b = HashSigned(seed, cellX + 1, cellZ);
        int c = HashSigned(seed, cellX, cellZ + 1);
        int d = HashSigned(seed, cellX + 1, cellZ + 1);
        int top = Lerp(a, b, tx);
        int bottom = Lerp(c, d, tx);
        return Lerp(top, bottom, tz);
    }

    private static int Smooth(int numerator, int denominator)
    {
        long t = (long)numerator * Unit / denominator;
        long t2 = t * t / Unit;
        long smooth = t2 * (3L * Unit - 2L * t) / Unit;
        return checked((int)smooth);
    }

    private static int Lerp(int left, int right, int t) =>
        checked((int)(left + ((long)(right - left) * t + Unit / 2) / Unit));

    private static int HashSigned(ulong seed, int x, int z)
    {
        ulong value = seed;
        value ^= unchecked((ulong)(uint)x) * 0x9E3779B185EBCA87UL;
        value ^= unchecked((ulong)(uint)z) * 0xC2B2AE3D27D4EB4FUL;
        value ^= value >> 30;
        value *= 0xBF58476D1CE4E5B9UL;
        value ^= value >> 27;
        value *= 0x94D049BB133111EBUL;
        value ^= value >> 31;
        return checked((int)((value >> 48) & 0xFFFFUL)) - 32_768;
    }
}
