using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using AssetBankPlugin.Extensions;
using AssetBankPlugin.Formats;
using AssetBankPlugin.Formats.GenericData;
using AssetBankPlugin.Formats.GenericData2;
using FrostySdk.IO;

namespace AssetBankPlugin.Ant;

public abstract class AntAsset
{
	private static readonly Dictionary<string, Func<AntAsset>> s_activatorCache = new Dictionary<string, Func<AntAsset>>();

	public abstract string Name { get; set; }

	public abstract Guid ID { get; set; }

	public string AssetType { get; set; } = "Unknown";

	public Bank Bank { get; set; }

	public Dictionary<string, object> RawData { get; set; }

	public abstract void SetData(Dictionary<string, object> data);

	public T GetProperty<T>(string propertyName, T defaultValue = default(T))
	{
		if (RawData == null || !RawData.TryGetValue(propertyName, out var value) || value == null)
		{
			return defaultValue;
		}
		if (value is T)
		{
			return (T)value;
		}
		try
		{
			if (typeof(T) == typeof(Guid))
			{
				return (T)(object)SafeGuid(value);
			}
			if (typeof(T) == typeof(string))
			{
				return (T)(object)SafeString(value);
			}
			return (T)Convert.ChangeType(value, typeof(T));
		}
		catch
		{
			return defaultValue;
		}
	}

	public T[] GetPropertyArray<T>(string propertyName)
	{
		if (RawData == null || !RawData.TryGetValue(propertyName, out var value) || value == null)
		{
			return Array.Empty<T>();
		}
		return ConvertArray<T>(value);
	}

	protected Guid SafeGuid(object obj)
	{
		if (obj == null)
		{
			return Guid.Empty;
		}
		if (obj is Guid)
		{
			return (Guid)obj;
		}
		if (obj is string text)
		{
			if (text.Length <= 16 && ulong.TryParse(text, NumberStyles.HexNumber, null, out var result))
			{
				byte[] array = new byte[16];
				BitConverter.GetBytes(result).CopyTo(array, 0);
				return new Guid(array);
			}
			if (Guid.TryParse(text, out var result2))
			{
				return result2;
			}
		}
		return Guid.Empty;
	}

	protected string SafeString(object obj)
	{
		if (obj == null)
		{
			return "unknown";
		}
		if (obj is string result)
		{
			return result;
		}
		try
		{
			if (obj is byte[] bytes)
			{
				return Encoding.ASCII.GetString(bytes).TrimEnd(default(char));
			}
			if (obj is object[] array)
			{
				byte[] array2 = new byte[array.Length];
				for (int i = 0; i < array.Length; i++)
				{
					array2[i] = Convert.ToByte(array[i]);
				}
				return Encoding.ASCII.GetString(array2).TrimEnd(default(char));
			}
		}
		catch
		{
			return "unknown_format";
		}
		return obj.ToString();
	}

	protected T[] ConvertArray<T>(object inObj)
	{
		if (inObj == null)
		{
			return Array.Empty<T>();
		}
		if (inObj is T[] result)
		{
			return result;
		}
		if (inObj is object[] array)
		{
			T[] array2 = new T[array.Length];
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i] is T val)
				{
					array2[i] = val;
				}
				else if (typeof(T) == typeof(Guid))
				{
					array2[i] = (T)(object)SafeGuid(array[i]);
				}
				else
				{
					array2[i] = (T)Convert.ChangeType(array[i], typeof(T));
				}
			}
			return array2;
		}
		if (inObj is IEnumerable enumerable)
		{
			List<T> list = ((inObj is ICollection collection) ? new List<T>(collection.Count) : new List<T>());
			foreach (object item2 in enumerable)
			{
				if (item2 is T item)
				{
					list.Add(item);
				}
				else if (typeof(T) == typeof(Guid))
				{
					list.Add((T)(object)SafeGuid(item2));
				}
				else
				{
					list.Add((T)Convert.ChangeType(item2, typeof(T)));
				}
			}
			return list.ToArray();
		}
		return Array.Empty<T>();
	}

	protected void ParseBasicData(Dictionary<string, object> data)
	{
		if (data.TryGetValue("__name", out var value))
		{
			Name = value.ToString();
		}
		else
		{
			Name = "Unknown_Asset";
		}
		object value3;
		if (data.TryGetValue("__guid", out var value2))
		{
			ID = SafeGuid(value2);
		}
		else if (data.TryGetValue("__key", out value3))
		{
			ID = SafeGuid(value3);
		}
		if (ID == Guid.Empty)
		{
			ID = Guid.NewGuid();
		}
	}

	public static AntAsset Deserialize(NativeReader r, SectionData section, Dictionary<uint, GenericClass> classes, Bank bank)
	{
		r.BaseStream.Position = section.DataOffset;
		r.ReadDataHeader(section.Endianness, out var hash, out var type, out var offset);
		Dictionary<string, object> dictionary = section.ReadValues(r, classes, section.DataOffset + offset, type);
		if (dictionary.TryGetValue("__base", out var value) && (long)value != 0L)
		{
			r.BaseStream.Position = section.DataOffset + (long)value;
			r.ReadDataHeader(section.Endianness, out hash, out var type2, out var offset2);
			Dictionary<string, object> dictionary2 = section.ReadValues(r, classes, section.DataOffset + offset2 + Convert.ToUInt32(value), type2);
			AntAsset value2 = CreateAndRegisterAsset(classes[type2].Name, dictionary2, bank);
			dictionary["__base"] = value2;
			foreach (KeyValuePair<string, object> item in dictionary2)
			{
				if (!dictionary.ContainsKey(item.Key))
				{
					dictionary.Add(item.Key, item.Value);
				}
			}
		}
		return CreateAndRegisterAsset(classes[type].Name, dictionary, bank);
	}

	public static AntAsset Deserialize(NativeReader r, SectionData2 section, Dictionary<uint, GenericClass2> classes, Bank bank, long objectHeaderOffset)
	{
		Dictionary<string, object> dictionary = section.ReadObjectAt(objectHeaderOffset, classes);
		if (dictionary == null)
		{
			return null;
		}
		NativeReader patchedReader = section.GetPatchedReader();
		patchedReader.BaseStream.Position = objectHeaderOffset - section.DataOffset;
		uint key = patchedReader.ReadUInt(section.Endianness);
		if (!classes.TryGetValue(key, out var value))
		{
			return null;
		}
		return CreateAndRegisterAsset(value.Name, dictionary, bank);
	}

	private static AntAsset CreateAndRegisterAsset(string typeName, Dictionary<string, object> values, Bank bank)
	{
		if (!s_activatorCache.TryGetValue(typeName, out var value))
		{
			Type type = Type.GetType("AssetBankPlugin.Ant." + typeName);
			if (type == null && (typeName.EndsWith("AnimationAsset") || typeName.EndsWith("AnimAsset")))
			{
				type = typeof(AnimationAsset);
			}
			if (type != null)
			{
				value = Expression.Lambda<Func<AntAsset>>(Expression.New(type), Array.Empty<ParameterExpression>()).Compile();
			}
			s_activatorCache[typeName] = value;
		}
		if (value != null)
		{
			try
			{
				AntAsset antAsset = value();
				antAsset.AssetType = typeName;
				antAsset.Bank = bank;
				antAsset.RawData = values;
				antAsset.SetData(values);
				AntRefTable.Add(antAsset);
				return antAsset;
			}
			catch
			{
			}
		}
		GenericAntAsset genericAntAsset = new GenericAntAsset();
		genericAntAsset.AssetType = typeName;
		genericAntAsset.Bank = bank;
		genericAntAsset.RawData = values;
		genericAntAsset.SetData(values);
		AntRefTable.Add(genericAntAsset);
		return genericAntAsset;
	}
}
