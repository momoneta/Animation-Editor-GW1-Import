using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using AssetBankPlugin.Ant;
using AssetBankPlugin.Ant.VBR;
using AssetBankPlugin.Formats.GenericData;
using Assimp;
using FrostySdk.IO;

namespace AssetBankPlugin.Import.Gw;

public static class GwAnimationImporter
{
	public sealed class Result
	{
		public byte[] Bank;

		public VbrAnimationAsset Asset;

		public int OldKeys;

		public int NewKeys;

		public GwClipTiming.Report Timing;

		public int OldSectionSize;

		public int NewSectionSize;

		public List<string> Notes = new List<string>();
	}

	private static readonly string[] CodecFields = new string[29]
	{
		"QuaternionCount", "Vector3Count", "FloatCount", "NumFloat", "ConstQuaternionCount", "ConstVector3Count", "ConstFloatCount", "NumKeys", "EndFrame", "KeyTimeSize",
		"ConstChanMapSize", "ConstPaletteSize", "VectorOffsetSize", "FloatOffsetSize", "Flags", "QuatMin", "QuatMax", "TrajMin", "TrajMax", "Vec3Min",
		"Vec3Max", "FloatMin", "FloatMax", "Dct", "VectorOffsetScale", "FloatOffsetScale", "ConstantPalette", "FrameBlockSizes", "Data"
	};

	private const ushort CodecFlags = 143;

	private const ushort FastDecoderFlag = 4;

	public static Result Import(Scene scene, byte[] bank, VbrAnimationAsset template, Dictionary<uint, GenericClass> classes, float maxErrorPct)
	{
		if (scene == null)
		{
			throw new ArgumentNullException("scene");
		}
		if (!scene.HasAnimations)
		{
			throw new InvalidOperationException("The imported file contains no animations.");
		}
		GwBankFile.Section section = Locate(bank, template, classes);
		CheckBones(scene, template);
		return Finish(VbrCompressor.Compress(scene, template, section.BigEndian, maxErrorPct, maxErrorPct, maxErrorPct, 1E-05f, linearSearchTables: true, curveFit: true, legacy: true), bank, section, template, classes);
	}

	/// <summary>
	/// The file is matched to the animation by bone name. If the bones of the animation are not
	/// known, or none of them is in the file, the compressor would quietly write every bone as a
	/// constant and the character would freeze in game: refuse instead.
	/// </summary>
	private static void CheckBones(Scene scene, VbrAnimationAsset template)
	{
		List<ChannelInfo> orderedChannels = template.OrderedChannels;
		if (orderedChannels == null || orderedChannels.Count == 0)
		{
			throw new InvalidOperationException("The bones of '" + template.Name + "' cannot be resolved: its rig and layouts are in another bank of the level that is not loaded. Nothing was changed.");
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (NodeAnimationChannel nodeAnimationChannel in scene.Animations[0].NodeAnimationChannels)
		{
			hashSet.Add(nodeAnimationChannel.NodeName);
		}
		List<string> list = new List<string>();
		int num = 0;
		foreach (ChannelInfo item in orderedChannels)
		{
			string text = (item.Name ?? "").Replace(".q", "").Replace(".t", "").Replace(".s", "").Trim();
			if (text.Length == 0)
			{
				continue;
			}
			if (hashSet.Contains(text))
			{
				num++;
			}
			else if (list.Count < 4 && !list.Contains(text))
			{
				list.Add(text);
			}
		}
		if (num == 0)
		{
			throw new InvalidOperationException("None of the bones of '" + template.Name + "' (" + string.Join(", ", list) + "...) is animated in the imported file, which has " + hashSet.Count + " animated nodes (" + string.Join(", ", hashSet.Take(4)) + "...). Is it the skeleton of another character? Nothing was changed.");
		}
	}

	public static Result Import(float[][][] quats, float[][][] vecs, float[][] floats, int frameCount, byte[] bank, VbrAnimationAsset template, Dictionary<uint, GenericClass> classes, float maxErrorPct)
	{
		GwBankFile.Section section = Locate(bank, template, classes);
		return Finish(VbrCompressor.CompressFromData(quats, vecs, floats, frameCount, (template.Flags & 1) != 0, template, maxErrorPct, maxErrorPct, maxErrorPct, 1E-05f, section.BigEndian, linearSearchTables: true, curveFit: true, legacy: true), bank, section, template, classes);
	}

	private static GwBankFile.Section Locate(byte[] bank, VbrAnimationAsset template, Dictionary<uint, GenericClass> classes)
	{
		if (bank == null || bank.Length == 0)
		{
			throw new ArgumentNullException("bank");
		}
		if (template == null)
		{
			throw new ArgumentNullException("template");
		}
		if (classes == null)
		{
			throw new ArgumentNullException("classes");
		}
		int num = GwBankFile.FindSection(bank, classes, template.ID);
		if (num < 0)
		{
			throw new InvalidOperationException($"'{template.Name}' ({template.ID}) is not in this bank.");
		}
		GwBankFile.Section section = GwBankFile.SectionAt(bank, num);
		GenericClass genericClass = GwBankFile.RootClass(bank, section, classes);
		if (genericClass?.Name != "VbrAnimationAsset")
		{
			throw new NotSupportedException("'" + template.Name + "' is a " + (genericClass?.Name ?? "unknown type") + "; only VbrAnimationAsset can be imported.");
		}
		return section;
	}

	private static Result Finish(VbrAnimationAsset compressed, byte[] bank, GwBankFile.Section section, VbrAnimationAsset template, Dictionary<uint, GenericClass> classes)
	{
		Result result = new Result
		{
			OldSectionSize = section.Size
		};
		Dictionary<string, object> dictionary = ReadSection(bank, section.Start, classes);
		result.OldKeys = Convert.ToInt32(dictionary.TryGetValue("NumKeys", out var value) ? value : ((object)template.NumKeys));
		object value2;
		ushort num = Convert.ToUInt16(dictionary.TryGetValue("Flags", out value2) ? value2 : ((object)template.Flags));
		compressed.Name = template.Name;
		compressed.ID = template.ID;
		compressed.Flags = (ushort)((compressed.Flags & 0x8F) | (num & -144));
		if ((compressed.Flags & 4) == 0)
		{
			result.Notes.Add("the animation needs the generic bit decoder; every animation shipped with GW uses the 128 bit one.");
		}
		if (compressed.QuaternionCount + compressed.Vector3Count + compressed.NumFloat == 0)
		{
			AddEmptyFrameBlocks(compressed);
		}
		Dictionary<string, object> dictionary2 = new Dictionary<string, object>();
		string[] codecFields = CodecFields;
		foreach (string key in codecFields)
		{
			if (compressed.RawData != null && compressed.RawData.TryGetValue(key, out var value3) && value3 != null)
			{
				dictionary2[key] = value3;
			}
		}
		dictionary2["Flags"] = compressed.Flags;
		dictionary2["Data"] = compressed.Data;
		dictionary2["FrameBlockSizes"] = compressed.FrameBlockSizes;
		dictionary2["ConstantPalette"] = compressed.ConstantPalette;
		dictionary2["NumKeys"] = compressed.NumKeys;
		dictionary2["EndFrame"] = compressed.EndFrame;
		CheckExactRewrite(bank, section, classes, result);
		GwSectionWriter.Result result2 = GwSectionWriter.Write(bank, section, classes, dictionary2);
		foreach (string item in result2.UnusedUpdates.Where((string n) => n != "FloatCount" && n != "NumFloat"))
		{
			result.Notes.Add("'" + item + "' has no field in the GW class; not written.");
		}
		if (!result2.Written.Contains("FloatCount") && !result2.Written.Contains("NumFloat") && compressed.NumFloat > 0)
		{
			throw new InvalidOperationException("The animation has float channels but the GW class has no field for their count.");
		}
		result.NewSectionSize = result2.Section.Length;
		Verify(result2.Section, classes, compressed, dictionary);
		byte[] bank2 = GwBankFile.ReplaceSection(bank, section.Start, result2.Section, classes);
		result.Timing = GwClipTiming.Retime(bank2, classes, section.Start, compressed.NumKeys);
		if (GwBankFile.FindSection(bank2, classes, template.ID) != section.Start)
		{
			throw new InvalidOperationException("The new section cannot be found again in the rebuilt bank.");
		}
		if (GwBankFile.SectionAt(bank2, section.Start).Size != result2.Section.Length)
		{
			throw new InvalidOperationException("The rebuilt bank does not hold the new section as written.");
		}
		result.Bank = bank2;
		result.Asset = compressed;
		result.NewKeys = compressed.NumKeys;
		return result;
	}

	private static void AddEmptyFrameBlocks(VbrAnimationAsset asset)
	{
		int num = asset.NumKeys + 7 >> 3;
		byte[] array = new byte[asset.Data.Length + num * 3];
		Buffer.BlockCopy(asset.Data, 0, array, 0, asset.Data.Length);
		for (int i = asset.Data.Length; i < array.Length; i++)
		{
			array[i] = byte.MaxValue;
		}
		asset.Data = array;
		asset.FrameBlockSizes = Enumerable.Repeat((ushort)3, num).ToArray();
		asset.FrameBlockSize = num;
		asset.Dct = float.MaxValue;
		asset.Flags |= 4;
		if (asset.RawData != null)
		{
			asset.RawData["Data"] = asset.Data;
			asset.RawData["FrameBlockSizes"] = asset.FrameBlockSizes;
			asset.RawData["Dct"] = asset.Dct;
			asset.RawData["Flags"] = asset.Flags;
		}
	}

	private static void CheckExactRewrite(byte[] bank, GwBankFile.Section section, Dictionary<uint, GenericClass> classes, Result result)
	{
		byte[] section2 = GwSectionWriter.Write(bank, section, classes, null).Section;
		if (section2.Length != section.Size)
		{
			result.Notes.Add($"rewriting the untouched section gives {section2.Length} bytes instead of {section.Size}.");
			return;
		}
		for (int i = 0; i < section2.Length; i++)
		{
			if (section2[i] != bank[section.Start + i])
			{
				result.Notes.Add($"rewriting the untouched section differs at byte 0x{i:X} of {section.Size}.");
				break;
			}
		}
	}

	private static void Verify(byte[] section, Dictionary<uint, GenericClass> classes, VbrAnimationAsset expected, Dictionary<string, object> original)
	{
		Dictionary<string, object> dictionary = ReadSection(section, 0, classes);
		VbrAnimationAsset vbrAnimationAsset = new VbrAnimationAsset();
		vbrAnimationAsset.SetData(dictionary);
		Check(Same(dictionary, original, "__guid"), "the asset id changed");
		Check(Same(dictionary, original, "__name"), "the asset name changed");
		Check(vbrAnimationAsset.NumKeys == expected.NumKeys, $"NumKeys {vbrAnimationAsset.NumKeys} != {expected.NumKeys}");
		Check(vbrAnimationAsset.QuaternionCount == expected.QuaternionCount, "QuaternionCount differs");
		Check(vbrAnimationAsset.Vector3Count == expected.Vector3Count, "Vector3Count differs");
		Check(vbrAnimationAsset.NumFloat == expected.NumFloat, "float channel count differs");
		Check(vbrAnimationAsset.ConstQuaternionCount == expected.ConstQuaternionCount, "ConstQuaternionCount differs");
		Check(vbrAnimationAsset.ConstVector3Count == expected.ConstVector3Count, "ConstVector3Count differs");
		Check(vbrAnimationAsset.ConstFloatCount == expected.ConstFloatCount, "ConstFloatCount differs");
		Check(vbrAnimationAsset.Flags == expected.Flags, $"Flags 0x{vbrAnimationAsset.Flags:X} != 0x{expected.Flags:X}");
		Check(vbrAnimationAsset.Data.SequenceEqual(expected.Data), "Data differs");
		Check((vbrAnimationAsset.FrameBlockSizes ?? new ushort[0]).SequenceEqual(expected.FrameBlockSizes ?? new ushort[0]), "FrameBlockSizes differs");
		Check((vbrAnimationAsset.ConstantPalette ?? new float[0]).SequenceEqual(expected.ConstantPalette ?? new float[0]), "ConstantPalette differs");
		if (expected.QuaternionCount + expected.Vector3Count + expected.NumFloat + expected.ConstQuaternionCount + expected.ConstVector3Count + expected.ConstFloatCount != 0)
		{
			List<Vector4> list = vbrAnimationAsset.Decompress();
			List<Vector4> list2 = Clone(expected).Decompress();
			Check(list.Count == list2.Count, $"decoded {list.Count} values instead of {list2.Count}");
			for (int i = 0; i < list.Count; i++)
			{
				Check(list[i] == list2[i], $"decoded value {i} differs");
			}
		}
	}

	private static VbrAnimationAsset Clone(VbrAnimationAsset asset)
	{
		VbrAnimationAsset vbrAnimationAsset = new VbrAnimationAsset();
		vbrAnimationAsset.SetData(new Dictionary<string, object>(asset.RawData)
		{
			["Data"] = asset.Data,
			["FrameBlockSizes"] = asset.FrameBlockSizes,
			["ConstantPalette"] = asset.ConstantPalette,
			["Flags"] = asset.Flags,
			["NumKeys"] = asset.NumKeys,
			["Dct"] = asset.Dct
		});
		return vbrAnimationAsset;
	}

	private static bool Same(Dictionary<string, object> a, Dictionary<string, object> b, string key)
	{
		a.TryGetValue(key, out var value);
		b.TryGetValue(key, out var value2);
		return object.Equals(value?.ToString(), value2?.ToString());
	}

	private static void Check(bool ok, string what)
	{
		if (!ok)
		{
			throw new InvalidOperationException("The written section does not read back as imported: " + what + ". Nothing was changed.");
		}
	}

	public static Dictionary<string, object> ReadSection(byte[] bytes, int sectionStart, Dictionary<uint, GenericClass> classes)
	{
		bool flag = bytes[sectionStart + 7] == 98;
		Endian endian = (flag ? Endian.Big : Endian.Little);
		using NativeReader nativeReader = new NativeReader(new MemoryStream(bytes));
		nativeReader.BaseStream.Position = sectionStart;
		SectionData sectionData = new SectionData(nativeReader, endian);
		int dataOffset = (int)sectionData.DataOffset;
		uint type = GwBankFile.ObjectType(bytes, dataOffset, flag);
		int num = GwBinary.ReadU16(bytes, dataOffset + 28, flag);
		Dictionary<string, object> dictionary = sectionData.ReadValues(nativeReader, classes, (uint)(dataOffset + num), type);
		if (dictionary.TryGetValue("__base", out var value) && value is long num2 && num2 != 0L)
		{
			int num3 = dataOffset + (int)num2;
			uint type2 = GwBankFile.ObjectType(bytes, num3, flag);
			int num4 = GwBinary.ReadU16(bytes, num3 + 28, flag);
			foreach (KeyValuePair<string, object> item in sectionData.ReadValues(nativeReader, classes, (uint)(num3 + num4), type2))
			{
				if (!dictionary.ContainsKey(item.Key))
				{
					dictionary[item.Key] = item.Value;
				}
			}
		}
		return dictionary;
	}
}
