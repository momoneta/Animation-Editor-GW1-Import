using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using AssetBankPlugin.Ant;
using AssetBankPlugin.Ant.VBR;
using AssetBankPlugin.Formats.GenericData;
using AssetBankPlugin.Formats.GenericData2;
using AssetBankPlugin.Import.Gw;
using Assimp;
using Frosty.Core;

namespace AssetBankPlugin.Import;

public static class AnimationImporter
{
	public static byte[] Import(Scene scene, byte[] bankBytes, VbrAnimationAsset template, Dictionary<uint, GenericClass2> classes, bool bigEndian, float maxRotErrPct = 0f, float maxTransErrPct = 0f, float maxTrajErrPct = 0f)
	{
		if (scene == null)
		{
			throw new ArgumentNullException("scene");
		}
		if (bankBytes == null || bankBytes.Length == 0)
		{
			throw new ArgumentNullException("bankBytes");
		}
		if (template == null)
		{
			throw new ArgumentNullException("template");
		}
		if (classes == null)
		{
			throw new ArgumentNullException("classes");
		}
		if (!scene.HasAnimations)
		{
			throw new InvalidOperationException("The imported file contains no animations.");
		}
		float num = Config.Get("AnimationImportQuality", 1f, ConfigScope.Game);
		float num2 = Math.Max(0f, Math.Min(1f, 1f - num));
		return ProcessSplicing(VbrCompressor.Compress(scene, template, bigEndian, num2, num2, num2), bankBytes, template, classes, bigEndian);
	}

	public static byte[] ImportLegacy(Scene scene, byte[] bankBytes, VbrAnimationAsset template, Dictionary<uint, GenericClass> classes)
	{
		float num = Config.Get("AnimationImportQuality", 1f, ConfigScope.Game);
		return ImportLegacy(scene, bankBytes, template, classes, Math.Max(0f, Math.Min(1f, 1f - num)));
	}

	public static byte[] ImportLegacy(Scene scene, byte[] bankBytes, VbrAnimationAsset template, Dictionary<uint, GenericClass> classes, float maxErrorPct)
	{
		return FinishLegacy(GwAnimationImporter.Import(scene, bankBytes, template, classes, maxErrorPct), template);
	}

	public static byte[] ImportLegacy(float[][][] quats, float[][][] vecs, float[][] floats, int frameCount, byte[] bankBytes, VbrAnimationAsset template, Dictionary<uint, GenericClass> classes, float maxErrorPct = 0f)
	{
		return FinishLegacy(GwAnimationImporter.Import(quats, vecs, floats, frameCount, bankBytes, template, classes, maxErrorPct), template);
	}

	private static byte[] FinishLegacy(GwAnimationImporter.Result result, VbrAnimationAsset template)
	{
		VbrAnimationAsset asset = result.Asset;
		foreach (ClipControllerAsset item in AntRefTable.Refs.Values.OfType<ClipControllerAsset>())
		{
			if ((item.Anim == template.ID || (item.Anims != null && item.Anims.Contains(template.ID))) && item.TimeScale > 0f && item.Bank == template.Bank)
			{
				item.NumTicks = Math.Max(0f, (float)(asset.NumKeys - 1) / item.TimeScale);
				if (item.RawData != null)
				{
					item.RawData["NumTicks"] = item.NumTicks;
				}
			}
		}
		if (App.Logger != null)
		{
			App.Logger.Log($"[AnimationImporter] '{template.Name}': {result.OldKeys} -> {result.NewKeys} keys, section {result.OldSectionSize:N0} -> {result.NewSectionSize:N0} bytes, read back and verified.");
			foreach (string note in result.Notes)
			{
				App.Logger.LogWarning("[AnimationImporter] '" + template.Name + "': " + note);
			}
			if (result.Timing.Clips > 0)
			{
				App.Logger.Log($"[AnimationImporter] '{template.Name}': play length updated in {result.Timing.Clips} clip controller(s) and {result.Timing.Actors} actor controller(s)." + ((result.Timing.SharedActors > 0) ? $" {result.Timing.SharedActors} actor(s) also play other clips and keep their length." : ""));
			}
			else if (result.OldKeys != result.NewKeys)
			{
				App.Logger.LogWarning($"[AnimationImporter] '{template.Name}' now has {result.NewKeys} keys instead of {result.OldKeys}, but its clip controller is not in this bank: the game will keep playing the original length.");
			}
		}
		ApplyToTemplate(asset, template);
		return result.Bank;
	}

	private static byte[] ProcessSplicing(VbrAnimationAsset newAsset, byte[] bankBytes, VbrAnimationAsset template, Dictionary<uint, GenericClass2> classes, bool bigEndian)
	{
		newAsset.Name = template.Name;
		newAsset.ID = template.ID;
		if (newAsset.RawData == null)
		{
			newAsset.RawData = new Dictionary<string, object>();
		}
		newAsset.RawData["__name"] = template.Name;
		newAsset.RawData["__guid"] = template.ID;
		newAsset.RawData["__key"] = GuidToKey(template.ID);
		byte[] newSection = DynamicDat2Serializer.Serialize(newAsset, classes, bigEndian);
		uint typeHashForAsset = DynamicDat2Serializer.GetTypeHashForAsset(newAsset, classes);
		ulong assetKey = GuidToKey(template.ID);
		int keyFieldOffset = 16;
		if (classes.TryGetValue(typeHashForAsset, out var value))
		{
			GenericField2 genericField = value.Elements.FirstOrDefault((GenericField2 f) => f.Name == "__key" || f.Name == "__guid");
			if (genericField != null)
			{
				keyFieldOffset = genericField.Offset;
			}
		}
		int num = FindDat2SectionStart(bankBytes, typeHashForAsset, assetKey, keyFieldOffset, bigEndian);
		if (num < 0)
		{
			throw new InvalidOperationException($"Cannot locate the binary section for '{template.Name}' (type 0x{typeHashForAsset:X8}) " + "in the bank. Confirm the duplicate was saved before importing.");
		}
		byte[] result = BankHeaderPatcher.PatchHeaderForKeys(SpliceSection(bankBytes, num, newSection, bigEndian), Array.Empty<ulong>(), bigEndian);
		ApplyToTemplate(newAsset, template);
		return result;
	}

	private static void ApplyToTemplate(VbrAnimationAsset newAsset, VbrAnimationAsset template)
	{
		template.Data = newAsset.Data;
		template.ConstantPalette = newAsset.ConstantPalette;
		template.FrameBlockSizes = newAsset.FrameBlockSizes;
		template.PaletteIndexes = newAsset.PaletteIndexes;
		template.ConstChanMap = newAsset.ConstChanMap;
		template.QuaternionCount = newAsset.QuaternionCount;
		template.Vector3Count = newAsset.Vector3Count;
		template.NumFloat = newAsset.NumFloat;
		template.ConstQuaternionCount = newAsset.ConstQuaternionCount;
		template.ConstVector3Count = newAsset.ConstVector3Count;
		template.ConstFloatCount = newAsset.ConstFloatCount;
		template.QuatMin = newAsset.QuatMin;
		template.QuatMax = newAsset.QuatMax;
		template.TrajMin = newAsset.TrajMin;
		template.TrajMax = newAsset.TrajMax;
		template.Vec3Min = newAsset.Vec3Min;
		template.Vec3Max = newAsset.Vec3Max;
		template.FloatMin = newAsset.FloatMin;
		template.FloatMax = newAsset.FloatMax;
		template.Dct = newAsset.Dct;
		template.KeyTimeSize = newAsset.KeyTimeSize;
		template.ConstChanMapSize = newAsset.ConstChanMapSize;
		template.ConstPaletteSize = newAsset.ConstPaletteSize;
		template.Flags = newAsset.Flags;
		template.EndFrame = newAsset.EndFrame;
		template.NumKeys = newAsset.NumKeys;
		if (newAsset.RawData != null)
		{
			template.RawData = new Dictionary<string, object>(newAsset.RawData);
			template.RawData["__name"] = template.Name;
			template.RawData["__guid"] = template.ID;
			template.RawData["__key"] = GuidToKey(template.ID);
			template.SetData(template.RawData);
		}
		if (template.FrameBlockSizes == null)
		{
			template.FrameBlockSizes = newAsset.FrameBlockSizes ?? new ushort[0];
		}
		FieldInfo field = typeof(VbrAnimationAsset).GetField("DecompressedData", BindingFlags.Instance | BindingFlags.NonPublic);
		if (field != null && field.GetValue(template) is List<Vector4> list)
		{
			list.Clear();
		}
	}

	private static ulong GuidToKey(Guid g)
	{
		return BitConverter.ToUInt64(g.ToByteArray(), 0);
	}

	private static int FindDat2SectionStart(byte[] bank, uint typeHash, ulong assetKey, int keyFieldOffset, bool big)
	{
		int num = 44 + keyFieldOffset;
		int num2 = 0;
		while (num2 + 12 <= bank.Length)
		{
			if (bank[num2] == 71 && bank[num2 + 1] == 68 && bank[num2 + 2] == 46 && bank[num2 + 3] == 68 && bank[num2 + 4] == 65 && bank[num2 + 5] == 84 && bank[num2 + 6] == 50)
			{
				uint val = ReadU32(bank, num2 + 8, big);
				if (num2 + 32 <= bank.Length && ReadU32(bank, num2 + 28, big) == typeHash && num2 + num + 8 <= bank.Length && ReadU64(bank, num2 + num, big) == assetKey)
				{
					return num2;
				}
				num2 += (int)Math.Max(val, 1u);
			}
			else
			{
				num2++;
			}
		}
		return -1;
	}

	private static byte[] SpliceSection(byte[] bank, int sectionStart, byte[] newSection, bool big)
	{
		int num = (int)ReadU32(bank, sectionStart + 8, big);
		int count = bank.Length - sectionStart - num;
		byte[] array = new byte[bank.Length - num + newSection.Length];
		Buffer.BlockCopy(bank, 0, array, 0, sectionStart);
		Buffer.BlockCopy(newSection, 0, array, sectionStart, newSection.Length);
		Buffer.BlockCopy(bank, sectionStart + num, array, sectionStart + newSection.Length, count);
		return array;
	}

	private static uint ReadU32(byte[] b, int o, bool big)
	{
		if (!big)
		{
			return (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));
		}
		return (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);
	}

	private static ulong ReadU64(byte[] b, int o, bool big)
	{
		if (!big)
		{
			return ReadU64LE(b, o);
		}
		return ReadU64BE(b, o);
	}

	private static ulong ReadU64LE(byte[] b, int o)
	{
		return b[o] | ((ulong)b[o + 1] << 8) | ((ulong)b[o + 2] << 16) | ((ulong)b[o + 3] << 24) | ((ulong)b[o + 4] << 32) | ((ulong)b[o + 5] << 40) | ((ulong)b[o + 6] << 48) | ((ulong)b[o + 7] << 56);
	}

	private static ulong ReadU64BE(byte[] b, int o)
	{
		return ((ulong)b[o] << 56) | ((ulong)b[o + 1] << 48) | ((ulong)b[o + 2] << 40) | ((ulong)b[o + 3] << 32) | ((ulong)b[o + 4] << 24) | ((ulong)b[o + 5] << 16) | ((ulong)b[o + 6] << 8) | b[o + 7];
	}
}
