using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using AssetBankPlugin.Ant;
using AssetBankPlugin.Export;
using AssetBankPlugin.Formats;
using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Windows;
using FrostySdk.Interfaces;
using FrostySdk.IO;
using FrostySdk.Managers.Entries;

namespace AssetBankPlugin;

public class AntStateAssetDefinition : AssetDefinition
{
	private static HashSet<string> _loadedContexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private static readonly object _loadLock = new object();

	protected static ImageSource antIcon = new ImageSourceConverter().ConvertFromString("pack://application:,,,/AssetBankPlugin;component/Images/AntStateAssetFileType.png") as ImageSource;

	public static void LoadContextualBanks(EbxAssetEntry entry)
	{
		if (entry == null || entry.Bundles.Count == 0)
		{
			return;
		}
		string name = App.AssetManager.GetBundleEntry(entry.Bundles[0]).Name;
		string prefix = (name.Contains("/") ? name.Substring(0, name.LastIndexOf('/') + 1) : name);
		lock (_loadLock)
		{
			if (_loadedContexts.Contains(prefix))
			{
				return;
			}
			foreach (BundleEntry item in from b in App.AssetManager.EnumerateBundles()
				where b.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
				select b)
			{
				LoadAntStateFromBundle(item);
			}
			_loadedContexts.Add(prefix);
		}
	}

	public override void GetSupportedExportTypes(List<AssetExportType> exportTypes)
	{
		exportTypes.Add(new AssetExportType("cast", "Cast Asset Container"));
		exportTypes.Add(new AssetExportType("seanim", "SEAnim Animation File"));
		exportTypes.Add(new AssetExportType("gltf", "GL Transfer format"));
		exportTypes.Add(new AssetExportType("xml", "XML Animation Keyframe Dump"));
		exportTypes.Add(new AssetExportType("smd", "Source StudioMdl Data"));
		exportTypes.Add(new AssetExportType("txt", "Metadata Text Dump"));
		base.GetSupportedExportTypes(exportTypes);
	}

	public override FrostyAssetEditor GetEditor(ILogger logger)
	{
		return new AntStateAssetEditor(logger);
	}

	public override bool Export(EbxAssetEntry entry, string path, string filterType)
	{
		AnimationOptions opt = new AnimationOptions();
		opt.Load();
		if (string.IsNullOrEmpty(opt.ExportSkeletonAsset) && filterType != "txt")
		{
			FrostyMessageBox.Show("Please set an Export Skeleton in Options first.", "Missing Skeleton");
			return false;
		}
		string exportDirectory = Path.GetDirectoryName(path);
		int exportedCount = 0;
		int totalAnimations = 0;
		dynamic rootObject = App.AssetManager.GetEbx(entry).RootObject;
		int num = 0;
		Stream inStream;
		if (rootObject.StreamingGuid == Guid.Empty)
		{
			ResAssetEntry resEntry = App.AssetManager.GetResEntry(entry.Name);
			num = resEntry.Bundles[0];
			inStream = App.AssetManager.GetRes(resEntry);
		}
		else
		{
			dynamic chunkEntry = App.AssetManager.GetChunkEntry(rootObject.StreamingGuid);
			num = chunkEntry.Bundles[0];
			inStream = App.AssetManager.GetChunk(chunkEntry);
		}
		Bank bank;
		using (NativeReader r = new NativeReader(inStream))
		{
			bank = new Bank(r, num);
		}
		totalAnimations = bank.DataNames.Count;
		if (filterType == "txt")
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (KeyValuePair<string, Guid> dataName2 in bank.DataNames)
			{
				AntAsset antAsset = AntRefTable.Get(dataName2.Value);
				if (antAsset != null)
				{
					stringBuilder.AppendLine("[Asset]: " + dataName2.Key);
					stringBuilder.AppendLine("[Type]: " + antAsset.GetType().Name);
					stringBuilder.AppendLine($"[ID]: {antAsset.ID}");
					DumpDictionary(stringBuilder, antAsset.RawData, 1);
					stringBuilder.AppendLine(new string('=', 60));
				}
			}
			File.WriteAllText(path, stringBuilder.ToString());
			FrostyMessageBox.Show("Text dump saved to:\n" + path, "Export Complete");
			return true;
		}
		FrostyTaskWindow.Show("Exporting Animations", "", delegate(FrostyTaskWindow task)
		{
			try
			{
				InternalSkeleton skeleton = null;
				Application.Current.Dispatcher.Invoke(delegate
				{
					LoadContextualBanks(entry);
					EbxAssetEntry ebxEntry = App.AssetManager.GetEbxEntry(opt.ExportSkeletonAsset);
					skeleton = SkeletonAssetExport.ConvertToInternal(App.AssetManager.GetEbx(ebxEntry).RootObject);
				});
				int num2 = 0;
				foreach (KeyValuePair<string, Guid> dataName in bank.DataNames)
				{
					double value = (double)num2 / (double)totalAnimations * 100.0;
					task.Update(dataName.Key, value);
					Dictionary<string, BoneChannelType> channels = null;
					AnimationAsset anim = null;
					string finalName = dataName.Key;
					Application.Current.Dispatcher.Invoke(delegate
					{
						if (AntRefTable.Get(dataName.Value) is AnimationAsset animationAsset)
						{
							anim = animationAsset;
							AntAsset antAsset2 = AntRefTable.Refs.Values.FirstOrDefault(delegate(AntAsset a)
							{
								if (a.AssetType == "ClipControllerAsset")
								{
									Guid property = a.GetProperty<Guid>("Anim");
									Guid[] propertyArray = a.GetPropertyArray<Guid>("Anims");
									if (!(property == dataName.Value))
									{
										if (propertyArray != null)
										{
											return Array.IndexOf(propertyArray, dataName.Value) != -1;
										}
										return false;
									}
									return true;
								}
								return a.AssetType == "ClipControllerData" && a.GetProperty<Guid>("Anim") == dataName.Value;
							});
							if (antAsset2 != null)
							{
								finalName = antAsset2.Name;
							}
							anim.Name = finalName;
							channels = anim.GetChannels(anim.ChannelToDofAsset);
						}
					});
					if (anim != null && channels != null && channels.Count > 0)
					{
						anim.Channels = channels;
						InternalAnimation intern = anim.ConvertToInternal();
						if (intern != null)
						{
							Application.Current.Dispatcher.Invoke(delegate
							{
								if (filterType == "cast")
								{
									new AnimationExporterCAST().Export(intern, skeleton, exportDirectory);
								}
								else
								{
									new AnimationExporterSEANIM().Export(intern, skeleton, exportDirectory);
								}
							});
							exportedCount++;
						}
					}
					num2++;
				}
				App.Logger.Log($"[Export] Successfully exported {exportedCount} animations from {entry.Name}");
			}
			catch (Exception ex)
			{
				App.Logger.LogError("[Export] Failed: " + ex.Message);
			}
		});
		FrostyMessageBox.Show($"Successfully exported {exportedCount} animations\nfrom '{entry.Filename}'\n\nDestination:\n{exportDirectory}", "Export Complete");
		return true;
	}

	public static void LoadAntStateFromBundle(BundleEntry bundle)
	{
		foreach (ResAssetEntry item in from resAssetEntry in App.AssetManager.EnumerateRes(bundle)
			where resAssetEntry.ResType == 1369688147 || resAssetEntry.ResType == 3961224180u
			select resAssetEntry)
		{
			using Stream inStream = App.AssetManager.GetRes(item);
			using NativeReader r = new NativeReader(inStream);
			new Bank(r, App.AssetManager.GetBundleId(bundle));
		}
	}

	private void DumpDictionary(StringBuilder sb, Dictionary<string, object> data, int indent)
	{
		if (data == null)
		{
			return;
		}
		string text = new string(' ', indent * 4);
		foreach (KeyValuePair<string, object> datum in data)
		{
			if (datum.Key.Equals("Data", StringComparison.OrdinalIgnoreCase) || datum.Key.Equals("ConstantPalette", StringComparison.OrdinalIgnoreCase))
			{
				if (datum.Value is Array array)
				{
					sb.AppendLine($"{text}{datum.Key} (Array, Count: {array.Length}) [Content Omitted]");
				}
				else
				{
					sb.AppendLine($"{text}{datum.Key}: {datum.Value} [Content Omitted]");
				}
			}
			else if (datum.Value is Dictionary<string, object> data2)
			{
				sb.AppendLine(text + datum.Key + " (Block):");
				DumpDictionary(sb, data2, indent + 1);
			}
			else if (datum.Value is Array array2)
			{
				sb.AppendLine($"{text}{datum.Key} (Array, Count: {array2.Length}):");
				for (int i = 0; i < array2.Length; i++)
				{
					object value = array2.GetValue(i);
					if (value is Dictionary<string, object> data3)
					{
						DumpDictionary(sb, data3, indent + 1);
					}
					else
					{
						sb.AppendLine($"{text}  [{i}]: {value}");
					}
				}
			}
			else
			{
				sb.AppendLine($"{text}{datum.Key}: {datum.Value}");
			}
		}
	}

	public override ImageSource GetIcon()
	{
		return antIcon;
	}
}
