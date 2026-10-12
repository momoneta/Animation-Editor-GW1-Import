using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AssetBankPlugin.Extensions;
using FrostySdk.IO;

namespace AssetBankPlugin.Formats.GenericData;

public class SectionData : Section
{
	public const string Identifier = "GD.DATA";

	public override Endian Endianness { get; set; }

	public override uint DataSize { get; set; }

	public override uint DataOffset { get; set; }

	public uint IndicesOffset { get; set; }

	public SectionData(NativeReader r, Endian endian)
	{
		Endianness = endian;
		r.ReadSizedString(8);
		DataSize = r.ReadUInt(Endianness);
		IndicesOffset = r.ReadUInt(Endianness);
		DataOffset = (uint)r.BaseStream.Position;
		IndicesOffset += DataOffset;
		r.BaseStream.Position += DataSize - 16;
	}

	public Dictionary<string, object> ReadValues(NativeReader r, Dictionary<uint, GenericClass> classes, uint baseOffset, uint type)
	{
		GenericClass genericClass = classes[type];
		int count = genericClass.Elements.Count;
		Dictionary<string, object> dictionary = new Dictionary<string, object>(count);
		for (int i = 0; i < count; i++)
		{
			GenericField genericField = genericClass.Elements[i];
			object value = null;
			SeekTo(r, baseOffset + genericField.Offset);
			switch (genericField.Type)
			{
			case "Bool":
				if (!genericField.IsArray)
				{
					value = r.ReadBoolean();
				}
				break;
			case "UInt8":
			{
				if (!genericField.IsArray)
				{
					value = r.ReadByte();
					break;
				}
				ReadArrayHeader(r, out var size12, out var dataPosition12);
				SeekTo(r, dataPosition12);
				value = r.ReadByteArray((int)size12);
				break;
			}
			case "Int8":
			{
				if (!genericField.IsArray)
				{
					value = r.ReadSByte();
					break;
				}
				ReadArrayHeader(r, out var size4, out var dataPosition4);
				SeekTo(r, dataPosition4);
				value = r.ReadSByteArray((int)size4);
				break;
			}
			case "Int16":
			{
				if (!genericField.IsArray)
				{
					value = r.ReadShort(Endianness);
					break;
				}
				ReadArrayHeader(r, out var size9, out var dataPosition9);
				SeekTo(r, dataPosition9);
				value = r.ReadShortArray((int)size9, Endianness);
				break;
			}
			case "UInt16":
			{
				if (!genericField.IsArray)
				{
					value = r.ReadUShort(Endianness);
					break;
				}
				ReadArrayHeader(r, out var size14, out var dataPosition14);
				SeekTo(r, dataPosition14);
				value = r.ReadUShortArray((int)size14, Endianness);
				break;
			}
			case "Int32":
			{
				if (!genericField.IsArray)
				{
					value = r.ReadInt(Endianness);
					break;
				}
				ReadArrayHeader(r, out var size7, out var dataPosition7);
				SeekTo(r, dataPosition7);
				value = r.ReadIntArray((int)size7, Endianness);
				break;
			}
			case "UInt32":
			{
				if (!genericField.IsArray)
				{
					value = r.ReadInt(Endianness);
					break;
				}
				ReadArrayHeader(r, out var size2, out var dataPosition2);
				SeekTo(r, dataPosition2);
				value = r.ReadUIntArray((int)size2, Endianness);
				break;
			}
			case "Int64":
			{
				if (!genericField.IsArray)
				{
					value = r.ReadLong(Endianness);
					break;
				}
				ReadArrayHeader(r, out var size11, out var dataPosition11);
				SeekTo(r, dataPosition11);
				value = r.ReadLongArray((int)size11, Endianness);
				break;
			}
			case "UInt64":
			{
				if (!genericField.IsArray)
				{
					value = r.ReadLong(Endianness);
					break;
				}
				ReadArrayHeader(r, out var size6, out var dataPosition6);
				SeekTo(r, dataPosition6);
				value = r.ReadULongArray((int)size6, Endianness);
				break;
			}
			case "Float":
			{
				if (!genericField.IsArray)
				{
					value = r.ReadFloat(Endianness);
					break;
				}
				ReadArrayHeader(r, out var size3, out var dataPosition3);
				SeekTo(r, dataPosition3);
				value = r.ReadSingleArray((int)size3, Endianness);
				break;
			}
			case "Double":
			{
				if (!genericField.IsArray)
				{
					value = r.ReadDouble(Endianness);
					break;
				}
				ReadArrayHeader(r, out var size13, out var dataPosition13);
				SeekTo(r, dataPosition13);
				value = r.ReadDoubleArray((int)size13, Endianness);
				break;
			}
			case "DataRef":
			{
				if (!genericField.IsArray)
				{
					value = r.ReadLong(Endianness);
					break;
				}
				ReadArrayHeader(r, out var size10, out var dataPosition10);
				SeekTo(r, dataPosition10);
				value = r.ReadGuidArray((int)size10, Endianness);
				break;
			}
			case "String":
				if (!genericField.IsArray)
				{
					ReadArrayHeader(r, out var size8, out var dataPosition8);
					SeekTo(r, dataPosition8);
					value = r.ReadSizedString((int)size8);
				}
				break;
			case "Guid":
			{
				if (!genericField.IsArray)
				{
					value = r.ReadGuid(Endianness);
					break;
				}
				ReadArrayHeader(r, out var size5, out var dataPosition5);
				SeekTo(r, dataPosition5);
				value = r.ReadGuidArray((int)size5, Endianness);
				break;
			}
			default:
			{
				if (!genericField.IsArray)
				{
					value = ReadValues(r, classes, (uint)(genericField.Offset + baseOffset), genericField.TypeHash);
					break;
				}
				ReadArrayHeader(r, out var size, out var dataPosition);
				Dictionary<string, object>[] array = new Dictionary<string, object>[size];
				uint alignedSize = GetAlignedSize(genericField.Size, genericField.Alignment);
				for (uint num = 0u; num < size; num++)
				{
					array[num] = ReadValues(r, classes, (uint)(dataPosition + alignedSize * num), genericField.TypeHash);
				}
				value = array;
				break;
			}
			}
			dictionary[genericField.Name] = value;
		}
		return dictionary;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ReadArrayHeader(NativeReader r, out uint size, out long dataPosition)
	{
		r.ReadUInt(Endianness);
		size = r.ReadUInt(Endianness);
		dataPosition = DataOffset + r.ReadLong(Endianness);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void SeekTo(NativeReader r, long position)
	{
		if (r.BaseStream.Position != position)
		{
			r.BaseStream.Position = position;
		}
	}

	private static uint GetAlignedSize(uint size, uint alignBy)
	{
		if (size % alignBy != 0)
		{
			size += alignBy - size % alignBy;
		}
		return size;
	}

	public override string ToString()
	{
		return $"GD.DATA [{Endianness}-Endian, Offset {DataOffset}, DataSize {DataSize}]";
	}
}
