using System;
using System.Collections;
using System.Collections.Generic;
using AssetBankPlugin.Ant;
using AssetBankPlugin.Formats.GenericData;
using AssetBankPlugin.Formats.GenericData2;
using FrostySdk;
using FrostySdk.IO;

namespace AssetBankPlugin.Formats;

public class Bank
{
	private readonly Dictionary<string, int> _nameCounters = new Dictionary<string, int>();

	private static readonly HashSet<string> VolatileKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Offset", "Size", "__offset", "__size" };

	public uint PackagingType { get; set; }

	public List<Section> Sections { get; set; } = new List<Section>();

	public Dictionary<uint, GenericClass> Classes { get; set; } = new Dictionary<uint, GenericClass>();

	public Dictionary<uint, GenericClass2> Classes2 { get; set; } = new Dictionary<uint, GenericClass2>();

	public Dictionary<string, Guid> DataNames { get; set; } = new Dictionary<string, Guid>();

	public Dictionary<Guid, ulong> OriginalHashes { get; } = new Dictionary<Guid, ulong>();

	public Bank(NativeReader r, int bundleId, bool isHashMode = false)
	{
		PackagingType = r.ReadUInt(Endian.Big);
		ProcessLegacyAntRefMap(r);
		uint num = (uint)r.BaseStream.Position;
		string text = r.ReadSizedString(3);
		r.BaseStream.Position = num;
		if (text != "GD.")
		{
			uint num2 = r.ReadUInt(Endian.Big);
			r.BaseStream.Position = num + num2;
		}
		List<SectionData2> list = new List<SectionData2>(8);
		List<SectionData> list2 = new List<SectionData>(8);
		while (r.BaseStream.Position < r.BaseStream.Length)
		{
			Section section = Section.ReadSection(r);
			if (section == null)
			{
				break;
			}
			Sections.Add(section);
			if (section is SectionRefl sectionRefl)
			{
				foreach (KeyValuePair<uint, GenericClass> @class in sectionRefl.Classes)
				{
					Classes[@class.Key] = @class.Value;
				}
			}
			else if (section is SectionRef2 sectionRef)
			{
				foreach (KeyValuePair<uint, GenericClass2> class2 in sectionRef.Classes)
				{
					Classes2[class2.Key] = class2.Value;
				}
			}
			else if (section is SectionData2 item)
			{
				list.Add(item);
			}
			else if (section is SectionData item2)
			{
				list2.Add(item2);
			}
		}
		foreach (SectionData item3 in list2)
		{
			AntAsset antAsset = AntAsset.Deserialize(r, item3, Classes, this);
			if (antAsset != null)
			{
				if (isHashMode)
				{
					ProcessHashMode(antAsset);
				}
				else
				{
					AddAsset(antAsset, bundleId);
				}
			}
		}
		foreach (SectionData2 item4 in list)
		{
			long num3 = item4.DataOffset + 28;
			if (num3 + 16 > item4.DataOffset + item4.DataSize)
			{
				continue;
			}
			AntAsset antAsset2 = AntAsset.Deserialize(r, item4, Classes2, this, num3);
			if (antAsset2 != null)
			{
				if (isHashMode)
				{
					ProcessHashMode(antAsset2);
				}
				else
				{
					AddAsset(antAsset2, bundleId);
				}
			}
		}
		if (isHashMode)
		{
			List<Guid> list3 = new List<Guid>();
			foreach (KeyValuePair<Guid, AntAsset> @ref in AntRefTable.Refs)
			{
				if (@ref.Value.Bank == this)
				{
					list3.Add(@ref.Key);
				}
			}
			foreach (Guid item5 in list3)
			{
				AntRefTable.Refs.TryRemove(item5, out var _);
			}
		}
		foreach (Section section2 in Sections)
		{
			if (section2 is SectionData2 sectionData)
			{
				sectionData.ClearCaches();
			}
		}
		Sections.Clear();
	}

	private void ProcessLegacyAntRefMap(NativeReader r)
	{
		ProfileVersion dataVersion = (ProfileVersion)ProfilesLibrary.DataVersion;
		if ((dataVersion == ProfileVersion.PlantsVsZombiesGardenWarfare2 || dataVersion == ProfileVersion.PlantsVsZombiesGardenWarfare || dataVersion == ProfileVersion.Battlefield4 || dataVersion == ProfileVersion.Battlefield1) && PackagingType == 3)
		{
			r.BaseStream.Position = 56L;
			uint num = r.ReadUInt(Endian.Big) / 20;
			for (int i = 0; i < num; i++)
			{
				Guid key = r.ReadGuid();
				byte[] array = new byte[16];
				r.BaseStream.Read(array, 0, 4);
				Guid value = new Guid(array);
				AntRefTable.InternalRefs[key] = value;
				Cache.AntRefMap[key] = value;
			}
			r.BaseStream.Position = 4L;
		}
	}

	private void AddAsset(AntAsset asset, int bundleId)
	{
		string name = asset.Name;
		string key;
		if (!_nameCounters.TryGetValue(name, out var value))
		{
			key = name;
			_nameCounters[name] = 1;
		}
		else
		{
			key = name + " [" + value + "]";
			_nameCounters[name] = value + 1;
		}
		DataNames[key] = asset.ID;
		Cache.AntStateBundleIndices[asset.ID] = bundleId;
	}

	private void ProcessHashMode(AntAsset asset)
	{
		OriginalHashes[asset.ID] = ComputeAssetHash(asset);
	}

	public static ulong ComputeAssetHash(AntAsset asset)
	{
		if (asset?.RawData == null)
		{
			return 0uL;
		}
		return ComputeHash(asset.RawData);
	}

	private static ulong ComputeHash(object obj)
	{
		if (obj == null)
		{
			return 0uL;
		}
		if (obj is Dictionary<string, object> dictionary)
		{
			ulong num = 0uL;
			{
				foreach (KeyValuePair<string, object> item in dictionary)
				{
					if (!VolatileKeys.Contains(item.Key))
					{
						ulong num2 = (ulong)item.Key.GetHashCode();
						ulong num3 = ComputeHash(item.Value);
						num ^= (num2 * 397) ^ num3;
					}
				}
				return num;
			}
		}
		if (obj is string text)
		{
			return (ulong)text.GetHashCode();
		}
		if (obj is byte[] array)
		{
			ulong num4 = 14695981039346656037uL;
			for (int i = 0; i < array.Length; i++)
			{
				num4 ^= array[i];
				num4 *= 1099511628211L;
			}
			return num4;
		}
		if (obj is uint[] array2)
		{
			ulong num5 = 14695981039346656037uL;
			for (int j = 0; j < array2.Length; j++)
			{
				num5 ^= array2[j];
				num5 *= 1099511628211L;
			}
			return num5;
		}
		if (obj is float[] array3)
		{
			ulong num6 = 14695981039346656037uL;
			for (int k = 0; k < array3.Length; k++)
			{
				num6 ^= (ulong)array3[k].GetHashCode();
				num6 *= 1099511628211L;
			}
			return num6;
		}
		if (obj is object[] array4)
		{
			ulong num7 = 17uL;
			for (int l = 0; l < array4.Length; l++)
			{
				num7 = num7 * 31 + ComputeHash(array4[l]);
			}
			return num7;
		}
		if (obj is IEnumerable enumerable)
		{
			ulong num8 = 17uL;
			{
				foreach (object item2 in enumerable)
				{
					num8 = num8 * 31 + ComputeHash(item2);
				}
				return num8;
			}
		}
		return (ulong)obj.GetHashCode();
	}
}
