using System;
using System.Collections.Generic;
using System.Text;
using AssetBankPlugin.Enums;
using AssetBankPlugin.Extensions;
using FrostySdk.IO;

namespace AssetBankPlugin.Formats.GenericData2;

public class SectionRef2 : Section
{
	private class RawLayout
	{
		public int MinSlot;

		public int MaxSlot;

		public uint DataSize;

		public uint Alignment;

		public uint Hash;

		public bool Reordered;

		public bool IsPod;

		public string Name;

		public RawEntry[] Entries;
	}

	private class RawEntry
	{
		public uint LayoutHash;

		public uint ElementSize;

		public uint Offset;

		public int NameIdx;

		public ushort Count;

		public ushort ElementAlign;

		public short RLE;

		public EFlags Flags;

		public long LayoutRef;

		public string Name;
	}

	private const bool VerboseLogging = false;

	public const string Identifier = "GD.REF2";

	public override Endian Endianness { get; set; }

	public override uint DataSize { get; set; }

	public override uint DataOffset { get; set; }

	public Dictionary<uint, GenericClass2> Classes { get; set; }

	public SectionRef2(NativeReader r, Endian bankEndian)
	{
		long position = r.BaseStream.Position;
		DataOffset = (uint)position;
		r.ReadSizedString(7);
		r.ReadByte();
		Endianness = bankEndian;
		DataSize = r.ReadUInt(Endianness);
		ulong num = r.ReadULong(Endianness);
		Classes = new Dictionary<uint, GenericClass2>((int)num);
		long[] array = new long[num];
		for (int i = 0; i < (int)num; i++)
		{
			array[i] = r.ReadReference(Endianness);
		}
		Dictionary<long, RawLayout> dictionary = new Dictionary<long, RawLayout>((int)num);
		for (int j = 0; j < (int)num; j++)
		{
			if (array[j] != 0L)
			{
				r.BaseStream.Position = array[j];
				dictionary[array[j]] = ReadRawLayout(r);
			}
		}
		foreach (RawLayout value4 in dictionary.Values)
		{
			GenericClass2 genericClass = new GenericClass2
			{
				Name = value4.Name,
				Size = (int)value4.DataSize,
				Alignment = (int)value4.Alignment
			};
			int num2 = value4.Entries.Length;
			if (num2 > 0)
			{
				genericClass.Elements.Capacity = num2;
			}
			RawEntry[] entries = value4.Entries;
			foreach (RawEntry rawEntry in entries)
			{
				if (rawEntry.LayoutHash != 0 && rawEntry.LayoutHash != uint.MaxValue)
				{
					GenericField2 genericField = new GenericField2
					{
						Name = rawEntry.Name,
						TypeHash = rawEntry.LayoutHash,
						Offset = (int)rawEntry.Offset,
						Size = rawEntry.ElementSize,
						Alignment = rawEntry.ElementAlign,
						IsList = ((rawEntry.Flags & EFlags.Array) != 0),
						InlineCount = rawEntry.Count
					};
					RawLayout value;
					if (PrimitiveTypeMap.IsPrimitive(rawEntry.LayoutHash))
					{
						genericField.Type = PrimitiveTypeMap.GetTypeName(rawEntry.LayoutHash);
						genericField.IsNative = true;
					}
					else if (rawEntry.LayoutRef != 0L && dictionary.TryGetValue(rawEntry.LayoutRef, out value))
					{
						genericField.Type = value.Name;
						genericField.IsNative = value.IsPod;
					}
					else
					{
						genericField.Type = $"Class_0x{rawEntry.LayoutHash:X8}";
					}
					genericClass.Elements.Add(genericField);
				}
			}
			if (value4.Reordered)
			{
				genericClass.Elements.Sort((GenericField2 a, GenericField2 b) => a.Offset.CompareTo(b.Offset));
			}
			Classes[value4.Hash] = genericClass;
		}
		foreach (GenericClass2 value5 in Classes.Values)
		{
			foreach (GenericField2 element in value5.Elements)
			{
				if (element.IsList)
				{
					if (!Classes.TryGetValue(element.TypeHash, out var value2))
					{
						if (PrimitiveTypeMap.IsPrimitive(element.TypeHash))
						{
							element.ElementTypeHash = element.TypeHash;
							element.ElementSize = (uint)PrimitiveTypeMap.GetTypeSize(element.TypeHash);
							element.ElementAlignment = Math.Min(element.ElementSize, 8u);
							element.Type = PrimitiveTypeMap.GetTypeName(element.TypeHash) + "[]";
						}
					}
					else
					{
						element.ElementTypeHash = element.TypeHash;
						element.ElementSize = (uint)value2.Size;
						element.ElementAlignment = (uint)value2.Alignment;
						element.Type = (PrimitiveTypeMap.IsPrimitive(element.ElementTypeHash) ? (PrimitiveTypeMap.GetTypeName(element.ElementTypeHash) + "[]") : (value2.Name + "[]"));
					}
				}
				else if (element.InlineCount > 1)
				{
					if (Classes.TryGetValue(element.TypeHash, out var value3))
					{
						element.ElementTypeHash = element.TypeHash;
						element.ElementSize = (uint)value3.Size;
						element.ElementAlignment = (uint)value3.Alignment;
					}
					else
					{
						element.ElementTypeHash = element.TypeHash;
						element.ElementSize = element.Size;
						element.ElementAlignment = element.Alignment;
					}
				}
			}
		}
		ResolveClassSizes();
		r.BaseStream.Position = position + DataSize;
	}

	private void ResolveClassSizes()
	{
		bool flag;
		do
		{
			flag = false;
			foreach (GenericClass2 value4 in Classes.Values)
			{
				foreach (GenericField2 element in value4.Elements)
				{
					if (element.Size == 0 && element.TypeHash != 0 && !PrimitiveTypeMap.IsPrimitive(element.TypeHash) && Classes.TryGetValue(element.TypeHash, out var value))
					{
						element.Size = (uint)value.Size;
						element.Alignment = (uint)value.Alignment;
						flag = true;
					}
					GenericClass2 value3;
					if (element.IsList)
					{
						if (element.ElementSize == 0 && element.ElementTypeHash != 0 && !PrimitiveTypeMap.IsPrimitive(element.ElementTypeHash) && Classes.TryGetValue(element.ElementTypeHash, out var value2))
						{
							element.ElementSize = (uint)value2.Size;
							element.ElementAlignment = (uint)value2.Alignment;
							flag = true;
						}
						if (element.Size == 0)
						{
							element.Size = 16u;
							element.Alignment = 8u;
							flag = true;
						}
					}
					else if (element.InlineCount > 1 && element.ElementSize == 0 && element.ElementTypeHash != 0 && !PrimitiveTypeMap.IsPrimitive(element.ElementTypeHash) && Classes.TryGetValue(element.ElementTypeHash, out value3))
					{
						element.ElementSize = (uint)value3.Size;
						element.ElementAlignment = (uint)value3.Alignment;
						flag = true;
					}
				}
			}
		}
		while (flag);
	}

	private RawLayout ReadRawLayout(NativeReader r)
	{
		RawLayout rawLayout = new RawLayout();
		rawLayout.MinSlot = r.ReadInt(Endianness);
		rawLayout.MaxSlot = r.ReadInt(Endianness);
		rawLayout.DataSize = r.ReadUInt(Endianness);
		rawLayout.Alignment = r.ReadUInt(Endianness);
		uint num = r.ReadUInt(Endianness);
		r.ReadUInt(Endianness);
		rawLayout.Reordered = r.ReadByte() != 0;
		rawLayout.IsPod = r.ReadByte() != 0;
		r.ReadUShort(Endianness);
		rawLayout.Hash = r.ReadUInt(Endianness);
		int num2 = rawLayout.MaxSlot - rawLayout.MinSlot + 1;
		rawLayout.Entries = new RawEntry[num2];
		for (int i = 0; i < num2; i++)
		{
			rawLayout.Entries[i] = new RawEntry
			{
				LayoutHash = r.ReadUInt(Endianness),
				ElementSize = r.ReadUInt(Endianness),
				Offset = r.ReadUInt(Endianness),
				NameIdx = r.ReadInt(Endianness),
				Count = r.ReadUShort(Endianness),
				Flags = (EFlags)r.ReadUShort(Endianness),
				ElementAlign = r.ReadUShort(Endianness),
				RLE = r.ReadShort(Endianness),
				LayoutRef = r.ReadReference(Endianness)
			};
		}
		if (num != 0)
		{
			byte[] array = r.ReadBytes((int)num);
			rawLayout.Name = GetString(array, 1);
			for (int j = 0; j < num2; j++)
			{
				int nameIdx = rawLayout.Entries[j].NameIdx;
				rawLayout.Entries[j].Name = ((nameIdx >= 0 && nameIdx < array.Length) ? GetString(array, nameIdx) : "unknown");
			}
		}
		return rawLayout;
	}

	private static string GetString(byte[] data, int idx)
	{
		int i = idx;
		for (int num = data.Length; i < num && data[i] != 0; i++)
		{
		}
		return Encoding.ASCII.GetString(data, idx, i - idx);
	}
}
