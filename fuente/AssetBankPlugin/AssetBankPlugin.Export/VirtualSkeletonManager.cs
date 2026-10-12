using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Frosty.Core;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Managers.Entries;

namespace AssetBankPlugin.Export;

public static class VirtualSkeletonManager
{
	public static string RegisterVirtualSkeleton(dynamic dynamicSkelObj)
	{
		string text = "ant_virtual_skeleton_" + Guid.NewGuid().ToString("N");
		Guid guid = Guid.NewGuid();
		EbxAsset ebxAsset = (EbxAsset)Activator.CreateInstance(typeof(EbxAsset), nonPublic: true);
		FieldInfo fieldInfo = typeof(EbxAsset).GetField("objects", BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? typeof(EbxAsset).GetField("m_objects", BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		if (fieldInfo != null)
		{
			IList list = fieldInfo.GetValue(ebxAsset) as IList;
			if (list == null)
			{
				list = new List<object>();
				fieldInfo.SetValue(ebxAsset, list);
			}
			list.Clear();
			list.Add(dynamicSkelObj);
		}
		VirtualSkeletonManager.SetMember(ebxAsset, "RootObject", dynamicSkelObj);
		EbxAssetEntry ebxAssetEntry = (EbxAssetEntry)Activator.CreateInstance(typeof(EbxAssetEntry), nonPublic: true);
		SetMember(ebxAssetEntry, "Name", text);
		SetMember(ebxAssetEntry, "Filename", text);
		SetMember(ebxAssetEntry, "Guid", guid);
		SetMember(ebxAssetEntry, "Type", "SkeletonAsset");
		ModifiedAssetEntry modifiedAssetEntry = new ModifiedAssetEntry();
		modifiedAssetEntry.DataObject = ebxAsset;
		ebxAssetEntry.ModifiedEntry = modifiedAssetEntry;
		AssetManager assetManager = App.AssetManager;
		Type type = assetManager.GetType();
		FieldInfo field = type.GetField("m_ebxList", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		if (field != null && field.GetValue(assetManager) is IDictionary dictionary)
		{
			dictionary[text.ToLower()] = ebxAssetEntry;
		}
		FieldInfo field2 = type.GetField("m_ebxGuidList", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		if (field2 != null && field2.GetValue(assetManager) is IDictionary dictionary2)
		{
			dictionary2[guid] = ebxAssetEntry;
		}
		return text;
	}

	public static void UnregisterVirtualSkeleton(string virtualName)
	{
		AssetManager assetManager = App.AssetManager;
		FieldInfo field = assetManager.GetType().GetField("m_ebxList", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		if (field != null && field.GetValue(assetManager) is IDictionary dictionary && dictionary.Contains(virtualName.ToLower()))
		{
			dictionary.Remove(virtualName.ToLower());
		}
	}

	private static void SetMember(object obj, string name, object val)
	{
		Type type = obj.GetType();
		PropertyInfo property = type.GetProperty(name, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		if (property != null && property.CanWrite)
		{
			property.SetValue(obj, val);
			return;
		}
		FieldInfo field = type.GetField(name, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		if (field != null)
		{
			field.SetValue(obj, val);
		}
	}
}
