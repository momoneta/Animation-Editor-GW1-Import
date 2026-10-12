using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using FrostySdk.IO;

namespace AssetBankPlugin.Formats.GenericData2;

public class SectionData2 : Section
{
	[StructLayout(LayoutKind.Explicit)]
	private struct FloatUnion
	{
		[FieldOffset(0)]
		public uint UInt;

		[FieldOffset(0)]
		public float Float;
	}

	[StructLayout(LayoutKind.Explicit)]
	private struct DoubleUnion
	{
		[FieldOffset(0)]
		public ulong ULong;

		[FieldOffset(0)]
		public double Double;
	}

	public const string Identifier = "GD.DAT2";

	private readonly byte[] _patchedData;

	private readonly NativeReader _patchedReader;

	private readonly bool _isBigEndian;

	private readonly Dictionary<int, string> _stringCache = new Dictionary<int, string>();

	private readonly Dictionary<long, Dictionary<string, object>> _objectCache = new Dictionary<long, Dictionary<string, object>>();

	public override Endian Endianness { get; set; }

	public override uint DataSize { get; set; }

	public override uint DataOffset { get; set; }

	public uint ObjectCount { get; private set; }

	public void ClearCaches()
	{
		_stringCache.Clear();
		_objectCache.Clear();
	}

	public SectionData2(NativeReader r, Endian bankEndian)
	{
		long position = r.BaseStream.Position;
		DataOffset = (uint)position;
		r.ReadSizedString(7);
		r.ReadByte();
		Endianness = bankEndian;
		_isBigEndian = bankEndian == Endian.Big;
		DataSize = r.ReadUInt(Endianness);
		r.ReadLong(Endianness);
		ObjectCount = (uint)r.ReadULong(Endianness);
		r.BaseStream.Position = position;
		_patchedData = r.ReadBytes((int)DataSize);
		_patchedReader = new NativeReader(new MemoryStream(_patchedData), null);
		r.BaseStream.Position = position + DataSize;
	}

	public NativeReader GetPatchedReader()
	{
		return _patchedReader;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private uint R_U32(int o)
	{
		byte[] patchedData = _patchedData;
		if (!_isBigEndian)
		{
			return (uint)(patchedData[o] | (patchedData[o + 1] << 8) | (patchedData[o + 2] << 16) | (patchedData[o + 3] << 24));
		}
		return (uint)((patchedData[o] << 24) | (patchedData[o + 1] << 16) | (patchedData[o + 2] << 8) | patchedData[o + 3]);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int R_I32(int o)
	{
		byte[] patchedData = _patchedData;
		if (!_isBigEndian)
		{
			return patchedData[o] | (patchedData[o + 1] << 8) | (patchedData[o + 2] << 16) | (patchedData[o + 3] << 24);
		}
		return (patchedData[o] << 24) | (patchedData[o + 1] << 16) | (patchedData[o + 2] << 8) | patchedData[o + 3];
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private ulong R_U64(int o)
	{
		byte[] patchedData = _patchedData;
		if (!_isBigEndian)
		{
			return patchedData[o] | ((ulong)patchedData[o + 1] << 8) | ((ulong)patchedData[o + 2] << 16) | ((ulong)patchedData[o + 3] << 24) | ((ulong)patchedData[o + 4] << 32) | ((ulong)patchedData[o + 5] << 40) | ((ulong)patchedData[o + 6] << 48) | ((ulong)patchedData[o + 7] << 56);
		}
		return ((ulong)patchedData[o] << 56) | ((ulong)patchedData[o + 1] << 48) | ((ulong)patchedData[o + 2] << 40) | ((ulong)patchedData[o + 3] << 32) | ((ulong)patchedData[o + 4] << 24) | ((ulong)patchedData[o + 5] << 16) | ((ulong)patchedData[o + 6] << 8) | patchedData[o + 7];
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private long R_I64(int o)
	{
		byte[] patchedData = _patchedData;
		if (!_isBigEndian)
		{
			return (long)(patchedData[o] | ((ulong)patchedData[o + 1] << 8) | ((ulong)patchedData[o + 2] << 16) | ((ulong)patchedData[o + 3] << 24) | ((ulong)patchedData[o + 4] << 32) | ((ulong)patchedData[o + 5] << 40) | ((ulong)patchedData[o + 6] << 48) | ((ulong)patchedData[o + 7] << 56));
		}
		return (long)(((ulong)patchedData[o] << 56) | ((ulong)patchedData[o + 1] << 48) | ((ulong)patchedData[o + 2] << 40) | ((ulong)patchedData[o + 3] << 32) | ((ulong)patchedData[o + 4] << 24) | ((ulong)patchedData[o + 5] << 16) | ((ulong)patchedData[o + 6] << 8) | patchedData[o + 7]);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private ushort R_U16(int o)
	{
		byte[] patchedData = _patchedData;
		if (!_isBigEndian)
		{
			return (ushort)(patchedData[o] | (patchedData[o + 1] << 8));
		}
		return (ushort)((patchedData[o] << 8) | patchedData[o + 1]);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private short R_I16(int o)
	{
		byte[] patchedData = _patchedData;
		if (!_isBigEndian)
		{
			return (short)(patchedData[o] | (patchedData[o + 1] << 8));
		}
		return (short)((patchedData[o] << 8) | patchedData[o + 1]);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private float R_F32(int o)
	{
		FloatUnion floatUnion = new FloatUnion
		{
			UInt = R_U32(o)
		};
		return floatUnion.Float;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private double R_F64(int o)
	{
		DoubleUnion doubleUnion = new DoubleUnion
		{
			ULong = R_U64(o)
		};
		return doubleUnion.Double;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private Guid R_Guid(int o)
	{
		byte[] patchedData = _patchedData;
		return new Guid((int)R_U32(o), R_I16(o + 4), R_I16(o + 6), patchedData[o + 8], patchedData[o + 9], patchedData[o + 10], patchedData[o + 11], patchedData[o + 12], patchedData[o + 13], patchedData[o + 14], patchedData[o + 15]);
	}

	private string R_NullStr(int o)
	{
		if (o < 0 || o >= _patchedData.Length)
		{
			return "";
		}
		if (_stringCache.TryGetValue(o, out var value))
		{
			return value;
		}
		int i = o;
		for (int num = _patchedData.Length; i < num && _patchedData[i] != 0; i++)
		{
		}
		string text = Encoding.UTF8.GetString(_patchedData, o, i - o);
		_stringCache[o] = text;
		return text;
	}

	public Dictionary<string, object> ReadObjectAt(long headerFileOffset, Dictionary<uint, GenericClass2> classes)
	{
		if (_objectCache.TryGetValue(headerFileOffset, out var value))
		{
			return value;
		}
		int num = (int)(headerFileOffset - DataOffset);
		if (num < 0 || num + 16 > _patchedData.Length)
		{
			return null;
		}
		uint num2 = (uint)(R_U64(num) & 0xFFFFFFFFu);
		if (!classes.TryGetValue(num2, out var value2))
		{
			return null;
		}
		Dictionary<string, object> dictionary = ReadValues(num + 16, value2, classes);
		dictionary["__typeHash"] = num2;
		_objectCache[headerFileOffset] = dictionary;
		return dictionary;
	}

	private Dictionary<string, object> ReadValues(int bufOff, GenericClass2 layout, Dictionary<uint, GenericClass2> classes)
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>(layout.Elements.Count);
		foreach (GenericField2 element in layout.Elements)
		{
			int bufOff2 = bufOff + element.Offset;
			if (element.IsList || element.Type == "String")
			{
				dictionary[element.Name] = ReadHeapItem(bufOff2, element, classes);
			}
			else if (element.InlineCount > 1)
			{
				dictionary[element.Name] = ReadArray(bufOff2, element, element.InlineCount, classes);
			}
			else
			{
				dictionary[element.Name] = ReadPrimitive(bufOff2, element, classes);
			}
		}
		return dictionary;
	}

	private object ReadHeapItem(int bufOff, GenericField2 field, Dictionary<uint, GenericClass2> classes)
	{
		int num = R_I32(bufOff + 4);
		long num2 = R_I64(bufOff + 8);
		if (num2 == 0L || num2 == -1 || num <= 0)
		{
			if (!(field.Type == "String"))
			{
				return new object[0];
			}
			return "";
		}
		long num3 = num2 << 4 >> 4;
		long num4 = DataOffset + bufOff + 8 + num3;
		if (num4 < DataOffset || num4 > DataOffset + DataSize)
		{
			if (!(field.Type == "String"))
			{
				return new object[0];
			}
			return "";
		}
		int num5 = (int)(num4 - DataOffset);
		if (field.Type == "String")
		{
			return R_NullStr(num5);
		}
		if (field.ElementTypeHash != 0 && field.ElementSize != 0)
		{
			GenericField2 genericField = new GenericField2
			{
				TypeHash = field.ElementTypeHash,
				Size = field.ElementSize,
				Alignment = field.ElementAlignment
			};
			GenericClass2 value;
			if (PrimitiveTypeMap.IsPrimitive(genericField.TypeHash))
			{
				genericField.Type = PrimitiveTypeMap.GetTypeName(genericField.TypeHash);
			}
			else if (classes.TryGetValue(genericField.TypeHash, out value))
			{
				genericField.Type = value.Name;
			}
			else
			{
				genericField.Type = $"Class_0x{genericField.TypeHash:X8}";
			}
			return ReadArray(num5, genericField, num, classes);
		}
		return ReadArray(num5, field, num, classes);
	}

	private object ReadPrimitive(int bufOff, GenericField2 field, Dictionary<uint, GenericClass2> classes)
	{
		if (bufOff < 0 || bufOff >= _patchedData.Length)
		{
			return null;
		}
		switch (field.TypeHash)
		{
		case 1u:
			return _patchedData[bufOff] != 0;
		case 2u:
			return (sbyte)_patchedData[bufOff];
		case 3u:
			return _patchedData[bufOff];
		case 4u:
			return R_I16(bufOff);
		case 5u:
			return R_U16(bufOff);
		case 6u:
			return R_I32(bufOff);
		case 7u:
			return R_U32(bufOff);
		case 8u:
			return R_I64(bufOff);
		case 9u:
			return R_U64(bufOff);
		case 10u:
			return R_F32(bufOff);
		case 19u:
			return R_F64(bufOff);
		case 35u:
			return R_U64(bufOff).ToString("X16");
		case 11u:
			return new float[2]
			{
				R_F32(bufOff),
				R_F32(bufOff + 4)
			};
		case 12u:
			return new float[3]
			{
				R_F32(bufOff),
				R_F32(bufOff + 4),
				R_F32(bufOff + 8)
			};
		case 13u:
		case 14u:
			return new float[4]
			{
				R_F32(bufOff),
				R_F32(bufOff + 4),
				R_F32(bufOff + 8),
				R_F32(bufOff + 12)
			};
		case 15u:
		{
			float[] array = new float[16];
			for (int i = 0; i < 16; i++)
			{
				array[i] = R_F32(bufOff + i * 4);
			}
			return array;
		}
		case 18u:
		{
			long num7 = R_I64(bufOff);
			if (num7 == 0L || num7 == -1)
			{
				return null;
			}
			long num8 = DataOffset + bufOff;
			return ReadObjectAt(num8 + (num7 << 4 >> 4) + 16, classes);
		}
		default:
		{
			if (field.Type == "Guid")
			{
				return R_Guid(bufOff);
			}
			if (field.Size == 8 && field.Alignment == 8 && !field.IsNative)
			{
				long num = R_I64(bufOff);
				if (num == 0L || num == -1)
				{
					return null;
				}
				long num2 = DataOffset + bufOff + (num << 4 >> 4);
				if (field.Type == "String" || field.ElementTypeHash == 17)
				{
					int num3 = (int)(num2 - DataOffset);
					uint num4 = R_U32(num3 + 4);
					long num5 = R_I64(num3 + 8);
					if (num5 != 0L && num5 != -1 && num4 != 0)
					{
						long num6 = num2 + 8 + (num5 << 4 >> 4);
						return R_NullStr((int)(num6 - DataOffset));
					}
					return "";
				}
				return ReadObjectAt(num2 + 16, classes);
			}
			if (!classes.TryGetValue(field.TypeHash, out var value))
			{
				return null;
			}
			return ReadValues(bufOff, value, classes);
		}
		}
	}

	private object ReadArray(int bufOff, GenericField2 field, int count, Dictionary<uint, GenericClass2> classes)
	{
		if (count <= 0 || count > 5000000)
		{
			return new object[0];
		}
		bool flag = field.Size == 8 && field.Alignment == 8 && !PrimitiveTypeMap.IsPrimitive(field.TypeHash) && field.TypeHash != 0;
		bool flag2 = field.Type == "String[]" || (field.ElementTypeHash != 0 && field.ElementTypeHash == 17);
		uint num = field.Size;
		uint num2 = field.Alignment;
		if (num == 0 && field.TypeHash != 0 && !PrimitiveTypeMap.IsPrimitive(field.TypeHash) && classes.TryGetValue(field.TypeHash, out var value))
		{
			num = (uint)value.Size;
			num2 = (uint)value.Alignment;
		}
		if (num == 0)
		{
			num = 1u;
		}
		if (num2 == 0)
		{
			num2 = 1u;
		}
		uint num3 = (num + num2 - 1) & ~(num2 - 1);
		if (!flag && !flag2 && PrimitiveTypeMap.IsPrimitive(field.TypeHash))
		{
			bool flag3 = !_isBigEndian && num3 == num;
			int num4 = (int)num3;
			switch (field.TypeHash)
			{
			case 1u:
			{
				bool[] array9 = new bool[count];
				for (int num7 = 0; num7 < count; num7++)
				{
					array9[num7] = _patchedData[bufOff + num7 * num4] != 0;
				}
				return array9;
			}
			case 2u:
			{
				sbyte[] array7 = new sbyte[count];
				if (flag3)
				{
					Buffer.BlockCopy(_patchedData, bufOff, array7, 0, count);
				}
				else
				{
					for (int num5 = 0; num5 < count; num5++)
					{
						array7[num5] = (sbyte)_patchedData[bufOff + num5 * num4];
					}
				}
				return array7;
			}
			case 3u:
			{
				byte[] array3 = new byte[count];
				if (flag3)
				{
					Buffer.BlockCopy(_patchedData, bufOff, array3, 0, count);
				}
				else
				{
					for (int k = 0; k < count; k++)
					{
						array3[k] = _patchedData[bufOff + k * num4];
					}
				}
				return array3;
			}
			case 4u:
			{
				short[] array2 = new short[count];
				if (flag3)
				{
					Buffer.BlockCopy(_patchedData, bufOff, array2, 0, count * 2);
				}
				else
				{
					for (int j = 0; j < count; j++)
					{
						array2[j] = R_I16(bufOff + j * num4);
					}
				}
				return array2;
			}
			case 5u:
			{
				ushort[] array4 = new ushort[count];
				if (flag3)
				{
					Buffer.BlockCopy(_patchedData, bufOff, array4, 0, count * 2);
				}
				else
				{
					for (int l = 0; l < count; l++)
					{
						array4[l] = R_U16(bufOff + l * num4);
					}
				}
				return array4;
			}
			case 6u:
			{
				int[] array8 = new int[count];
				if (flag3)
				{
					Buffer.BlockCopy(_patchedData, bufOff, array8, 0, count * 4);
				}
				else
				{
					for (int num6 = 0; num6 < count; num6++)
					{
						array8[num6] = R_I32(bufOff + num6 * num4);
					}
				}
				return array8;
			}
			case 7u:
			{
				uint[] array5 = new uint[count];
				if (flag3)
				{
					Buffer.BlockCopy(_patchedData, bufOff, array5, 0, count * 4);
				}
				else
				{
					for (int m = 0; m < count; m++)
					{
						array5[m] = R_U32(bufOff + m * num4);
					}
				}
				return array5;
			}
			case 8u:
			{
				long[] array10 = new long[count];
				if (flag3)
				{
					Buffer.BlockCopy(_patchedData, bufOff, array10, 0, count * 8);
				}
				else
				{
					for (int num8 = 0; num8 < count; num8++)
					{
						array10[num8] = R_I64(bufOff + num8 * num4);
					}
				}
				return array10;
			}
			case 9u:
			{
				ulong[] array11 = new ulong[count];
				if (flag3)
				{
					Buffer.BlockCopy(_patchedData, bufOff, array11, 0, count * 8);
				}
				else
				{
					for (int num9 = 0; num9 < count; num9++)
					{
						array11[num9] = R_U64(bufOff + num9 * num4);
					}
				}
				return array11;
			}
			case 10u:
			{
				float[] array6 = new float[count];
				if (flag3)
				{
					Buffer.BlockCopy(_patchedData, bufOff, array6, 0, count * 4);
				}
				else
				{
					for (int n = 0; n < count; n++)
					{
						array6[n] = R_F32(bufOff + n * num4);
					}
				}
				return array6;
			}
			case 19u:
			{
				double[] array = new double[count];
				if (flag3)
				{
					Buffer.BlockCopy(_patchedData, bufOff, array, 0, count * 8);
				}
				else
				{
					for (int i = 0; i < count; i++)
					{
						array[i] = R_F64(bufOff + i * num4);
					}
				}
				return array;
			}
			}
		}
		if (flag2)
		{
			string[] array12 = new string[count];
			for (int num10 = 0; num10 < count; num10++)
			{
				int num11 = (int)(num10 * num3);
				long num12 = R_I64(bufOff + num11);
				if (num12 == 0L || num12 == -1)
				{
					array12[num10] = "";
					continue;
				}
				long num13 = DataOffset + bufOff + num11 + (num12 << 4 >> 4);
				int num14 = (int)(num13 - DataOffset);
				uint num15 = R_U32(num14 + 4);
				long num16 = R_I64(num14 + 8);
				if (num16 != 0L && num16 != -1 && num15 != 0)
				{
					long num17 = num13 + 8 + (num16 << 4 >> 4);
					array12[num10] = R_NullStr((int)(num17 - DataOffset));
				}
				else
				{
					array12[num10] = "";
				}
			}
			return array12;
		}
		object[] array13 = new object[count];
		for (int num18 = 0; num18 < count; num18++)
		{
			int num19 = (int)(num18 * num3);
			if (flag)
			{
				long num20 = R_I64(bufOff + num19);
				if (num20 == 0L || num20 == -1)
				{
					array13[num18] = null;
					continue;
				}
				long num21 = DataOffset + bufOff + num19 + (num20 << 4 >> 4);
				array13[num18] = ReadObjectAt(num21 + 16, classes);
			}
			else
			{
				array13[num18] = ReadPrimitive(bufOff + num19, field, classes);
			}
		}
		return array13;
	}
}
