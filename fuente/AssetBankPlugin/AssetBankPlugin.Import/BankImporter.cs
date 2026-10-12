using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AssetBankPlugin.Ant;
using AssetBankPlugin.Formats;
using Assimp;
using Frosty.Core;
using FrostySdk;
using FrostySdk.IO;
using FrostySdk.Managers.Entries;

namespace AssetBankPlugin.Import;

public static class BankImporter
{
	private sealed class Candidate
	{
		public EbxAssetEntry Ebx;

		public ResAssetEntry Res;

		public ChunkAssetEntry Chunk;

		public int BundleId;
	}

	public sealed class Report
	{
		public readonly List<string> Written = new List<string>();

		public readonly List<string> Failed = new List<string>();

		public int Scanned;

		public int NotFound;

		public int Samples;
	}

	private static string DescribeSimilar(List<VbrAnimationAsset> anims, string wanted)
	{
		string text = (wanted ?? "").Trim();
		if (text.Length < 4 || anims.Count == 0)
		{
			return string.Empty;
		}
		string word = text.Split(' ')[0];
		if (word.Length < 4)
		{
			word = text;
		}
		List<string> list = (from item in anims
			where item.Name != null && item.Name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0
			select item.Name).Distinct().Take(3).ToList();
		if (list.Count <= 0)
		{
			return string.Empty;
		}
		return "; parecidas: " + string.Join(", ", list);
	}

	private static VbrAnimationAsset Find(List<VbrAnimationAsset> anims, string wanted)
	{
		if (anims.Count == 0)
		{
			return null;
		}
		string needle = (wanted ?? "").Trim();
		VbrAnimationAsset vbrAnimationAsset = anims.FirstOrDefault((VbrAnimationAsset item) => item.Name == needle);
		if (vbrAnimationAsset != null)
		{
			return vbrAnimationAsset;
		}
		vbrAnimationAsset = anims.FirstOrDefault((VbrAnimationAsset item) => string.Equals(item.Name, needle, StringComparison.OrdinalIgnoreCase));
		if (vbrAnimationAsset != null)
		{
			return vbrAnimationAsset;
		}
		if (needle.EndsWith("]", StringComparison.Ordinal))
		{
			int num = needle.LastIndexOf(" [", StringComparison.Ordinal);
			if (num > 0)
			{
				needle = needle.Substring(0, num);
				vbrAnimationAsset = anims.FirstOrDefault((VbrAnimationAsset item) => item.Name == needle);
				if (vbrAnimationAsset != null)
				{
					return vbrAnimationAsset;
				}
			}
		}
		string corto = SinSufijoAnim(needle);
		return anims.FirstOrDefault((VbrAnimationAsset item) => item.Name != null && string.Equals(SinSufijoAnim(item.Name.Trim()), corto, StringComparison.OrdinalIgnoreCase));
	}

	private static string SinSufijoAnim(string name)
	{
		if (!name.EndsWith(" Anim", StringComparison.OrdinalIgnoreCase))
		{
			return name;
		}
		return name.Substring(0, name.Length - 5).TrimEnd();
	}

	private static List<EbxAssetEntry> AntStateEntries(Action<string> log)
	{
		List<EbxAssetEntry> list = App.AssetManager.EnumerateEbx("AntStateAsset", modifiedOnly: false, includeLinked: true).ToList();
		if (list.Count > 0)
		{
			return list;
		}
		log?.Invoke("[ImportAll] el filtro por tipo no devuelve nada: se busca por nombre.");
		return (from item in App.AssetManager.EnumerateEbx("", modifiedOnly: false, includeLinked: true)
			where item?.Name.EndsWith("_win32_antstate", StringComparison.OrdinalIgnoreCase) ?? false
			select item).ToList();
	}

	public static List<string> DescribeCandidates()
	{
		List<string> list = new List<string>();
		foreach (EbxAssetEntry item in AntStateEntries(null))
		{
			if (Resolve(item) != null)
			{
				list.Add(item.Name);
			}
		}
		list.Sort(StringComparer.OrdinalIgnoreCase);
		return list;
	}

	public static Report Import(string filePath, string animationName, Action<string> log, Action<string, double> progress, Func<bool> cancelled)
	{
		return Import(filePath, animationName, null, log, progress, cancelled);
	}

	public static Report Import(string filePath, string animationName, Func<string, bool> bankFilter, Action<string> log, Action<string, double> progress, Func<bool> cancelled)
	{
		if (string.IsNullOrEmpty(filePath))
		{
			throw new ArgumentNullException("filePath");
		}
		using AssimpContext assimpContext = new AssimpContext();
		Scene scene = assimpContext.ImportFile(filePath, PostProcessSteps.None);
		if (!scene.HasAnimations)
		{
			throw new InvalidOperationException("The imported file contains no animations.");
		}
		Report report = new Report();
		List<Candidate> list = new List<Candidate>();
		foreach (EbxAssetEntry item in AntStateEntries(log))
		{
			if (bankFilter != null && !bankFilter(item.Name))
			{
				continue;
			}
			Candidate candidate = Resolve(item);
			if (candidate != null)
			{
				list.Add(candidate);
			}
		}
		int count = list.Count;
		log?.Invoke("[ImportAll] bancos candidatos: " + count + "; animacion solicitada: '" + animationName + "'");
		for (int i = 0; i < count; i++)
		{
			if (cancelled != null && cancelled())
			{
				log?.Invoke("[ImportAll] cancelado por el usuario.");
				break;
			}
			Candidate candidate2 = list[i];
			string name = candidate2.Ebx.Name;
			progress?.Invoke(name, (double)i / (double)count * 100.0);
			report.Scanned++;
			try
			{
				ProcessOne(scene, animationName, candidate2, report, log);
			}
			catch (Exception ex)
			{
				report.Failed.Add(name + ": " + ex.Message);
				log?.Invoke("[ImportAll] ERROR " + name + ": " + ex.Message);
			}
			GC.Collect();
			GC.WaitForPendingFinalizers();
		}
		GC.Collect();
		return report;
	}

	private static Candidate Resolve(EbxAssetEntry entry)
	{
		Candidate candidate = new Candidate();
		candidate.Ebx = entry;
		Guid guid = Guid.Empty;
		try
		{
			EbxAsset ebx = App.AssetManager.GetEbx(entry);
			if (ebx != null && ebx.RootObject != null)
			{
				guid = (Guid)((dynamic)ebx.RootObject).StreamingGuid;
			}
		}
		catch
		{
			return null;
		}
		if (guid == Guid.Empty)
		{
			ResAssetEntry resEntry = App.AssetManager.GetResEntry(entry.Name);
			if (resEntry == null || resEntry.Bundles.Count == 0)
			{
				return null;
			}
			candidate.Res = resEntry;
			candidate.BundleId = resEntry.Bundles[0];
		}
		else
		{
			ChunkAssetEntry chunkEntry = App.AssetManager.GetChunkEntry(guid);
			if (chunkEntry == null || chunkEntry.Bundles.Count == 0)
			{
				return null;
			}
			candidate.Chunk = chunkEntry;
			candidate.BundleId = chunkEntry.Bundles[0];
		}
		return candidate;
	}

	private static void ProcessOne(Scene scene, string wanted, Candidate candidate, Report report, Action<string> log)
	{
		byte[] array = ReadBytes(candidate);
		if (array == null || array.Length < 1024)
		{
			return;
		}
		ConcurrentDictionary<Guid, AntAsset> refs = AntRefTable.Refs;
		ConcurrentDictionary<Guid, Guid> internalRefs = AntRefTable.InternalRefs;
		AntRefTable.Refs = new ConcurrentDictionary<Guid, AntAsset>();
		AntRefTable.InternalRefs = new ConcurrentDictionary<Guid, Guid>();
		int value;
		try
		{
			Bank bank;
			using (NativeReader r = new NativeReader(new MemoryStream(array)))
			{
				bank = new Bank(r, candidate.BundleId);
			}
			List<VbrAnimationAsset> list = AntRefTable.Refs.Values.OfType<VbrAnimationAsset>().ToList();
			VbrAnimationAsset vbrAnimationAsset = Find(list, wanted);
			if (vbrAnimationAsset == null)
			{
				report.NotFound++;
				if (report.Samples < 5)
				{
					report.Samples++;
					if (log != null)
					{
						string[] obj = new string[10]
						{
							"[ImportAll] '",
							wanted,
							"' no esta en ",
							candidate.Ebx.Name,
							" (",
							null,
							null,
							null,
							null,
							null
						};
						value = list.Count;
						obj[5] = value.ToString();
						obj[6] = " animaciones";
						obj[7] = ((list.Count > 0) ? (", p.ej. '" + list[0].Name + "'") : "");
						obj[8] = ")";
						obj[9] = DescribeSimilar(list, wanted);
						log(string.Concat(obj));
					}
				}
			}
			else
			{
				// Some level banks (gamelogic_shared, rush...) hold nothing but animations: the rig,
				// the layouts with the bone names and the clip controllers are in the other banks of
				// the same level. Without them every bone would be written as a constant.
				if (!AntRefTable.Refs.ContainsKey(vbrAnimationAsset.ChannelToDofAsset))
				{
					int loaded = LoadLevelBanks(candidate.BundleId);
					log?.Invoke("[ImportAll] " + candidate.Ebx.Name + " solo trae animaciones: cargados " + loaded + " bundles de su nivel para resolver los huesos.");
				}
				if (vbrAnimationAsset.OrderedChannels == null || vbrAnimationAsset.OrderedChannels.Count == 0)
				{
					vbrAnimationAsset.Channels = vbrAnimationAsset.GetChannels(vbrAnimationAsset.ChannelToDofAsset);
				}
				byte[] array2 = AnimationImporter.ImportLegacy(scene, array, vbrAnimationAsset, bank.Classes);
				Write(candidate, array2);
				List<string> written = report.Written;
				string name = candidate.Ebx.Name;
				value = array2.Length / 1024;
				written.Add(name + " (" + value + " KB)");
				if (log != null)
				{
					string[] obj2 = new string[5]
					{
						"[ImportAll] escrito: ",
						candidate.Ebx.Name,
						" -> ",
						null,
						null
					};
					value = array2.Length;
					obj2[3] = value.ToString("N0");
					obj2[4] = " bytes";
					log(string.Concat(obj2));
				}
			}
		}
		finally
		{
			foreach (Guid key in AntRefTable.Refs.Keys)
			{
				if (!refs.ContainsKey(key))
				{
					Cache.AntStateBundleIndices.TryRemove(key, out value);
				}
			}
			AntRefTable.Refs = refs;
			AntRefTable.InternalRefs = internalRefs;
		}
	}

	/// <summary>
	/// Reads into the reference table every AntState bank of the level a bundle belongs to (the
	/// bundles that share its folder). Returns how many bundles were read.
	/// </summary>
	public static int LoadLevelBanks(int bundleId)
	{
		BundleEntry bundleEntry = App.AssetManager.GetBundleEntry(bundleId);
		if (bundleEntry == null)
		{
			return 0;
		}
		string name = bundleEntry.Name;
		string prefix = (name.Contains("/") ? name.Substring(0, name.LastIndexOf('/') + 1) : name);
		// The assets that are already in the table (the bank that is open, with its unsaved
		// imports) must stay as they are: reading their bundle again would replace them.
		KeyValuePair<Guid, AntAsset>[] array = AntRefTable.Refs.ToArray();
		int num = 0;
		foreach (BundleEntry item in App.AssetManager.EnumerateBundles().ToList())
		{
			if (item.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				AntStateAssetDefinition.LoadAntStateFromBundle(item);
				num++;
			}
		}
		KeyValuePair<Guid, AntAsset>[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			KeyValuePair<Guid, AntAsset> keyValuePair = array2[i];
			AntRefTable.Refs[keyValuePair.Key] = keyValuePair.Value;
		}
		return num;
	}

	private static byte[] ReadBytes(Candidate candidate)
	{
		Stream stream = null;
		try
		{
			stream = ((candidate.Res != null) ? App.AssetManager.GetRes(candidate.Res) : App.AssetManager.GetChunk(candidate.Chunk));
			if (stream == null)
			{
				return null;
			}
			using MemoryStream memoryStream = new MemoryStream();
			stream.CopyTo(memoryStream);
			return memoryStream.ToArray();
		}
		finally
		{
			stream?.Dispose();
		}
	}

	private static void Write(Candidate candidate, byte[] data)
	{
		AnimationOptions animationOptions = new AnimationOptions();
		animationOptions.Load();
		CompressionType compressionType = animationOptions.GetSelectedCompressionType();
		if (compressionType == CompressionType.None)
		{
			compressionType = CompressionType.Default;
		}
		if (candidate.Res != null)
		{
			byte[] array = Utils.CompressFile(data, null, (ResourceType)candidate.Res.ResType, compressionType);
			if (candidate.Res.ModifiedEntry == null)
			{
				candidate.Res.ModifiedEntry = new ModifiedAssetEntry();
			}
			candidate.Res.ModifiedEntry.Data = array;
			candidate.Res.ModifiedEntry.OriginalSize = data.Length;
			candidate.Res.ModifiedEntry.Sha1 = Sha1Of(array);
			candidate.Res.IsDirty = true;
		}
		else
		{
			App.AssetManager.ModifyChunk(candidate.Chunk.Id, data);
			if (candidate.Chunk.ModifiedEntry == null || candidate.Chunk.ModifiedEntry.Data == null)
			{
				throw new InvalidOperationException("the chunk is not in the asset manager");
			}
			candidate.Chunk.ModifiedEntry.OriginalSize = (uint)data.Length;
			candidate.Chunk.IsDirty = true;
		}
		candidate.Ebx.IsDirty = true;
		candidate.Ebx.LinkAsset((AssetEntry)(((object)candidate.Chunk) ?? ((object)candidate.Res)));
	}

	private static Sha1 Sha1Of(byte[] buffer)
	{
		using SHA1 sHA = SHA1.Create();
		return new Sha1(sHA.ComputeHash(buffer));
	}
}
