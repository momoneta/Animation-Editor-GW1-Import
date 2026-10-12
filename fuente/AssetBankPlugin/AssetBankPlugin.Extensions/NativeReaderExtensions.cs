using System;
using System.Runtime.InteropServices;
using AssetBankPlugin.Enums;
using AssetBankPlugin.Formats.GenericData;
using FrostySdk.IO;

namespace AssetBankPlugin.Extensions;

public static class NativeReaderExtensions
{
	[StructLayout(LayoutKind.Explicit)]
	private struct FloatUnion
	{
		[FieldOffset(0)]
		public uint UIntValue;

		[FieldOffset(0)]
		public float FloatValue;
	}

	[StructLayout(LayoutKind.Explicit)]
	private struct DoubleUnion
	{
		[FieldOffset(0)]
		public ulong ULongValue;

		[FieldOffset(0)]
		public double DoubleValue;
	}

	public static long ReadReference(this NativeReader r, Endian endian)
	{
		long position = r.BaseStream.Position;
		long num = r.ReadLong(endian);
		if (num == 0L || num == -1)
		{
			return 0L;
		}
		long num2 = num << 4 >> 4;
		long num3 = position + num2;
		if (num3 >= 0 && num3 <= r.BaseStream.Length)
		{
			return num3;
		}
		return 0L;
	}

	public static ReflLayout ReadReflLayoutEntry(this NativeReader r, Endian bigEndian, out int fieldSize)
	{
		ReflLayout reflLayout = new ReflLayout();
		reflLayout.mMinSlot = r.ReadInt(bigEndian);
		reflLayout.mMaxSlot = r.ReadInt(bigEndian);
		fieldSize = reflLayout.mMinSlot * -1 + reflLayout.mMaxSlot + 1;
		reflLayout.mDataSize = r.ReadUInt(bigEndian);
		reflLayout.mAlignment = r.ReadUInt(bigEndian);
		reflLayout.mStringTableOffset = r.ReadUInt(bigEndian);
		reflLayout.mStringTableLength = r.ReadUInt(bigEndian);
		reflLayout.mReordered = r.ReadBoolean();
		reflLayout.mNative = r.ReadBoolean();
		r.ReadBytes(2);
		reflLayout.mHash = r.ReadUInt(bigEndian);
		reflLayout.mEntries = new ReflEntry[fieldSize];
		for (int i = 0; i < fieldSize; i++)
		{
			ReflEntry reflEntry;
			while (true)
			{
				reflEntry = new ReflEntry();
				reflEntry.mLayoutHash = r.ReadUInt(bigEndian);
				reflEntry.mElementSize = r.ReadUInt(bigEndian);
				reflEntry.mOffset = r.ReadUInt(bigEndian);
				reflEntry.mName = r.ReadUInt(bigEndian);
				reflEntry.mCount = r.ReadUShort(bigEndian);
				reflEntry.mFlags = (EFlags)r.ReadUShort(bigEndian);
				reflEntry.mElementAlign = r.ReadUShort(bigEndian);
				reflEntry.mRLE = r.ReadShort(bigEndian);
				reflEntry.mLayout = r.ReadLong(bigEndian);
				if (reflEntry.mElementSize != 0 && reflEntry.mCount != 0)
				{
					break;
				}
				fieldSize--;
			}
			reflLayout.mEntries[i] = reflEntry;
		}
		r.ReadBytes(1);
		reflLayout.mName = r.ReadNullTerminatedString();
		reflLayout.mFieldNames = new string[fieldSize];
		for (int j = 0; j < fieldSize; j++)
		{
			reflLayout.mFieldNames[j] = r.ReadNullTerminatedString();
		}
		return reflLayout;
	}

	public static void ReadDataHeader(this NativeReader r, Endian bigEndian, out uint hash, out uint type, out uint offset)
	{
		hash = (uint)r.ReadULong(bigEndian);
		r.ReadBytes(8);
		type = (uint)r.ReadULong(bigEndian);
		r.ReadBytes(4);
		offset = r.ReadUShort(bigEndian);
		r.ReadBytes(2);
	}

	public static byte[] ReadByteArray(this NativeReader r, int count)
	{
		return r.ReadBytes(count);
	}

	public static sbyte[] ReadSByteArray(this NativeReader r, int count)
	{
		byte[] src = r.ReadBytes(count);
		sbyte[] array = new sbyte[count];
		Buffer.BlockCopy(src, 0, array, 0, count);
		return array;
	}

	public static short[] ReadShortArray(this NativeReader r, int count, Endian endian)
	{
		byte[] array = r.ReadBytes(count * 2);
		short[] array2 = new short[count];
		if (endian == Endian.Little)
		{
			for (int i = 0; i < count; i++)
			{
				int num = i * 2;
				array2[i] = (short)(array[num] | (array[num + 1] << 8));
			}
		}
		else
		{
			for (int j = 0; j < count; j++)
			{
				int num2 = j * 2;
				array2[j] = (short)(array[num2 + 1] | (array[num2] << 8));
			}
		}
		return array2;
	}

	public static ushort[] ReadUShortArray(this NativeReader r, int count, Endian endian)
	{
		byte[] array = r.ReadBytes(count * 2);
		ushort[] array2 = new ushort[count];
		if (endian == Endian.Little)
		{
			for (int i = 0; i < count; i++)
			{
				int num = i * 2;
				array2[i] = (ushort)(array[num] | (array[num + 1] << 8));
			}
		}
		else
		{
			for (int j = 0; j < count; j++)
			{
				int num2 = j * 2;
				array2[j] = (ushort)(array[num2 + 1] | (array[num2] << 8));
			}
		}
		return array2;
	}

	public static int[] ReadIntArray(this NativeReader r, int count, Endian endian)
	{
		byte[] array = r.ReadBytes(count * 4);
		int[] array2 = new int[count];
		if (endian == Endian.Little)
		{
			for (int i = 0; i < count; i++)
			{
				int num = i * 4;
				array2[i] = array[num] | (array[num + 1] << 8) | (array[num + 2] << 16) | (array[num + 3] << 24);
			}
		}
		else
		{
			for (int j = 0; j < count; j++)
			{
				int num2 = j * 4;
				array2[j] = array[num2 + 3] | (array[num2 + 2] << 8) | (array[num2 + 1] << 16) | (array[num2] << 24);
			}
		}
		return array2;
	}

	public static uint[] ReadUIntArray(this NativeReader r, int count, Endian endian)
	{
		byte[] array = r.ReadBytes(count * 4);
		uint[] array2 = new uint[count];
		if (endian == Endian.Little)
		{
			for (int i = 0; i < count; i++)
			{
				int num = i * 4;
				array2[i] = (uint)(array[num] | (array[num + 1] << 8) | (array[num + 2] << 16) | (array[num + 3] << 24));
			}
		}
		else
		{
			for (int j = 0; j < count; j++)
			{
				int num2 = j * 4;
				array2[j] = (uint)(array[num2 + 3] | (array[num2 + 2] << 8) | (array[num2 + 1] << 16) | (array[num2] << 24));
			}
		}
		return array2;
	}

	public static long[] ReadLongArray(this NativeReader r, int count, Endian endian)
	{
		byte[] array = r.ReadBytes(count * 8);
		long[] array2 = new long[count];
		if (endian == Endian.Little)
		{
			for (int i = 0; i < count; i++)
			{
				int num = i * 8;
				array2[i] = (long)(((ulong)(uint)(array[num + 4] | (array[num + 5] << 8) | (array[num + 6] << 16) | (array[num + 7] << 24)) << 32) | (uint)(array[num] | (array[num + 1] << 8) | (array[num + 2] << 16) | (array[num + 3] << 24)));
			}
		}
		else
		{
			for (int j = 0; j < count; j++)
			{
				int num2 = j * 8;
				array2[j] = (long)(((ulong)(uint)(array[num2 + 3] | (array[num2 + 2] << 8) | (array[num2 + 1] << 16) | (array[num2] << 24)) << 32) | (uint)(array[num2 + 7] | (array[num2 + 6] << 8) | (array[num2 + 5] << 16) | (array[num2 + 4] << 24)));
			}
		}
		return array2;
	}

	public static ulong[] ReadULongArray(this NativeReader r, int count, Endian endian)
	{
		byte[] array = r.ReadBytes(count * 8);
		ulong[] array2 = new ulong[count];
		if (endian == Endian.Little)
		{
			for (int i = 0; i < count; i++)
			{
				int num = i * 8;
				array2[i] = ((ulong)(uint)(array[num + 4] | (array[num + 5] << 8) | (array[num + 6] << 16) | (array[num + 7] << 24)) << 32) | (uint)(array[num] | (array[num + 1] << 8) | (array[num + 2] << 16) | (array[num + 3] << 24));
			}
		}
		else
		{
			for (int j = 0; j < count; j++)
			{
				int num2 = j * 8;
				array2[j] = ((ulong)(uint)(array[num2 + 3] | (array[num2 + 2] << 8) | (array[num2 + 1] << 16) | (array[num2] << 24)) << 32) | (uint)(array[num2 + 7] | (array[num2 + 6] << 8) | (array[num2 + 5] << 16) | (array[num2 + 4] << 24));
			}
		}
		return array2;
	}

	public static float[] ReadSingleArray(this NativeReader r, int count, Endian endian)
	{
		byte[] array = r.ReadBytes(count * 4);
		float[] array2 = new float[count];
		if (endian == Endian.Little)
		{
			for (int i = 0; i < count; i++)
			{
				int num = i * 4;
				array2[i] = new FloatUnion
				{
					UIntValue = (uint)(array[num] | (array[num + 1] << 8) | (array[num + 2] << 16) | (array[num + 3] << 24))
				}.FloatValue;
			}
		}
		else
		{
			for (int j = 0; j < count; j++)
			{
				int num2 = j * 4;
				array2[j] = new FloatUnion
				{
					UIntValue = (uint)(array[num2 + 3] | (array[num2 + 2] << 8) | (array[num2 + 1] << 16) | (array[num2] << 24))
				}.FloatValue;
			}
		}
		return array2;
	}

	public static double[] ReadDoubleArray(this NativeReader r, int count, Endian endian)
	{
		byte[] array = r.ReadBytes(count * 8);
		double[] array2 = new double[count];
		if (endian == Endian.Little)
		{
			for (int i = 0; i < count; i++)
			{
				int num = i * 8;
				uint num2 = (uint)(array[num] | (array[num + 1] << 8) | (array[num + 2] << 16) | (array[num + 3] << 24));
				uint num3 = (uint)(array[num + 4] | (array[num + 5] << 8) | (array[num + 6] << 16) | (array[num + 7] << 24));
				array2[i] = new DoubleUnion
				{
					ULongValue = (((ulong)num3 << 32) | num2)
				}.DoubleValue;
			}
		}
		else
		{
			for (int j = 0; j < count; j++)
			{
				int num4 = j * 8;
				uint num5 = (uint)(array[num4 + 3] | (array[num4 + 2] << 8) | (array[num4 + 1] << 16) | (array[num4] << 24));
				uint num6 = (uint)(array[num4 + 7] | (array[num4 + 6] << 8) | (array[num4 + 5] << 16) | (array[num4 + 4] << 24));
				array2[j] = new DoubleUnion
				{
					ULongValue = (((ulong)num6 << 32) | num5)
				}.DoubleValue;
			}
		}
		return array2;
	}

	public static Guid[] ReadGuidArray(this NativeReader r, int count, Endian endian)
	{
		byte[] array = r.ReadBytes(count * 16);
		Guid[] array2 = new Guid[count];
		if (endian == Endian.Little)
		{
			for (int i = 0; i < count; i++)
			{
				int num = i * 16;
				array2[i] = new Guid(new byte[16]
				{
					array[num],
					array[num + 1],
					array[num + 2],
					array[num + 3],
					array[num + 4],
					array[num + 5],
					array[num + 6],
					array[num + 7],
					array[num + 8],
					array[num + 9],
					array[num + 10],
					array[num + 11],
					array[num + 12],
					array[num + 13],
					array[num + 14],
					array[num + 15]
				});
			}
		}
		else
		{
			for (int j = 0; j < count; j++)
			{
				int num2 = j * 16;
				array2[j] = new Guid(new byte[16]
				{
					array[num2 + 3],
					array[num2 + 2],
					array[num2 + 1],
					array[num2],
					array[num2 + 5],
					array[num2 + 4],
					array[num2 + 7],
					array[num2 + 6],
					array[num2 + 8],
					array[num2 + 9],
					array[num2 + 10],
					array[num2 + 11],
					array[num2 + 12],
					array[num2 + 13],
					array[num2 + 14],
					array[num2 + 15]
				});
			}
		}
		return array2;
	}
}
