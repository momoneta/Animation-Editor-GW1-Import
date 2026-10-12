using System.Collections.Generic;
using AssetBankPlugin.Enums;
using AssetBankPlugin.Extensions;
using FrostySdk.IO;

namespace AssetBankPlugin.Formats.GenericData;

public class SectionRefl : Section
{
	public const string Identifier = "GD.REFL";

	public override Endian Endianness { get; set; }

	public override uint DataSize { get; set; }

	public override uint DataOffset { get; set; }

	public uint IndicesOffset { get; set; }

	public Dictionary<uint, GenericClass> Classes { get; set; } = new Dictionary<uint, GenericClass>();

	public SectionRefl(NativeReader r, Endian endian)
	{
		long position = r.BaseStream.Position;
		Endianness = endian;
		r.ReadSizedString(8);
		DataSize = r.ReadUInt(Endianness);
		IndicesOffset = r.ReadUInt(Endianness);
		DataOffset = (uint)r.BaseStream.Position;
		long num = r.ReadLong(endian);
		long[] array = r.ReadLongArray((int)num, endian);
		ReflLayout[] array2 = new ReflLayout[num];
		for (int i = 0; i < num; i++)
		{
			r.BaseStream.Position = array[i] + DataOffset;
			array2[i] = r.ReadReflLayoutEntry(endian, out var fieldSize);
			GenericClass genericClass = new GenericClass
			{
				Name = array2[i].mName,
				Alignment = (int)array2[i].mAlignment,
				Size = (int)array2[i].mDataSize
			};
			for (int j = 0; j < fieldSize; j++)
			{
				GenericField item = default(GenericField);
				long position2 = r.BaseStream.Position;
				r.BaseStream.Position = array[i] + DataOffset + array2[i].mStringTableOffset + array2[i].mEntries[j].mName;
				item.Name = r.ReadNullTerminatedString();
				r.BaseStream.Position = position2;
				item.Offset = (int)array2[i].mEntries[j].mOffset;
				item.IsArray = array2[i].mEntries[j].mFlags == EFlags.Array;
				long position3 = r.BaseStream.Position;
				if (array2[i].mEntries[j] != null)
				{
					r.BaseStream.Position = array2[i].mEntries[j].mLayout + DataOffset;
					int fieldSize2;
					ReflLayout reflLayout = r.ReadReflLayoutEntry(endian, out fieldSize2);
					item.Type = reflLayout.mName;
					item.TypeHash = reflLayout.mHash;
					item.IsNative = reflLayout.mNative;
					item.Size = reflLayout.mDataSize;
					item.Alignment = reflLayout.mAlignment;
					r.BaseStream.Position = position3;
					genericClass.Elements.Add(item);
				}
			}
			Classes.Add(array2[i].mHash, genericClass);
		}
		r.Pad(4);
		int count = r.ReadInt(endian);
		r.ReadIntArray(count, endian);
		r.BaseStream.Position = position + DataSize;
	}

	public override string ToString()
	{
		return $"GD.REFL [{Endianness}-Endian, Offset {DataOffset}, DataSize {DataSize}, Defines {Classes.Count} Classes]";
	}
}
