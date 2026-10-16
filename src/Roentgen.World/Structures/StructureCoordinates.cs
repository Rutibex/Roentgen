using System.Globalization;

namespace Roentgen.World.Structures;

public readonly record struct StructurePoint(int XCentimeters, int YCentimeters, int ZCentimeters)
{
    public static StructurePoint FromMeters(float x, float y, float z) => new(
        WorldUnits.MetersToCentimeters(x), WorldUnits.MetersToCentimeters(y), WorldUnits.MetersToCentimeters(z));

    public override string ToString() => string.Create(CultureInfo.InvariantCulture,
        $"({WorldUnits.CentimetersToMeters(XCentimeters):F2}, {WorldUnits.CentimetersToMeters(YCentimeters):F2}, {WorldUnits.CentimetersToMeters(ZCentimeters):F2}) m");
}

public readonly record struct StructureRect(int MinimumXCentimeters, int MinimumZCentimeters, int MaximumXCentimeters, int MaximumZCentimeters)
{
    public int WidthCentimeters => checked(MaximumXCentimeters - MinimumXCentimeters);
    public int DepthCentimeters => checked(MaximumZCentimeters - MinimumZCentimeters);
    public int AreaSquareCentimeters => checked(WidthCentimeters * DepthCentimeters);
    public int CenterXCentimeters => MinimumXCentimeters + WidthCentimeters / 2;
    public int CenterZCentimeters => MinimumZCentimeters + DepthCentimeters / 2;

    public bool Contains(int x, int z) => x >= MinimumXCentimeters && x <= MaximumXCentimeters && z >= MinimumZCentimeters && z <= MaximumZCentimeters;
    public bool Intersects(StructureRect other) => MinimumXCentimeters < other.MaximumXCentimeters && MaximumXCentimeters > other.MinimumXCentimeters && MinimumZCentimeters < other.MaximumZCentimeters && MaximumZCentimeters > other.MinimumZCentimeters;
    public StructureRect Inset(int centimeters) => new(MinimumXCentimeters + centimeters, MinimumZCentimeters + centimeters, MaximumXCentimeters - centimeters, MaximumZCentimeters - centimeters);
    public StructureRect Translate(int x, int z) => new(MinimumXCentimeters + x, MinimumZCentimeters + z, MaximumXCentimeters + x, MaximumZCentimeters + z);

    public void Validate()
    {
        if (WidthCentimeters <= 0 || DepthCentimeters <= 0) throw new InvalidOperationException("A structure rectangle must have positive dimensions.");
    }
}

public readonly record struct StructureBox(StructurePoint Minimum, StructurePoint Maximum)
{
    public int WidthCentimeters => checked(Maximum.XCentimeters - Minimum.XCentimeters);
    public int HeightCentimeters => checked(Maximum.YCentimeters - Minimum.YCentimeters);
    public int DepthCentimeters => checked(Maximum.ZCentimeters - Minimum.ZCentimeters);
    public StructurePoint Center => new(Minimum.XCentimeters + WidthCentimeters / 2, Minimum.YCentimeters + HeightCentimeters / 2, Minimum.ZCentimeters + DepthCentimeters / 2);

    public bool Contains(StructurePoint point) => point.XCentimeters >= Minimum.XCentimeters && point.XCentimeters <= Maximum.XCentimeters && point.YCentimeters >= Minimum.YCentimeters && point.YCentimeters <= Maximum.YCentimeters && point.ZCentimeters >= Minimum.ZCentimeters && point.ZCentimeters <= Maximum.ZCentimeters;
    public bool Intersects(StructureBox other) => Minimum.XCentimeters < other.Maximum.XCentimeters && Maximum.XCentimeters > other.Minimum.XCentimeters && Minimum.YCentimeters < other.Maximum.YCentimeters && Maximum.YCentimeters > other.Minimum.YCentimeters && Minimum.ZCentimeters < other.Maximum.ZCentimeters && Maximum.ZCentimeters > other.Minimum.ZCentimeters;

    public void Validate()
    {
        if (WidthCentimeters <= 0 || HeightCentimeters <= 0 || DepthCentimeters <= 0) throw new InvalidOperationException("A structure box must have positive dimensions.");
    }
}
