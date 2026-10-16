using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Roentgen.Rendering;

public sealed class FlyCamera
{
    private float _yaw = MathHelper.Pi;
    private float _pitch = -0.15f;
    private KeyboardState _previousKeyboard;
    private bool _mouseInitialized;

    public Vector3 Position { get; private set; } = new(0f, 9f, 18f);
    public float MoveSpeed { get; set; } = 14f;
    public float FastMultiplier { get; set; } = 4f;
    public float MouseSensitivity { get; set; } = 0.0025f;
    public bool MouseLookEnabled { get; private set; }

    public void Update(GameWindow window, GameTime gameTime)
    {
        KeyboardState keyboard = Keyboard.GetState();
        MouseState mouse = Mouse.GetState();
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (keyboard.IsKeyDown(Keys.Tab) && _previousKeyboard.IsKeyUp(Keys.Tab))
        {
            MouseLookEnabled = !MouseLookEnabled;
            _mouseInitialized = false;
        }

        Vector3 forward = Forward;
        Vector3 flatForward = new(forward.X, 0f, forward.Z);
        if (flatForward.LengthSquared() > 0.000001f)
        {
            flatForward.Normalize();
        }

        Vector3 right = Vector3.Normalize(Vector3.Cross(flatForward, Vector3.Up));
        Vector3 movement = Vector3.Zero;
        if (keyboard.IsKeyDown(Keys.W)) movement += flatForward;
        if (keyboard.IsKeyDown(Keys.S)) movement -= flatForward;
        if (keyboard.IsKeyDown(Keys.D)) movement += right;
        if (keyboard.IsKeyDown(Keys.A)) movement -= right;
        if (keyboard.IsKeyDown(Keys.Space)) movement += Vector3.Up;
        if (keyboard.IsKeyDown(Keys.LeftControl)) movement -= Vector3.Up;
        if (movement.LengthSquared() > 1f) movement.Normalize();

        float speed = MoveSpeed * (keyboard.IsKeyDown(Keys.LeftShift) ? FastMultiplier : 1f);
        Position += movement * speed * dt;

        if (MouseLookEnabled)
        {
            int centerX = window.ClientBounds.Width / 2;
            int centerY = window.ClientBounds.Height / 2;
            if (_mouseInitialized)
            {
                _yaw -= (mouse.X - centerX) * MouseSensitivity;
                _pitch -= (mouse.Y - centerY) * MouseSensitivity;
                _pitch = MathHelper.Clamp(_pitch, -1.52f, 1.52f);
            }

            Mouse.SetPosition(centerX, centerY);
            _mouseInitialized = true;
        }

        _previousKeyboard = keyboard;
    }

    public void SetPosition(Vector3 position)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        Position = position;
    }

    public Vector3 Forward => Vector3.Normalize(new Vector3(
        MathF.Sin(_yaw) * MathF.Cos(_pitch),
        MathF.Sin(_pitch),
        MathF.Cos(_yaw) * MathF.Cos(_pitch)));

    public CameraState ToState(RenderingOptions options) => new(
        Position,
        Forward,
        Vector3.Up,
        MathHelper.ToRadians(options.FieldOfViewDegrees),
        options.NearPlane,
        options.FarPlane);
}
