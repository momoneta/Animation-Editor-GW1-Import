using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using FrostySdk.IO;

namespace AssetBankPlugin;

public class Cache
{
	public static ConcurrentDictionary<Guid, int> AntStateBundleIndices = new ConcurrentDictionary<Guid, int>();

	public static ConcurrentDictionary<Guid, Guid> AntRefMap = new ConcurrentDictionary<Guid, Guid>();

	public static void ReadState(string path)
	{
		using FileStream inStream = File.OpenRead(path);
		using NativeReader nativeReader = new NativeReader(inStream);
		uint num = nativeReader.ReadUInt();
		for (int i = 0; i < num; i++)
		{
			Guid key = nativeReader.ReadGuid();
			int value = nativeReader.ReadInt();
			AntStateBundleIndices[key] = value;
		}
	}

	public static void WriteState(string path)
	{
		using FileStream inStream = File.OpenWrite(path);
		using NativeWriter nativeWriter = new NativeWriter(inStream);
		KeyValuePair<Guid, int>[] array = AntStateBundleIndices.ToArray();
		nativeWriter.Write(array.Length);
		KeyValuePair<Guid, int>[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			KeyValuePair<Guid, int> keyValuePair = array2[i];
			nativeWriter.Write(keyValuePair.Key);
			nativeWriter.Write(keyValuePair.Value);
		}
	}

	public static void ReadMap(string path)
	{
		using FileStream inStream = File.OpenRead(path);
		using NativeReader nativeReader = new NativeReader(inStream);
		uint num = nativeReader.ReadUInt();
		for (int i = 0; i < num; i++)
		{
			Guid key = nativeReader.ReadGuid();
			Guid value = nativeReader.ReadGuid();
			AntRefMap[key] = value;
		}
	}

	public static void WriteMap(string path)
	{
		using FileStream inStream = File.OpenWrite(path);
		using NativeWriter nativeWriter = new NativeWriter(inStream);
		KeyValuePair<Guid, Guid>[] array = AntRefMap.ToArray();
		nativeWriter.Write(array.Length);
		KeyValuePair<Guid, Guid>[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			KeyValuePair<Guid, Guid> keyValuePair = array2[i];
			nativeWriter.Write(keyValuePair.Key);
			nativeWriter.Write(keyValuePair.Value);
		}
	}
}
