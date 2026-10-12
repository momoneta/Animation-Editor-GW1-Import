using System.Numerics;

namespace SELib;

public class Quaternion : Vector3
{
	public static readonly Quaternion Identity = new Quaternion
	{
		X = 0.0,
		Y = 0.0,
		Z = 0.0,
		W = 1.0
	};

	public double W { get; set; }

	public Quaternion()
	{
		base.X = 0.0;
		base.Y = 0.0;
		base.Z = 0.0;
		W = 1.0;
	}

	public Quaternion(float XCoord, float YCoord, float ZCoord, float WCoord)
	{
		base.X = XCoord;
		base.Y = YCoord;
		base.Z = ZCoord;
		W = WCoord;
	}

	public override bool Equals(object obj)
	{
		Quaternion quaternion = (Quaternion)obj;
		if (quaternion.X == base.X && quaternion.Y == base.Y && quaternion.Z == base.Z)
		{
			return quaternion.W == W;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return base.GetHashCode() ^ W.GetHashCode();
	}

	public static implicit operator Quaternion(System.Numerics.Quaternion vec)
	{
		return new Quaternion(vec.X, vec.Y, vec.Z, vec.W);
	}
}
