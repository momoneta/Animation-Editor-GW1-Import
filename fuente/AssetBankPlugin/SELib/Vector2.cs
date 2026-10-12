namespace SELib;

public class Vector2 : KeyData
{
	public static readonly Vector2 Zero = new Vector2
	{
		X = 0.0,
		Y = 0.0
	};

	public static readonly Vector2 One = new Vector2
	{
		X = 1.0,
		Y = 1.0
	};

	public double X { get; set; }

	public double Y { get; set; }

	public Vector2()
	{
		X = 0.0;
		Y = 0.0;
	}

	public Vector2(float XCoord, float YCoord)
	{
		X = XCoord;
		Y = YCoord;
	}

	public override bool Equals(object obj)
	{
		Vector2 vector = (Vector2)obj;
		if (vector.X == X)
		{
			return vector.Y == Y;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return X.GetHashCode() ^ Y.GetHashCode();
	}
}
