using System.Globalization;

namespace Roentgen.Contracts;

public readonly record struct StableId(ulong High, ulong Low) : IComparable<StableId>
{
    public static StableId Empty => default;

    public bool IsEmpty => High == 0UL && Low == 0UL;

    public int CompareTo(StableId other)
    {
        int high = High.CompareTo(other.High);
        return high != 0 ? high : Low.CompareTo(other.Low);
    }

    public override string ToString() => string.Concat(
        High.ToString("x16", CultureInfo.InvariantCulture),
        Low.ToString("x16", CultureInfo.InvariantCulture));

    public static StableId Parse(string value)
    {
        if (!TryParse(value, out StableId result))
        {
            throw new FormatException("A stable ID must contain exactly 32 hexadecimal characters.");
        }

        return result;
    }

    public static bool TryParse(string? value, out StableId result)
    {
        result = default;
        if (value is null || value.Length != 32)
        {
            return false;
        }

        bool highValid = ulong.TryParse(value.AsSpan(0, 16), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong high);
        bool lowValid = ulong.TryParse(value.AsSpan(16, 16), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong low);
        if (!highValid || !lowValid)
        {
            return false;
        }

        result = new StableId(high, low);
        return true;
    }
}
