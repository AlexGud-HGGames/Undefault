namespace Core.Models;

public readonly record struct Vector3(float X, float Y, float Z)
{
    public static Vector3 Zero => new(0f, 0f, 0f);

    public float DistanceTo(Vector3 other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        var dz = Z - other.Z;
        return MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }
}
