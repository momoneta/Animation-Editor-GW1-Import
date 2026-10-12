namespace SELib;

public class Color
{
	public static readonly Color White = new Color(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);

	public byte R { get; set; }

	public byte G { get; set; }

	public byte B { get; set; }

	public byte A { get; set; }

	public Color()
	{
		R = byte.MaxValue;
		G = byte.MaxValue;
		B = byte.MaxValue;
		A = byte.MaxValue;
	}

	public Color(byte Red, byte Green, byte Blue, byte Alpha)
	{
		R = Red;
		G = Green;
		B = Blue;
		A = Alpha;
	}

	public override bool Equals(object obj)
	{
		Color color = (Color)obj;
		if (color.R == R && color.G == G && color.B == B)
		{
			return color.A == A;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return R.GetHashCode() ^ G.GetHashCode() ^ B.GetHashCode() ^ A.GetHashCode();
	}
}
