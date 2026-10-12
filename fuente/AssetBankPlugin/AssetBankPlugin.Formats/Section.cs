using System;
using AssetBankPlugin.Formats.GenericData;
using AssetBankPlugin.Formats.GenericData2;
using FrostySdk.IO;

namespace AssetBankPlugin.Formats;

public abstract class Section
{
	public abstract Endian Endianness { get; set; }

	public abstract uint DataSize { get; set; }

	public abstract uint DataOffset { get; set; }

	public static Section ReadSection(NativeReader r)
	{
		while (r.BaseStream.Position + 12 <= r.BaseStream.Length)
		{
			long position = r.BaseStream.Position;
			string text = r.ReadSizedString(3);
			r.BaseStream.Position = position;
			if (text == "GD.")
			{
				string text2 = r.ReadSizedString(7);
				Endian endian = ((r.ReadByte() == 98) ? Endian.Big : Endian.Little);
				uint num = r.ReadUInt(endian);
				r.BaseStream.Position = position;
				switch (text2)
				{
				case "GD.STRM":
					return new SectionStrm(r, endian);
				case "GD.REFL":
					return new SectionRefl(r, endian);
				case "GD.DATA":
					return new SectionData(r, endian);
				case "GD.REF2":
					return new SectionRef2(r, endian);
				case "GD.DAT2":
					return new SectionData2(r, endian);
				}
				Console.WriteLine($"[DEBUG] Found unknown GD tag '{text2}' at 0x{position:X}. Skipping {num} bytes.");
				r.BaseStream.Position = position + num;
			}
			else
			{
				r.BaseStream.Position = position + 1;
			}
		}
		return null;
	}
}
