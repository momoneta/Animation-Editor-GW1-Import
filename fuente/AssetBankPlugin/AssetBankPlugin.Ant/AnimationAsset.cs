using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AssetBankPlugin.Enums;
using AssetBankPlugin.Export;
using Frosty.Core;
using FrostySdk;

namespace AssetBankPlugin.Ant;

public class AnimationAsset : AntAsset
{
	public uint CodecType;

	public uint AnimId;

	public float TrimOffset;

	public ushort EndFrame;

	public bool Additive;

	public Guid ChannelToDofAsset;

	public Guid DofSetList;

	public Dictionary<string, BoneChannelType> Channels;

	public List<ChannelInfo> OrderedChannels;

	public float FPS;

	public StorageType StorageType;

	private RigAsset _cachedRig;

	private Dictionary<uint, Vector4> _rigDefaultVector4;

	private Dictionary<uint, Vector3> _rigDefaultVector3;

	private Dictionary<uint, float> _rigDefaultFloat;

	public override string Name { get; set; }

	public override Guid ID { get; set; }

	private void CollectDofSetGuidsFromList(Guid listId, HashSet<Guid> guids)
	{
		AntAsset antAsset = AntRefTable.Get(listId);
		if (antAsset != null && !(antAsset.AssetType != "DofSetListAsset"))
		{
			Guid[] propertyArray = antAsset.GetPropertyArray<Guid>("DofSetAssets");
			if (propertyArray.Length != 0)
			{
				guids.UnionWith(propertyArray);
			}
			Guid[] propertyArray2 = antAsset.GetPropertyArray<Guid>("DofSetListAssets");
			foreach (Guid listId2 in propertyArray2)
			{
				CollectDofSetGuidsFromList(listId2, guids);
			}
		}
	}

	public override void SetData(Dictionary<string, object> data)
	{
		ParseBasicData(data);
		if (data.TryGetValue("CodecType", out var value))
		{
			CodecType = Convert.ToUInt32(value);
		}
		if (data.TryGetValue("AnimId", out var value2))
		{
			AnimId = (uint)Convert.ToInt64(value2);
		}
		if (data.TryGetValue("EndFrame", out var value3))
		{
			EndFrame = Convert.ToUInt16(value3);
		}
		if (data.TryGetValue("Additive", out var value4))
		{
			Additive = Convert.ToBoolean(value4);
		}
		if (data.TryGetValue("ChannelToDofAsset", out var value5))
		{
			ChannelToDofAsset = SafeGuid(value5);
		}
		if (data.TryGetValue("DofSetList", out var value6))
		{
			DofSetList = SafeGuid(value6);
		}
	}

	public AntAsset RecursiveHierarchySearch(AntAsset initialHierarchy, AntAsset TargetHierarchy)
	{
		if (initialHierarchy == null || TargetHierarchy == null)
		{
			return null;
		}
		if (initialHierarchy.ID == TargetHierarchy.ID)
		{
			return initialHierarchy;
		}
		Guid[] propertyArray = initialHierarchy.GetPropertyArray<Guid>("Children");
		if (propertyArray != null)
		{
			for (int i = 0; i < propertyArray.Length; i++)
			{
				AntAsset antAsset = AntRefTable.Get(propertyArray[i]);
				if (antAsset != null && antAsset.AssetType == "LayoutHierarchyAsset")
				{
					AntAsset antAsset2 = RecursiveHierarchySearch(antAsset, TargetHierarchy);
					if (antAsset2 != null && antAsset2.ID == TargetHierarchy.ID)
					{
						return antAsset2;
					}
				}
			}
		}
		return null;
	}

	public Dictionary<string, BoneChannelType> GetChannels(Guid channelToDofAsset)
	{
		ProfileVersion dataVersion = (ProfileVersion)ProfilesLibrary.DataVersion;
		if (!(AntRefTable.Get(channelToDofAsset) is ChannelToDofAsset channelToDofAsset2))
		{
			App.Logger.LogError($"[AnimationAsset] Missing ChannelToDofAsset ({channelToDofAsset}) for '{Name}'.");
			OrderedChannels = new List<ChannelInfo>();
			return new Dictionary<string, BoneChannelType>();
		}
		StorageType = channelToDofAsset2.StorageType;
		Guid guid = Guid.Empty;
		ICollection<AntAsset> values = AntRefTable.Refs.Values;
		foreach (AntAsset item2 in values)
		{
			if (item2 is ClipControllerAsset clipControllerAsset)
			{
				if (clipControllerAsset.ID == ID || clipControllerAsset.Anim == ID || (clipControllerAsset.Anims != null && clipControllerAsset.Anims.Contains(ID)))
				{
					FPS = clipControllerAsset.FPS;
					guid = clipControllerAsset.Target;
					break;
				}
			}
			else if (item2.AssetType == "ClipControllerData" && item2.GetProperty<Guid>("Anim") == ID)
			{
				FPS = item2.GetProperty("FPS", 0f);
				guid = item2.GetProperty<Guid>("Target");
				break;
			}
		}
		if (guid == Guid.Empty && DofSetList != Guid.Empty)
		{
			guid = DofSetList;
		}
		if (guid == Guid.Empty)
		{
			App.Logger.LogError("[AnimationAsset] Could not find a Target Hierarchy/DofSetList for '" + Name + "'.");
			OrderedChannels = new List<ChannelInfo>();
			return new Dictionary<string, BoneChannelType>();
		}
		Dictionary<string, BoneChannelType> channelNames = new Dictionary<string, BoneChannelType>();
		HashSet<Guid> encounteredDofSets = new HashSet<Guid>();
		AntAsset antAsset = AntRefTable.Get(guid);
		if (antAsset != null && antAsset.AssetType == "LayoutHierarchyAsset")
		{
			Guid[] propertyArray = antAsset.GetPropertyArray<Guid>("LayoutAssets");
			foreach (Guid guid2 in propertyArray)
			{
				AntAsset antAsset2 = AntRefTable.Get(guid2);
				if (antAsset2 != null && antAsset2.AssetType == "LayoutAsset")
				{
					if (!(antAsset2.GetProperty<object>("Slots") is IEnumerable enumerable))
					{
						continue;
					}
					foreach (object item3 in enumerable)
					{
						if (item3 is Dictionary<string, object> dictionary)
						{
							object value;
							string key = (dictionary.TryGetValue("Name", out value) ? SafeString(value) : "unknown");
							object value3;
							BoneChannelType value2 = (dictionary.TryGetValue("Type", out value3) ? ((BoneChannelType)Convert.ToInt32(value3)) : BoneChannelType.None);
							channelNames[key] = value2;
						}
					}
				}
				else if (antAsset2 != null && antAsset2.AssetType == "DofSetAsset")
				{
					ExtractSlotsFromDofSet(guid2);
				}
				else if (antAsset2?.AssetType == "DeltaTrajLayoutAsset")
				{
					for (int j = 0; j < 8; j++)
					{
						channelNames[j.ToString()] = BoneChannelType.Rotation;
					}
				}
			}
		}
		else if (antAsset != null && antAsset.AssetType == "DofSetListAsset")
		{
			Guid[] propertyArray = antAsset.GetPropertyArray<Guid>("DofSetAssets");
			foreach (Guid guid3 in propertyArray)
			{
				ExtractSlotsFromDofSet(guid3);
			}
		}
		else if (antAsset != null && antAsset.AssetType == "DofSetAsset")
		{
			ExtractSlotsFromDofSet(antAsset.ID);
		}
		uint[] indexData = channelToDofAsset2.IndexData;
		int num = ((indexData != null) ? indexData.Length : 0);
		KeyValuePair<string, BoneChannelType>[] array = channelNames.ToArray();
		int num2 = array.Length;
		if (channelToDofAsset2.rigId == Guid.Empty && encounteredDofSets.Count > 0)
		{
			foreach (AntAsset item4 in values)
			{
				if (!(item4 is RigAsset { DofIds: not null } rigAsset) || rigAsset.DofIds.Length == 0)
				{
					continue;
				}
				HashSet<Guid> hashSet = null;
				if (rigAsset.RigDofSets != null)
				{
					hashSet = new HashSet<Guid>(rigAsset.RigDofSets);
				}
				else if (rigAsset.DofSetLists != null)
				{
					hashSet = new HashSet<Guid>();
					Guid[] propertyArray = rigAsset.DofSetLists;
					foreach (Guid listId in propertyArray)
					{
						CollectDofSetGuidsFromList(listId, hashSet);
					}
				}
				if (hashSet != null && hashSet.IsSupersetOf(encounteredDofSets))
				{
					channelToDofAsset2.rigId = rigAsset.ID;
					App.Logger.Log("[AnimationAsset] Automatic rig match: '" + rigAsset.Name + "' for animation '" + Name + "'");
					break;
				}
			}
		}
		if (channelToDofAsset2.rigId == Guid.Empty)
		{
			foreach (AntAsset item5 in values)
			{
				if (!(item5 is RigAsset { DofSetLists: { } dofSetLists } rigAsset2))
				{
					continue;
				}
				Guid[] propertyArray = dofSetLists;
				for (int i = 0; i < propertyArray.Length; i++)
				{
					AntAsset antAsset3 = AntRefTable.Get(propertyArray[i]);
					if (antAsset3 != null && antAsset3.AssetType == "LayoutHierarchyAsset" && antAsset != null && antAsset.AssetType == "LayoutHierarchyAsset" && RecursiveHierarchySearch(antAsset3, antAsset) != null)
					{
						channelToDofAsset2.rigId = rigAsset2.ID;
						App.Logger.Log("[AnimationAsset] Fallback rig match via hierarchy search: '" + rigAsset2.Name + "'");
						break;
					}
				}
				if (channelToDofAsset2.rigId != Guid.Empty)
				{
					break;
				}
			}
		}
		List<ChannelInfo> list = new List<ChannelInfo>(num);
		if (dataVersion == ProfileVersion.PlantsVsZombiesBattleforNeighborville || dataVersion == ProfileVersion.StarWarsBattlefrontII)
		{
			if (channelToDofAsset2.rigId == Guid.Empty)
			{
				OrderedChannels = list;
				return new Dictionary<string, BoneChannelType>();
			}
			if (!(AntRefTable.Get(channelToDofAsset2.rigId) is RigAsset rig))
			{
				OrderedChannels = list;
				return new Dictionary<string, BoneChannelType>();
			}
			CacheRigDefaults(rig);
			Dictionary<uint, (string, BoneChannelType)> dictionary2 = GetRigDofOrder(rig).ToDictionary(((uint DofId, string Name, BoneChannelType Type) tuple) => tuple.DofId, ((uint DofId, string Name, BoneChannelType Type) v) => (v.Name, v.Type));
			for (int num3 = 0; num3 < num; num3++)
			{
				uint num4 = indexData[num3];
				if (dictionary2.TryGetValue(num4, out var value4))
				{
					list.Add(new ChannelInfo
					{
						Name = value4.Item1,
						Type = value4.Item2,
						DofId = num4
					});
				}
				else
				{
					list.Add(new ChannelInfo
					{
						Name = $"Unknown_{num4}",
						Type = BoneChannelType.None,
						DofId = num4
					});
				}
			}
		}
		else if (dataVersion == ProfileVersion.PlantsVsZombiesGardenWarfare2)
		{
			if (channelToDofAsset2.rigId == Guid.Empty)
			{
				OrderedChannels = list;
				return new Dictionary<string, BoneChannelType>();
			}
			if (!(AntRefTable.Get(channelToDofAsset2.rigId) is RigAsset { DofIds: not null } rigAsset3))
			{
				OrderedChannels = list;
				return new Dictionary<string, BoneChannelType>();
			}
			Dictionary<uint, int> dictionary3 = new Dictionary<uint, int>(rigAsset3.DofIds.Length);
			for (int num5 = 0; num5 < rigAsset3.DofIds.Length; num5++)
			{
				dictionary3[rigAsset3.DofIds[num5]] = num5;
			}
			for (int num6 = 0; num6 < num; num6++)
			{
				uint num7 = indexData[num6];
				if (dictionary3.TryGetValue(num7, out var value5) && value5 >= 0 && value5 < num2)
				{
					KeyValuePair<string, BoneChannelType> keyValuePair = array[value5];
					list.Add(new ChannelInfo
					{
						Name = keyValuePair.Key,
						Type = keyValuePair.Value,
						DofId = num7
					});
				}
				else
				{
					list.Add(new ChannelInfo
					{
						Name = $"Unknown_{num7}",
						Type = BoneChannelType.None,
						DofId = num7
					});
				}
			}
		}
		else if (dataVersion == ProfileVersion.PlantsVsZombiesGardenWarfare || StorageType == StorageType.Overwrite)
		{
			for (int num8 = 0; num8 < num; num8++)
			{
				int num9 = (int)indexData[num8];
				if (num9 >= 0 && num9 < num2)
				{
					KeyValuePair<string, BoneChannelType> keyValuePair2 = array[num9];
					list.Add(new ChannelInfo
					{
						Name = keyValuePair2.Key,
						Type = keyValuePair2.Value,
						DofId = 0u
					});
				}
			}
		}
		else if (StorageType == StorageType.Append)
		{
			Dictionary<int, int> dictionary4 = new Dictionary<int, int>(num / 2);
			int num10 = 0;
			for (int num11 = 0; num11 < num; num11 += 2)
			{
				int key2 = (int)indexData[num11];
				int num12 = (int)indexData[num11 + 1];
				dictionary4[key2] = num10++;
				int num13 = dictionary4[key2];
				string name = ((num12 >= 0 && num12 < num2) ? array[num12].Key : "");
				ChannelInfo item = new ChannelInfo
				{
					Name = name,
					Type = BoneChannelType.None,
					DofId = 0u
				};
				if (num13 <= list.Count)
				{
					list.Insert(num13, item);
				}
				else
				{
					list.Add(item);
				}
			}
			for (int num14 = 0; num14 < list.Count; num14++)
			{
				if (!string.IsNullOrEmpty(list[num14].Name) && channelNames.TryGetValue(list[num14].Name, out var value6))
				{
					list[num14].Type = value6;
				}
			}
		}
		Dictionary<string, BoneChannelType> dictionary5 = new Dictionary<string, BoneChannelType>();
		foreach (ChannelInfo item6 in list)
		{
			if (!string.IsNullOrEmpty(item6.Name) && !dictionary5.ContainsKey(item6.Name))
			{
				dictionary5[item6.Name] = item6.Type;
			}
		}
		OrderedChannels = list;
		return dictionary5;
		void ExtractSlotsFromDofSet(Guid guid4)
		{
			encounteredDofSets.Add(guid4);
			AntAsset antAsset4 = AntRefTable.Get(guid4);
			if (antAsset4 != null && antAsset4.AssetType == "DofSetAsset")
			{
				if (antAsset4.GetProperty<object>("Slots") is IEnumerable enumerable2)
				{
					foreach (object item7 in enumerable2)
					{
						string key3 = "unknown";
						BoneChannelType value7 = BoneChannelType.None;
						if (item7 is AntAsset antAsset5)
						{
							if (antAsset5.AssetType == "LayoutEntry")
							{
								key3 = antAsset5.Name;
								value7 = (BoneChannelType)antAsset5.GetProperty("Type", 0u);
							}
							else
							{
								key3 = GetStringFromObj(antAsset5.RawData);
								value7 = (BoneChannelType)antAsset5.GetProperty("Type", 0u);
							}
						}
						else if (item7 is Dictionary<string, object> dictionary6)
						{
							key3 = GetStringFromObj(dictionary6);
							if (dictionary6.TryGetValue("Type", out var value8))
							{
								value7 = (BoneChannelType)Convert.ToUInt32(value8);
							}
						}
						channelNames[key3] = value7;
					}
				}
				Guid[] propertyArray2 = antAsset4.GetPropertyArray<Guid>("DofAssets");
				foreach (Guid guid5 in propertyArray2)
				{
					ExtractSlotsFromDofSet(guid5);
				}
			}
		}
	}

	private List<(uint DofId, string Name, BoneChannelType Type)> GetRigDofOrder(RigAsset rig)
	{
		List<(uint, string, BoneChannelType)> list = new List<(uint, string, BoneChannelType)>();
		if (rig.DofIds == null)
		{
			return list;
		}
		List<Slot>[] array = new List<Slot>[(rig.RigDofSets != null) ? rig.RigDofSets.Length : 0];
		int num = 0;
		while (rig.RigDofSets != null && num < rig.RigDofSets.Length)
		{
			AntAsset antAsset = AntRefTable.Get(rig.RigDofSets[num]);
			List<Slot> list2 = new List<Slot>();
			if (antAsset != null && antAsset.AssetType == "DofSetAsset" && antAsset.GetProperty<object>("Slots") is IEnumerable enumerable)
			{
				foreach (object item3 in enumerable)
				{
					string name = "unknown";
					BoneChannelType type = BoneChannelType.None;
					if (item3 is AntAsset antAsset2)
					{
						name = antAsset2.Name;
						type = (BoneChannelType)antAsset2.GetProperty("Type", 0u);
					}
					else if (item3 is Dictionary<string, object> dictionary)
					{
						name = GetStringFromObj(dictionary);
						if (dictionary.TryGetValue("Type", out var value))
						{
							type = (BoneChannelType)Convert.ToUInt32(value);
						}
					}
					list2.Add(new Slot
					{
						Name = name,
						Type = type
					});
				}
			}
			array[num] = list2;
			num++;
		}
		bool flag = rig.DofSetIdIndices != null && rig.RigDofSets != null && rig.DofSetIdIndices.Length == rig.RigDofSets.Length;
		uint num2 = 0u;
		int num3 = 0;
		while (rig.RigDofSets != null && num3 < rig.RigDofSets.Length)
		{
			uint num4 = (flag ? rig.DofSetIdIndices[num3] : num2);
			int count = array[num3].Count;
			for (int i = 0; i < count; i++)
			{
				uint num5 = rig.DofIds[num4 + i];
				Slot slot = array[num3][i];
				string item = ((!string.IsNullOrEmpty(slot.Name)) ? slot.Name : $"Unknown_{num5}");
				list.Add((num5, item, slot.Type));
			}
			if (!flag)
			{
				num2 += (uint)count;
			}
			num3++;
		}
		if (rig.RigDofSets == null && rig.DofSetLists != null)
		{
			List<Slot> list3 = new List<Slot>();
			Guid[] dofSetLists = rig.DofSetLists;
			foreach (Guid assetId in dofSetLists)
			{
				CollectSlotsGW2(assetId, list3);
			}
			for (int k = 0; k < rig.DofIds.Length && k < list3.Count; k++)
			{
				uint num6 = rig.DofIds[k];
				Slot slot2 = list3[k];
				string item2 = ((!string.IsNullOrEmpty(slot2.Name)) ? slot2.Name : $"Unknown_{num6}");
				list.Add((num6, item2, slot2.Type));
			}
		}
		return list;
	}

	private string GetStringFromObj(Dictionary<string, object> dict)
	{
		if (dict.TryGetValue("chars", out var value))
		{
			return SafeString(value);
		}
		if (dict.TryGetValue("Name", out var value2))
		{
			return SafeString(value2);
		}
		return "unknown";
	}

	private void CollectSlotsGW2(Guid assetId, List<Slot> accumulator)
	{
		AntAsset antAsset = AntRefTable.Get(assetId);
		if (antAsset == null)
		{
			return;
		}
		if (antAsset.AssetType == "DofSetAsset")
		{
			if (antAsset.GetProperty<object>("Slots") is IEnumerable enumerable)
			{
				foreach (object item in enumerable)
				{
					string name = "unknown";
					BoneChannelType type = BoneChannelType.None;
					if (item is AntAsset antAsset2)
					{
						name = antAsset2.Name;
						type = (BoneChannelType)antAsset2.GetProperty("Type", 0u);
					}
					else if (item is Dictionary<string, object> dictionary)
					{
						name = GetStringFromObj(dictionary);
						if (dictionary.TryGetValue("Type", out var value))
						{
							type = (BoneChannelType)Convert.ToUInt32(value);
						}
					}
					accumulator.Add(new Slot
					{
						Name = name,
						Type = type
					});
				}
			}
			Guid[] propertyArray = antAsset.GetPropertyArray<Guid>("DofAssets");
			foreach (Guid assetId2 in propertyArray)
			{
				CollectSlotsGW2(assetId2, accumulator);
			}
		}
		else if (antAsset.AssetType == "DofSetListAsset")
		{
			Guid[] propertyArray = antAsset.GetPropertyArray<Guid>("DofSetAssets");
			foreach (Guid assetId3 in propertyArray)
			{
				CollectSlotsGW2(assetId3, accumulator);
			}
		}
		else if (antAsset.AssetType == "LayoutHierarchyAsset")
		{
			Guid[] propertyArray = antAsset.GetPropertyArray<Guid>("LayoutAssets");
			foreach (Guid assetId4 in propertyArray)
			{
				CollectSlotsGW2(assetId4, accumulator);
			}
		}
	}

	private void CacheRigDefaults(RigAsset rig)
	{
		if (_cachedRig == rig)
		{
			return;
		}
		_cachedRig = rig;
		_rigDefaultVector4 = new Dictionary<uint, Vector4>();
		if (rig.DefaultVector4Values != null)
		{
			foreach (DefaultDofVector4 defaultVector4Value in rig.DefaultVector4Values)
			{
				if (defaultVector4Value.Value != null && defaultVector4Value.Value.Length >= 4)
				{
					_rigDefaultVector4[defaultVector4Value.DofId] = new Vector4(defaultVector4Value.Value[0], defaultVector4Value.Value[1], defaultVector4Value.Value[2], defaultVector4Value.Value[3]);
				}
			}
		}
		_rigDefaultVector3 = new Dictionary<uint, Vector3>();
		if (rig.DefaultVector3Values != null)
		{
			foreach (DefaultDofVector3 defaultVector3Value in rig.DefaultVector3Values)
			{
				if (defaultVector3Value.Value != null && defaultVector3Value.Value.Length >= 3)
				{
					_rigDefaultVector3[defaultVector3Value.DofId] = new Vector3(defaultVector3Value.Value[0], defaultVector3Value.Value[1], defaultVector3Value.Value[2]);
				}
			}
		}
		_rigDefaultFloat = new Dictionary<uint, float>();
		if (rig.DefaultFloatValues == null)
		{
			return;
		}
		foreach (DefaultDofFloat defaultFloatValue in rig.DefaultFloatValues)
		{
			_rigDefaultFloat[defaultFloatValue.DofId] = defaultFloatValue.Value;
		}
	}

	public bool TryGetRigDefaultVector4(uint dofId, out Vector4 value)
	{
		value = default(Vector4);
		if (_rigDefaultVector4 != null)
		{
			return _rigDefaultVector4.TryGetValue(dofId, out value);
		}
		return false;
	}

	public bool TryGetRigDefaultVector3(uint dofId, out Vector3 value)
	{
		value = default(Vector3);
		if (_rigDefaultVector3 != null)
		{
			return _rigDefaultVector3.TryGetValue(dofId, out value);
		}
		return false;
	}

	public bool TryGetRigDefaultFloat(uint dofId, out float value)
	{
		value = 0f;
		if (_rigDefaultFloat != null)
		{
			return _rigDefaultFloat.TryGetValue(dofId, out value);
		}
		return false;
	}

	public virtual InternalAnimation ConvertToInternal()
	{
		string assetType = base.AssetType;
		if (!(assetType == "FrameAnimationAsset"))
		{
			if (assetType == "CurveAnimationAsset")
			{
				return ConvertCurveAnimation();
			}
			return null;
		}
		return ConvertFrameAnimation();
	}

	private InternalAnimation ConvertFrameAnimation()
	{
		float[] propertyArray = GetPropertyArray<float>("Data");
		if (propertyArray == null || propertyArray.Length == 0)
		{
			return null;
		}
		List<ChannelInfo> orderedChannels = OrderedChannels;
		if (orderedChannels == null || orderedChannels.Count == 0)
		{
			return null;
		}
		List<Vector3> list = new List<Vector3>();
		List<Quaternion> list2 = new List<Quaternion>();
		List<Vector3> list3 = new List<Vector3>();
		List<string> list4 = new List<string>();
		List<string> list5 = new List<string>();
		List<string> list6 = new List<string>();
		int num = 0;
		foreach (ChannelInfo item2 in orderedChannels)
		{
			if (num >= propertyArray.Length)
			{
				break;
			}
			string name = item2.Name;
			switch (item2.Type)
			{
			case BoneChannelType.Rotation:
				if (num + 3 < propertyArray.Length)
				{
					list5.Add(name.Replace(".q", ""));
					list2.Add(new Quaternion(propertyArray[num], propertyArray[num + 1], propertyArray[num + 2], propertyArray[num + 3]));
				}
				num += 4;
				break;
			case BoneChannelType.Position:
				if (num + 2 < propertyArray.Length)
				{
					list4.Add(name.Replace(".t", ""));
					list.Add(new Vector3(propertyArray[num], propertyArray[num + 1], propertyArray[num + 2]));
				}
				num += 4;
				break;
			case BoneChannelType.Scale:
				if (num + 2 < propertyArray.Length)
				{
					list6.Add(name.Replace(".s", ""));
					list3.Add(new Vector3(propertyArray[num], propertyArray[num + 1], propertyArray[num + 2]));
				}
				num += 4;
				break;
			default:
				num++;
				break;
			}
		}
		InternalAnimation obj = new InternalAnimation
		{
			Name = Name
		};
		Frame item = new Frame
		{
			FrameIndex = 0,
			Positions = list,
			Rotations = list2,
			Scales = list3
		};
		obj.Frames.Add(item);
		obj.PositionChannels = list4;
		obj.RotationChannels = list5;
		obj.ScaleChannels = list6;
		obj.Additive = Additive;
		return obj;
	}

	private InternalAnimation ConvertCurveAnimation()
	{
		InternalAnimation internalAnimation = new InternalAnimation();
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		foreach (KeyValuePair<string, BoneChannelType> channel in Channels)
		{
			if (channel.Value == BoneChannelType.Rotation)
			{
				list2.Add(channel.Key.Replace(".q", ""));
			}
			else if (channel.Value == BoneChannelType.Position)
			{
				list.Add(channel.Key.Replace(".t", ""));
			}
		}
		internalAnimation.Name = Name;
		internalAnimation.PositionChannels = list;
		internalAnimation.RotationChannels = list2;
		internalAnimation.Additive = Additive;
		return internalAnimation;
	}
}
