using System;
using System.Runtime.InteropServices;
using FrostySdk.IO;

namespace AssetBankPlugin.Ant;

public abstract class EndianBitConverter
{
	[StructLayout(LayoutKind.Explicit)]
	private struct Int32SingleUnion
	{
		[FieldOffset(0)]
		private readonly int m_IntValue;

		[FieldOffset(0)]
		private readonly float m_FloatValue;

		internal int AsInt32 => m_IntValue;

		internal float AsSingle => m_FloatValue;

		internal Int32SingleUnion(int p_IntValue)
		{
			m_FloatValue = 0f;
			m_IntValue = p_IntValue;
		}

		internal Int32SingleUnion(float p_FloatValue)
		{
			m_IntValue = 0;
			m_FloatValue = p_FloatValue;
		}
	}

	public abstract Endian Endianness { get; }

	public static LittleEndianBitConverter Little { get; } = new LittleEndianBitConverter();

	public static BigEndianBitConverter Big { get; } = new BigEndianBitConverter();

	public abstract bool IsLittleEndian();

	public long DoubleToInt64Bits(double p_Value)
	{
		return BitConverter.DoubleToInt64Bits(p_Value);
	}

	public double Int64BitsToDouble(long p_Value)
	{
		return BitConverter.Int64BitsToDouble(p_Value);
	}

	public int SingleToInt32Bits(float p_Value)
	{
		return new Int32SingleUnion(p_Value).AsInt32;
	}

	public float Int32BitsToSingle(int p_Value)
	{
		return new Int32SingleUnion(p_Value).AsSingle;
	}

	public bool ToBoolean(byte[] p_Value, int p_StartIndex)
	{
		CheckByteArgument(p_Value, p_StartIndex, 1);
		return BitConverter.ToBoolean(p_Value, p_StartIndex);
	}

	public char ToChar(byte[] p_Value, int p_StartIndex)
	{
		return (char)CheckedFromBytes(p_Value, p_StartIndex, 2);
	}

	public double ToDouble(byte[] p_Value, int p_StartIndex)
	{
		return Int64BitsToDouble(ToInt64(p_Value, p_StartIndex));
	}

	public float ToSingle(byte[] p_Value, int p_StartIndex)
	{
		return Int32BitsToSingle(ToInt32(p_Value, p_StartIndex));
	}

	public short ToInt16(byte[] p_Value, int p_StartIndex)
	{
		return (short)CheckedFromBytes(p_Value, p_StartIndex, 2);
	}

	public int ToInt32(byte[] p_Value, int p_StartIndex)
	{
		return (int)CheckedFromBytes(p_Value, p_StartIndex, 4);
	}

	public long ToInt64(byte[] p_Value, int p_StartIndex)
	{
		return CheckedFromBytes(p_Value, p_StartIndex, 8);
	}

	public ushort ToUInt16(byte[] p_Value, int p_StartIndex)
	{
		return (ushort)CheckedFromBytes(p_Value, p_StartIndex, 2);
	}

	public uint ToUInt32(byte[] p_Value, int p_StartIndex)
	{
		return (uint)CheckedFromBytes(p_Value, p_StartIndex, 4);
	}

	public ulong ToUInt64(byte[] p_Value, int p_StartIndex)
	{
		return (ulong)CheckedFromBytes(p_Value, p_StartIndex, 8);
	}

	protected static void CheckByteArgument(byte[] p_Value, int p_StartIndex, int p_BytesRequired)
	{
		if (p_Value == null)
		{
			throw new ArgumentNullException("p_Value");
		}
		if (p_StartIndex < 0 || p_StartIndex > p_Value.Length - p_BytesRequired)
		{
			throw new ArgumentOutOfRangeException("p_StartIndex");
		}
	}

	protected long CheckedFromBytes(byte[] p_Value, int p_StartIndex, int p_BytesToConvert)
	{
		CheckByteArgument(p_Value, p_StartIndex, p_BytesToConvert);
		return FromBytes(p_Value, p_StartIndex, p_BytesToConvert);
	}

	protected abstract long FromBytes(byte[] p_Value, int p_StartIndex, int p_BytesToConvert);

	public static string ToString(byte[] p_Value)
	{
		return BitConverter.ToString(p_Value);
	}

	public static string ToString(byte[] p_Value, int p_StartIndex)
	{
		return BitConverter.ToString(p_Value, p_StartIndex);
	}

	public static string ToString(byte[] p_Value, int p_StartIndex, int p_Length)
	{
		return BitConverter.ToString(p_Value, p_StartIndex, p_Length);
	}

	public decimal ToDecimal(byte[] p_Value, int p_StartIndex)
	{
		int[] array = new int[4];
		for (int i = 0; i < 4; i++)
		{
			array[i] = ToInt32(p_Value, p_StartIndex + i * 4);
		}
		return new decimal(array);
	}

	public byte[] GetBytes(decimal p_Value)
	{
		byte[] array = new byte[16];
		int[] bits = decimal.GetBits(p_Value);
		for (int i = 0; i < 4; i++)
		{
			CopyBytesImpl(bits[i], 4, array, i * 4);
		}
		return array;
	}

	public void CopyBytes(decimal p_Value, byte[] p_Buffer, int p_Index)
	{
		int[] bits = decimal.GetBits(p_Value);
		for (int i = 0; i < 4; i++)
		{
			CopyBytesImpl(bits[i], 4, p_Buffer, i * 4 + p_Index);
		}
	}

	private byte[] GetBytes(long p_Value, int p_Bytes)
	{
		byte[] array = new byte[p_Bytes];
		CopyBytes(p_Value, p_Bytes, array, 0);
		return array;
	}

	public byte[] GetBytes(bool p_Value)
	{
		return BitConverter.GetBytes(p_Value);
	}

	public byte[] GetBytes(char p_Value)
	{
		return GetBytes(p_Value, 2);
	}

	public byte[] GetBytes(double p_Value)
	{
		return GetBytes(DoubleToInt64Bits(p_Value), 8);
	}

	public byte[] GetBytes(short p_Value)
	{
		return GetBytes(p_Value, 2);
	}

	public byte[] GetBytes(int p_Value)
	{
		return GetBytes(p_Value, 4);
	}

	public byte[] GetBytes(long p_Value)
	{
		return GetBytes(p_Value, 8);
	}

	public byte[] GetBytes(float p_Value)
	{
		return GetBytes(SingleToInt32Bits(p_Value), 4);
	}

	public byte[] GetBytes(ushort p_Value)
	{
		return GetBytes(p_Value, 2);
	}

	public byte[] GetBytes(uint p_Value)
	{
		return GetBytes(p_Value, 4);
	}

	public byte[] GetBytes(ulong p_Value)
	{
		return GetBytes((long)p_Value, 8);
	}

	private void CopyBytes(long p_Value, int p_Bytes, byte[] p_Buffer, int p_Index)
	{
		if (p_Buffer.Length < p_Index + p_Bytes)
		{
			throw new ArgumentOutOfRangeException("p_Buffer", "Buffer not big enough for value");
		}
		CopyBytesImpl(p_Value, p_Bytes, p_Buffer, p_Index);
	}

	protected abstract void CopyBytesImpl(long p_Value, int p_Bytes, byte[] p_Buffer, int p_Index);

	public void CopyBytes(bool p_Value, byte[] p_Buffer, int p_Index)
	{
		CopyBytes(p_Value ? 1 : 0, 1, p_Buffer, p_Index);
	}

	public void CopyBytes(char p_Value, byte[] p_Buffer, int p_Index)
	{
		CopyBytes(p_Value, 2, p_Buffer, p_Index);
	}

	public void CopyBytes(double p_Value, byte[] p_Buffer, int p_Index)
	{
		CopyBytes(DoubleToInt64Bits(p_Value), 8, p_Buffer, p_Index);
	}

	public void CopyBytes(short p_Value, byte[] p_Buffer, int p_Index)
	{
		CopyBytes(p_Value, 2, p_Buffer, p_Index);
	}

	public void CopyBytes(int p_Value, byte[] p_Buffer, int p_Index)
	{
		CopyBytes(p_Value, 4, p_Buffer, p_Index);
	}

	public void CopyBytes(long p_Value, byte[] p_Buffer, int p_Index)
	{
		CopyBytes(p_Value, 8, p_Buffer, p_Index);
	}

	public void CopyBytes(float p_Value, byte[] p_Buffer, int p_Index)
	{
		CopyBytes(SingleToInt32Bits(p_Value), 4, p_Buffer, p_Index);
	}

	public void CopyBytes(ushort p_Value, byte[] p_Buffer, int p_Index)
	{
		CopyBytes(p_Value, 2, p_Buffer, p_Index);
	}

	public void CopyBytes(uint p_Value, byte[] p_Buffer, int p_Index)
	{
		CopyBytes(p_Value, 4, p_Buffer, p_Index);
	}

	public void CopyBytes(ulong p_Value, byte[] p_Buffer, int p_Index)
	{
		CopyBytes((long)p_Value, 8, p_Buffer, p_Index);
	}
}
