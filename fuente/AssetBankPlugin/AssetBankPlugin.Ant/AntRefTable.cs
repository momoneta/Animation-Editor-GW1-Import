using System;
using System.Collections.Concurrent;
using Frosty.Core;

namespace AssetBankPlugin.Ant;

public class AntRefTable
{
	public static ConcurrentDictionary<Guid, Guid> InternalRefs = new ConcurrentDictionary<Guid, Guid>();

	public static ConcurrentDictionary<Guid, AntAsset> Refs = new ConcurrentDictionary<Guid, AntAsset>();

	public static void Add(AntAsset asset)
	{
		Refs[asset.ID] = asset;
	}

	public static void Clear()
	{
		InternalRefs.Clear();
		Refs.Clear();
	}

	public static AntAsset Get(Guid refId, bool recurse = false)
	{
		if (refId == Guid.Empty)
		{
			return null;
		}
		if (Refs.TryGetValue(refId, out var value))
		{
			return value;
		}
		if (InternalRefs.TryGetValue(refId, out var value2))
		{
			Refs.TryGetValue(value2, out var value3);
			return value3;
		}
		if (recurse)
		{
			return null;
		}
		int num = -1;
		if (Cache.AntStateBundleIndices.TryGetValue(refId, out var value4))
		{
			num = value4;
		}
		else
		{
			if (!Cache.AntRefMap.TryGetValue(refId, out var value5) || !Cache.AntStateBundleIndices.TryGetValue(value5, out var value6))
			{
				return null;
			}
			num = value6;
		}
		AntStateAssetDefinition.LoadAntStateFromBundle(App.AssetManager.GetBundleEntry(num));
		return Get(refId, recurse: true);
	}
}
