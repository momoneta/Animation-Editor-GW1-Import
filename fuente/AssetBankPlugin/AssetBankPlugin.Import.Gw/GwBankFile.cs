using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AssetBankPlugin.Formats.GenericData;

namespace AssetBankPlugin.Import.Gw;

public static class GwBankFile
{
	public struct Section
	{
		public int Start;

		public int Size;

		public string Tag;

		public bool BigEndian;

		public int DataStart => Start + 16;
	}

	public const int SectionHeaderSize = 16;

	public const int ObjectHeaderSize = 32;

	public const int MemoryBudgetOffset = 20;

	public static bool IsLegacy(byte[] bank)
	{
		foreach (Section item in Sections(bank))
		{
			if (item.Tag == "GD.DATA" || item.Tag == "GD.REFL")
			{
				return true;
			}
			if (item.Tag == "GD.DAT2" || item.Tag == "GD.REF2")
			{
				return false;
			}
		}
		return false;
	}

	public static int FindStream(byte[] bank)
	{
		if (bank.Length >= 8)
		{
			long num = (long)GwBinary.ReadU32(bank, 4, big: true) + 4L;
			if (num + 16 <= bank.Length && GwBinary.IsTag(bank, (int)num, "GD.STRM"))
			{
				return (int)num;
			}
		}
		for (int i = 0; i + 16 <= bank.Length; i++)
		{
			if (GwBinary.IsTag(bank, i, "GD.STRM"))
			{
				return i;
			}
		}
		return -1;
	}

	public static IEnumerable<Section> Sections(byte[] bank)
	{
		int num = FindStream(bank);
		if (num < 0)
		{
			yield break;
		}
		int size;
		for (int pos = num + 16; pos + 16 <= bank.Length && GwBinary.IsTag(bank, pos, "GD."); pos += size)
		{
			bool flag = bank[pos + 7] == 98;
			size = (int)GwBinary.ReadU32(bank, pos + 8, flag);
			if (size < 16 || pos + size > bank.Length)
			{
				break;
			}
			yield return new Section
			{
				Start = pos,
				Size = size,
				Tag = Encoding.ASCII.GetString(bank, pos, 7),
				BigEndian = flag
			};
		}
	}

	public static Section SectionAt(byte[] bank, int start)
	{
		foreach (Section item in Sections(bank))
		{
			if (item.Start == start)
			{
				return item;
			}
		}
		throw new InvalidOperationException($"There is no section at 0x{start:X} in the bank.");
	}

	public static uint ObjectType(byte[] bank, int objectStart, bool big)
	{
		return (uint)GwBinary.ReadU64(bank, objectStart + 16, big);
	}

	public static int ObjectFields(byte[] bank, int objectStart, bool big)
	{
		return objectStart + GwBinary.ReadU16(bank, objectStart + 28, big);
	}

	public static GenericClass RootClass(byte[] bank, Section s, Dictionary<uint, GenericClass> classes)
	{
		if (!classes.TryGetValue(ObjectType(bank, s.DataStart, s.BigEndian), out var value))
		{
			return null;
		}
		return value;
	}

	public static byte[] RootGuidBytes(byte[] bank, Section s, Dictionary<uint, GenericClass> classes)
	{
		GenericField genericField = RootClass(bank, s, classes)?.Elements.FirstOrDefault((GenericField f) => f.Name == "__guid") ?? default(GenericField);
		if (genericField.Name == null)
		{
			return null;
		}
		byte[] array = new byte[16];
		Buffer.BlockCopy(bank, ObjectFields(bank, s.DataStart, s.BigEndian) + genericField.Offset, array, 0, 16);
		if (!array.All((byte b) => b == 0))
		{
			return array;
		}
		return null;
	}

	public static int FindSection(byte[] bank, Dictionary<uint, GenericClass> classes, Guid id)
	{
		foreach (Section item in Sections(bank))
		{
			if (item.Tag != "GD.DATA")
			{
				continue;
			}
			byte[] array = RootGuidBytes(bank, item, classes);
			if (array != null)
			{
				byte[] array2 = new byte[16];
				GwBinary.WriteGuid(array2, 0, id, item.BigEndian);
				if (array.SequenceEqual(array2))
				{
					return item.Start;
				}
			}
		}
		return -1;
	}

	public static byte[] ReplaceSection(byte[] bank, int sectionStart, byte[] newSection, Dictionary<uint, GenericClass> classes)
	{
		Section section = SectionAt(bank, sectionStart);
		long num = RuntimeSize(bank, section.Start, classes);
		long num2 = RuntimeSize(newSection, 0, classes);
		byte[] array = new byte[bank.Length - section.Size + newSection.Length];
		Buffer.BlockCopy(bank, 0, array, 0, section.Start);
		Buffer.BlockCopy(newSection, 0, array, section.Start, newSection.Length);
		Buffer.BlockCopy(bank, section.Start + section.Size, array, section.Start + newSection.Length, bank.Length - section.Start - section.Size);
		int num3 = FindStream(array);
		if (num3 < 0)
		{
			throw new InvalidOperationException("The bank has no GD.STRM container.");
		}
		bool big = array[num3 + 7] == 98;
		GwBinary.WriteU32(array, num3 + 8, (uint)(array.Length - num3), big);
		uint num4 = 0u;
		foreach (Section item in Sections(array))
		{
			num4 = Math.Max(num4, (uint)item.Size);
		}
		GwBinary.WriteU32(array, num3 + 12, DecompressionBound(num4), big);
		if (num3 >= 24)
		{
			long val = GwBinary.ReadU32(array, 20, big: true) + num2 - num;
			GwBinary.WriteU32(array, 20, (uint)Math.Max(0L, val), big: true);
		}
		return array;
	}

	public static uint DecompressionBound(uint biggestSection)
	{
		return biggestSection + (biggestSection + 4) / 9 + 142;
	}

	public static long RuntimeSize(byte[] bytes, int sectionStart, Dictionary<uint, GenericClass> classes)
	{
		bool big = bytes[sectionStart + 7] == 98;
		int objectStart = sectionStart + 16;
		if (!classes.TryGetValue(ObjectType(bytes, objectStart, big), out var value) || value.Name != "VbrAnimationAsset")
		{
			return 0L;
		}
		int fields = ObjectFields(bytes, objectStart, big);
		long v = ArrayCount(bytes, fields, value, "Data", big);
		long num = ArrayCount(bytes, fields, value, "ConstantPalette", big);
		long num2 = ArrayCount(bytes, fields, value, "FrameBlockSizes", big);
		return 176 + Align16(Align16(v) + num * 4 + num2 * 2);
	}

	private static long ArrayCount(byte[] bytes, int fields, GenericClass cls, string name, bool big)
	{
		GenericField genericField = cls.Elements.FirstOrDefault((GenericField f) => f.Name == name);
		return (genericField.Name != null) ? GwBinary.ReadU32(bytes, fields + genericField.Offset + 4, big) : 0;
	}

	private static long Align16(long v)
	{
		return (v + 15) & -16;
	}
}
