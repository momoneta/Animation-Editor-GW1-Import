using System;
using System.Numerics;

namespace AssetBankPlugin.Export;

public class Transform
{
	public Vector3 Position;

	public Quaternion Rotation = Quaternion.Identity;

	public Vector3 Scale;

	public Vector3 EulerAngles => ToEulerAngles(Rotation);

	public Matrix4x4 Matrix => Matrix4x4.Identity * Matrix4x4.CreateFromQuaternion(Rotation) * Matrix4x4.CreateScale(Scale) * Matrix4x4.CreateTranslation(Position);

	public Transform(Vector3 position, Quaternion rotation, Vector3 scale)
	{
		Position = position;
		Scale = scale;
		Rotation = rotation;
	}

	public Transform(Matrix4x4 matrix)
	{
		Position = matrix.Translation;
		Rotation = Quaternion.CreateFromRotationMatrix(matrix);
		Scale = new Vector3(matrix.GetDeterminant());
	}

	public static Transform Interpolate(Transform a, Transform b, float progression)
	{
		Vector3 position = Vector3.Lerp(a.Position, b.Position, progression);
		Quaternion rotation = Quaternion.Slerp(a.Rotation, b.Rotation, progression);
		Vector3 scale = Vector3.Lerp(a.Position, b.Position, progression);
		return new Transform(position, rotation, scale);
	}

	public static Transform operator *(Transform a, Transform b)
	{
		return new Transform(a.Position * b.Position, a.Rotation * b.Rotation, a.Scale * b.Scale);
	}

	public static Transform operator +(Transform a, Transform b)
	{
		return new Transform(a.Position + b.Position, a.Rotation + b.Rotation, a.Scale + b.Scale);
	}

	public static Transform operator -(Transform a, Transform b)
	{
		return new Transform(a.Position - b.Position, a.Rotation - b.Rotation, a.Scale - b.Scale);
	}

	public static Transform operator *(Transform a, float b)
	{
		return new Transform(a.Position * b, a.Rotation * b, a.Scale * b);
	}

	public static Vector3 ToEulerAngles(Quaternion quaternion)
	{
		Vector3 result = default(Vector3);
		float num = 2f * (quaternion.W * quaternion.X + quaternion.Y * quaternion.Z);
		float num2 = 1f - 2f * (quaternion.X * quaternion.X + quaternion.Y * quaternion.Y);
		result.X = (float)Math.Atan2(num, num2);
		float num3 = 2f * (quaternion.W * quaternion.Y - quaternion.Z * quaternion.X);
		num3 = ((num3 > 1f) ? 1f : num3);
		num3 = ((num3 < -1f) ? (-1f) : num3);
		result.Y = (float)Math.Asin(num3);
		float num4 = 2f * (quaternion.W * quaternion.Z + quaternion.X * quaternion.Y);
		float num5 = 1f - 2f * (quaternion.Y * quaternion.Y + quaternion.Z * quaternion.Z);
		result.Z = (float)Math.Atan2(num4, num5);
		return result;
	}
}
