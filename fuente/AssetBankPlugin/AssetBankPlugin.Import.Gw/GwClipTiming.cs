using System;
using System.Collections.Generic;
using System.Linq;
using AssetBankPlugin.Formats.GenericData;

namespace AssetBankPlugin.Import.Gw;

public static class GwClipTiming
{
	public sealed class Report
	{
		public int Clips;

		public int Actors;

		public int SharedActors;
	}

	public static Report Retime(byte[] bank, Dictionary<uint, GenericClass> classes, int animSectionStart, int newKeys)
	{
		Report report = new Report();
		byte[] array = GwBankFile.RootGuidBytes(bank, GwBankFile.SectionAt(bank, animSectionStart), classes);
		if (array == null)
		{
			return report;
		}
		List<KeyValuePair<byte[], float>> list = new List<KeyValuePair<byte[], float>>();
		List<GwBankFile.Section> list2 = new List<GwBankFile.Section>();
		foreach (GwBankFile.Section item in GwBankFile.Sections(bank))
		{
			if (item.Tag != "GD.DATA")
			{
				continue;
			}
			GenericClass genericClass = GwBankFile.RootClass(bank, item, classes);
			if (genericClass == null)
			{
				continue;
			}
			if (genericClass.Name == "ActorControllerAsset")
			{
				list2.Add(item);
			}
			else
			{
				if (genericClass.Name != "ClipControllerAsset" && genericClass.Name != "ClipControllerData")
				{
					continue;
				}
				GenericField genericField = Field(genericClass, "NumTicks");
				GenericField genericField2 = Field(genericClass, "FPSScale", "TimeScale");
				GenericField anim = Field(genericClass, "Anim", "Anims");
				if (genericField.Name == null || genericField2.Name == null || anim.Name == null)
				{
					continue;
				}
				int num = GwBankFile.ObjectFields(bank, item.DataStart, item.BigEndian);
				if (!PlaysAnimation(bank, item, num, anim, array))
				{
					continue;
				}
				float num2 = GwBinary.ReadF32(bank, num + genericField2.Offset, item.BigEndian);
				if (!(num2 <= 0f))
				{
					float num3 = Math.Max(0f, (float)(newKeys - 1) / num2);
					GwBinary.WriteF32(bank, num + genericField.Offset, num3, item.BigEndian);
					report.Clips++;
					byte[] array2 = GwBankFile.RootGuidBytes(bank, item, classes);
					if (array2 != null)
					{
						list.Add(new KeyValuePair<byte[], float>(array2, num3));
					}
				}
			}
		}
		foreach (GwBankFile.Section item2 in list2)
		{
			GenericClass genericClass2 = GwBankFile.RootClass(bank, item2, classes);
			GenericField genericField3 = Field(genericClass2, "Length");
			if (genericField3.Name == null)
			{
				continue;
			}
			int num4 = GwBankFile.ObjectFields(bank, item2.DataStart, item2.BigEndian);
			int num5 = item2.Start + (int)GwBinary.ReadU32(bank, item2.Start + 12, item2.BigEndian);
			int num6 = 0;
			float v = 0f;
			foreach (KeyValuePair<byte[], float> item3 in list)
			{
				for (int i = num4 + genericClass2.Size; i + 16 <= num5; i++)
				{
					if (bank[i] == item3.Key[0] && GwBinary.SameBytes(bank, i, item3.Key))
					{
						num6++;
						v = item3.Value;
						break;
					}
				}
			}
			if (num6 != 0)
			{
				if (num6 > 1 || CountClipReferences(bank, item2, num4 + genericClass2.Size, num5, classes) > 1)
				{
					report.SharedActors++;
				}
				else if (GwBinary.ReadF32(bank, num4 + genericField3.Offset, item2.BigEndian) > 0f)
				{
					GwBinary.WriteF32(bank, num4 + genericField3.Offset, v, item2.BigEndian);
					report.Actors++;
				}
			}
		}
		return report;
	}

	private static bool PlaysAnimation(byte[] bank, GwBankFile.Section s, int fields, GenericField anim, byte[] animGuid)
	{
		if (!anim.IsArray)
		{
			return GwBinary.SameBytes(bank, fields + anim.Offset, animGuid);
		}
		int num = (int)GwBinary.ReadU32(bank, fields + anim.Offset + 4, s.BigEndian);
		int num2 = s.DataStart + (int)GwBinary.ReadU64(bank, fields + anim.Offset + 8, s.BigEndian);
		for (int i = 0; i < num; i++)
		{
			if (GwBinary.SameBytes(bank, num2 + i * 16, animGuid))
			{
				return true;
			}
		}
		return false;
	}

	private static int CountClipReferences(byte[] bank, GwBankFile.Section actor, int from, int to, Dictionary<uint, GenericClass> classes)
	{
		int num = 0;
		foreach (GwBankFile.Section item in GwBankFile.Sections(bank))
		{
			if (item.Tag != "GD.DATA")
			{
				continue;
			}
			GenericClass genericClass = GwBankFile.RootClass(bank, item, classes);
			if (genericClass == null || (genericClass.Name != "ClipControllerAsset" && genericClass.Name != "ClipControllerData"))
			{
				continue;
			}
			byte[] array = GwBankFile.RootGuidBytes(bank, item, classes);
			if (array == null)
			{
				continue;
			}
			for (int i = from; i + 16 <= to; i++)
			{
				if (bank[i] == array[0] && GwBinary.SameBytes(bank, i, array))
				{
					num++;
					break;
				}
			}
		}
		return num;
	}

	private static GenericField Field(GenericClass cls, params string[] names)
	{
		return cls.Elements.FirstOrDefault((GenericField f) => names.Contains(f.Name));
	}
}
