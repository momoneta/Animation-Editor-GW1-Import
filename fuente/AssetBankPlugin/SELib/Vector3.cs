using System.Numerics;

namespace SELib;

public class Vector3 : Vector2
{
	public new static readonly Vector3 Zero = new Vector3
	{
		X = 0.0,
		Y = 0.0,
		Z = 0.0
	};

	public new static readonly Vector3 One = new Vector3
	{
		X = 1.0,
		Y = 1.0,
		Z = 1.0
	};

	public double Z { get; set; }

	public Vector3()
	{
		base.X = 0.0;
		base.Y = 0.0;
		Z = 0.0;
	}

	public Vector3(float XCoord, float YCoord, float ZCoord)
	{
		base.X = XCoord;
		base.Y = YCoord;
		Z = ZCoord;
	}

	public override bool Equals(object obj)
	{
		Vector3 vector = (Vector3)obj;
		if (vector.X == base.X && vector.Y == base.Y)
		{
			return vector.Z == Z;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return base.GetHashCode() ^ Z.GetHashCode();
	}

	public static implicit operator Vector3(System.Numerics.Vector3 vec)
	{
		return new Vector3(vec.X, vec.Y, vec.Z);
	}
}
