namespace Roentgen.Assets;

public enum MeshPrimitiveKind
{
    Box = 0,
    Cylinder = 1,
    Cone = 2,
    UvSphere = 3,
    Wedge = 4,
    Lathe = 5,
    Extrusion = 6,
    Tube = 7
}

public enum MeshModifierKind
{
    Translate = 0,
    Scale = 1,
    RotateEulerDegrees = 2,
    Taper = 3,
    TwistDegrees = 4,
    BendDegrees = 5,
    NoiseDisplace = 6,
    ClipBelow = 7,
    ClipAbove = 8
}

public enum UvProjectionKind
{
    Primitive = 0,
    PlanarX = 1,
    PlanarY = 2,
    PlanarZ = 3,
    CylindricalY = 4,
    Spherical = 5
}

public enum CollisionProxyKind
{
    None = 0,
    AxisAlignedBox = 1,
    BoundingSphere = 2,
    CapsuleY = 3,
    CompoundBoxes = 4
}

public enum MaterialLayerKind
{
    Solid = 0,
    VerticalGradient = 1,
    Checker = 2,
    ValueNoise = 3,
    RustSpots = 4,
    Scratches = 5,
    WarningStripes = 6,
    EdgeWear = 7,
    Speckles = 8
}

public enum MaterialChannel
{
    Albedo = 0,
    Roughness = 1,
    Metallic = 2,
    Emissive = 3,
    Height = 4
}
