using Microsoft.Xna.Framework;

namespace Roentgen.Rendering;

public readonly record struct CameraState(
    Vector3 Position,
    Vector3 Forward,
    Vector3 Up,
    float VerticalFieldOfViewRadians,
    float NearPlane,
    float FarPlane)
{
    public static CameraState CreateDefault(float aspectRatio, RenderingOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(aspectRatio, 0f);
        options.Validate();
        return new CameraState(
            new Vector3(0f, 9f, 18f),
            Vector3.Normalize(new Vector3(0f, -0.2f, -1f)),
            Vector3.Up,
            MathHelper.ToRadians(options.FieldOfViewDegrees),
            options.NearPlane,
            options.FarPlane);
    }

    public CameraMatrices ToMatrices(float aspectRatio)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(aspectRatio, 0f);
        Vector3 forward = Forward;
        if (forward.LengthSquared() < 0.000001f)
        {
            throw new InvalidOperationException("Camera forward vector cannot be zero.");
        }

        forward.Normalize();
        Vector3 up = Up;
        if (up.LengthSquared() < 0.000001f)
        {
            throw new InvalidOperationException("Camera up vector cannot be zero.");
        }

        up.Normalize();
        Matrix view = Matrix.CreateLookAt(Position, Position + forward, up);
        Matrix projection = Matrix.CreatePerspectiveFieldOfView(
            VerticalFieldOfViewRadians,
            aspectRatio,
            NearPlane,
            FarPlane);
        return new CameraMatrices(view, projection, view * projection, new BoundingFrustum(view * projection));
    }
}

public sealed record CameraMatrices(Matrix View, Matrix Projection, Matrix ViewProjection, BoundingFrustum Frustum);
