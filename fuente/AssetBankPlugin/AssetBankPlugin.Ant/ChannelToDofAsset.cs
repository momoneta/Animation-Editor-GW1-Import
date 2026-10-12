using System;
using System.Collections.Generic;
using AssetBankPlugin.Enums;
using FrostySdk;

namespace AssetBankPlugin.Ant;

public class ChannelToDofAsset : AntAsset
{
	public override string Name { get; set; }

	public override Guid ID { get; set; }

	public StorageType StorageType { get; set; } = StorageType.Overwrite;

	public uint[] IndexData { get; set; }

	public Guid rigId { get; set; }

	public override void SetData(Dictionary<string, object> data)
	{
		ParseBasicData(data);
		object value2;
		object value3;
		if (data.TryGetValue("ChannelToDofIds", out var value))
		{
			IndexData = ConvertArray<uint>(value);
		}
		else if (data.TryGetValue("DofIds", out value2))
		{
			IndexData = ConvertArray<uint>(value2);
		}
		else if (data.TryGetValue("IndexData", out value3))
		{
			byte[] array = ConvertArray<byte>(value3);
			if (ProfilesLibrary.IsLoaded(ProfileVersion.PlantsVsZombiesGardenWarfare) && array.Length > 256)
			{
				uint[] array2 = new uint[array.Length / 2];
				for (int i = 0; i < array2.Length; i++)
				{
					array2[i] = (uint)((array[i * 2] << 8) | array[i * 2 + 1]);
				}
				IndexData = array2;
			}
			else
			{
				IndexData = Array.ConvertAll(array, (Converter<byte, uint>)((byte val) => val));
			}
		}
		if (data.TryGetValue("Rig", out var value4))
		{
			rigId = SafeGuid(value4);
		}
		if (data.TryGetValue("StorageType", out var value5))
		{
			StorageType = (StorageType)Convert.ToInt32(value5);
		}
	}
}
