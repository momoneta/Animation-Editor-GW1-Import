using System;

namespace AssetBankPlugin.Import.Gw;

public static class GwBinary
{
	public static ushort ReadU16(byte[] b, int o, bool big)
	{
		return (ushort)(big ? ((b[o] << 8) | b[o + 1]) : (b[o] | (b[o + 1] << 8)));
	}

	public static uint ReadU32(byte[] b, int o, bool big)
	{
		if (!big)
		{
			return (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));
		}
		return (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);
	}

	public static ulong ReadU64(byte[] b, int o, bool big)
	{
		ulong num = ReadU32(b, o, big);
		ulong num2 = ReadU32(b, o + 4, big);
		if (!big)
		{
			return (num2 << 32) | num;
		}
		return (num << 32) | num2;
	}

	public static float ReadF32(byte[] b, int o, bool big)
	{
		return BitConverter.ToSingle(BitConverter.GetBytes(ReadU32(b, o, big)), 0);
	}

	public static void WriteU8(byte[] b, int o, byte v)
	{
		b[o] = v;
	}

	public static void WriteU16(byte[] b, int o, ushort v, bool big)
	{
		for (int i = 0; i < 2; i++)
		{
			b[o + (big ? (1 - i) : i)] = (byte)(v >> i * 8);
		}
	}

	public static void WriteU32(byte[] b, int o, uint v, bool big)
	{
		for (int i = 0; i < 4; i++)
		{
			b[o + (big ? (3 - i) : i)] = (byte)(v >> i * 8);
		}
	}

	public static void WriteU64(byte[] b, int o, ulong v, bool big)
	{
		for (int i = 0; i < 8; i++)
		{
			b[o + (big ? (7 - i) : i)] = (byte)(v >> i * 8);
		}
	}

	public static void WriteF32(byte[] b, int o, float v, bool big)
	{
		WriteU32(b, o, BitConverter.ToUInt32(BitConverter.GetBytes(v), 0), big);
	}

	public static void WriteF64(byte[] b, int o, double v, bool big)
	{
		WriteU64(b, o, (ulong)BitConverter.DoubleToInt64Bits(v), big);
	}

	public static void WriteGuid(byte[] b, int o, Guid g, bool big)
	{
		byte[] array = g.ToByteArray();
		if (big)
		{
			Array.Reverse(array, 0, 4);
			Array.Reverse(array, 4, 2);
			Array.Reverse(array, 6, 2);
		}
		Buffer.BlockCopy(array, 0, b, o, 16);
	}

	public static bool IsTag(byte[] b, int o, string tag)
	{
		if (o < 0 || o + tag.Length > b.Length)
		{
			return false;
		}
		for (int i = 0; i < tag.Length; i++)
		{
			if (b[o + i] != tag[i])
			{
				return false;
			}
		}
		return true;
	}

	public static bool SameBytes(byte[] b, int o, byte[] value)
	{
		if (o < 0 || o + value.Length > b.Length)
		{
			return false;
		}
		for (int i = 0; i < value.Length; i++)
		{
			if (b[o + i] != value[i])
			{
				return false;
			}
		}
		return true;
	}

	public static int Align(int value, int alignment)
	{
		if (alignment > 1)
		{
			return (value + alignment - 1) / alignment * alignment;
		}
		return value;
	}
}
