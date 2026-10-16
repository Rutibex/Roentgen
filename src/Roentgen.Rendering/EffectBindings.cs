using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Roentgen.Rendering;

internal static class EffectBindings
{
    public static void Set(Effect effect, string name, Matrix value) => effect.Parameters[name]?.SetValue(value);
    public static void Set(Effect effect, string name, Matrix[] value) => effect.Parameters[name]?.SetValue(value);
    public static void Set(Effect effect, string name, Vector2 value) => effect.Parameters[name]?.SetValue(value);
    public static void Set(Effect effect, string name, Vector3 value) => effect.Parameters[name]?.SetValue(value);
    public static void Set(Effect effect, string name, Vector4 value) => effect.Parameters[name]?.SetValue(value);
    public static void Set(Effect effect, string name, float value) => effect.Parameters[name]?.SetValue(value);
    public static void Set(Effect effect, string name, bool value) => effect.Parameters[name]?.SetValue(value);
    public static void Set(Effect effect, string name, Texture2D value) => effect.Parameters[name]?.SetValue(value);
}
