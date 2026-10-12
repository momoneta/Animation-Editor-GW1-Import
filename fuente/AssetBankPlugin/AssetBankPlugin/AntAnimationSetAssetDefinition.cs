using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using AssetBankPlugin.Ant;
using AssetBankPlugin.Export;
using AssetBankPlugin.Formats;
using Frosty.Core;
using FrostySdk;
using FrostySdk.IO;
using FrostySdk.Managers.Entries;

namespace AssetBankPlugin;

public class AntAnimationSetAssetDefinition : AssetDefinition
{
	public override void GetSupportedExportTypes(List<AssetExportType> exportTypes)
	{
		exportTypes.Add(new AssetExportType("gltf", "GL Transfer format"));
		exportTypes.Add(new AssetExportType("xml", "XML Animation Keyframe Dump"));
		exportTypes.Add(new AssetExportType("smd", "Source StudioMdl Data"));
		base.GetSupportedExportTypes(exportTypes);
	}

	public override bool Export(EbxAssetEntry entry, string path, string filterType)
	{
		AnimationOptions animationOptions = new AnimationOptions();
		animationOptions.Load();
		dynamic rootObject = App.AssetManager.GetEbx(entry).RootObject;
		if (rootObject.AssetBankResource != 0uL)
		{
			ResAssetEntry entry2 = App.AssetManager.GetResEntry(rootObject.AssetBankResource);
			Stream res = App.AssetManager.GetRes(entry2);
			string path2 = "Caches/" + ProfilesLibrary.ProfileName + "_antstate.cache";
			string path3 = "Caches/" + ProfilesLibrary.ProfileName + "_antref.cache";
			if (animationOptions.UseCache)
			{
				if (File.Exists(path2) && File.Exists(path3))
				{
					Cache.ReadState(path2);
					Cache.ReadMap(path3);
				}
				else
				{
					foreach (BundleEntry item in App.AssetManager.EnumerateBundles())
					{
						LoadAntStateFromBundle(item);
					}
					Cache.WriteState(path2);
					Cache.WriteMap(path3);
				}
			}
			else
			{
				foreach (BundleEntry item2 in App.AssetManager.EnumerateBundles())
				{
					LoadAntStateFromBundle(item2);
				}
			}
			using (NativeReader r = new NativeReader(res))
			{
				Bank bank = new Bank(r, 0);
				EbxAssetEntry entry3 = App.AssetManager.GetEbxEntry(rootObject.SkeletonAsset.External.FileGuid);
				dynamic rootObject2 = App.AssetManager.GetEbx(entry3).RootObject;
				dynamic val = SkeletonAssetExport.ConvertToInternal(rootObject2);
				foreach (KeyValuePair<string, Guid> dataName in bank.DataNames)
				{
					if (AntRefTable.Get(dataName.Value) is AnimationAsset animationAsset)
					{
						animationAsset.Name = dataName.Key;
						animationAsset.Channels = animationAsset.GetChannels(animationAsset.ChannelToDofAsset);
						InternalAnimation internalAnimation = animationAsset.ConvertToInternal();
						if (filterType == "cast")
						{
							new AnimationExporterCAST().Export(internalAnimation, val, Path.GetDirectoryName(path));
						}
						else
						{
							new AnimationExporterSEANIM().Export(internalAnimation, val, Path.GetDirectoryName(path));
						}
					}
				}
			}
			MessageBox.Show("Exported " + entry.Name + " for " + ProfilesLibrary.ProfileName, "Test");
			return true;
		}
		App.Logger.Log("Asset does not reference a bank");
		return false;
	}

	public static void LoadAntStateFromBundle(BundleEntry bundle)
	{
		foreach (ResAssetEntry item in from x in App.AssetManager.EnumerateRes(bundle)
			where x.Type == "AssetBank"
			select x)
		{
			Console.WriteLine(item.DisplayName);
			Stream res = App.AssetManager.GetRes(item);
			NativeReader nativeReader = new NativeReader(res);
			new Bank(nativeReader, App.AssetManager.GetBundleId(bundle));
			res.Dispose();
			nativeReader.Dispose();
		}
	}
}
