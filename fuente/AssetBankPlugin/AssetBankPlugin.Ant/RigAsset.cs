using System;
using System.Collections;
using System.Collections.Generic;

namespace AssetBankPlugin.Ant;

public class RigAsset : AntAsset
{
	public override string Name { get; set; }

	public override Guid ID { get; set; }

	public Guid FeatureCollection { get; set; }

	public Guid Skeleton { get; set; }

	public Guid[] DofSetLists { get; set; }

	public Guid[] RigDofSets { get; set; }

	public uint[] DofIds { get; set; }

	public uint[] DofSetIdIndices { get; set; }

	public List<DefaultDofVector4> DefaultVector4Values { get; set; }

	public List<DefaultDofVector3> DefaultVector3Values { get; set; }

	public List<DefaultDofFloat> DefaultFloatValues { get; set; }

	public List<DefaultDofInt> DefaultIntValues { get; set; }

	public override void SetData(Dictionary<string, object> data)
	{
		ParseBasicData(data);
		if (data.TryGetValue("FeatureCollection", out var value))
		{
			FeatureCollection = SafeGuid(value);
		}
		if (data.TryGetValue("Skeleton", out var value2))
		{
			Skeleton = SafeGuid(value2);
		}
		if (data.TryGetValue("DofSetLists", out var value3))
		{
			DofSetLists = ConvertArray<Guid>(value3);
		}
		if (data.TryGetValue("RigDofSets", out var value4))
		{
			RigDofSets = ConvertArray<Guid>(value4);
		}
		if (data.TryGetValue("DofIds", out var value5))
		{
			DofIds = ConvertArray<uint>(value5);
		}
		if (data.TryGetValue("DofSetIdIndices", out var value6))
		{
			DofSetIdIndices = ConvertArray<uint>(value6);
		}
		if (data.TryGetValue("DefaultVector4Values", out var value7))
		{
			DefaultVector4Values = ParseDefaultList(value7, ParseDefaultVector4);
		}
		if (data.TryGetValue("DefaultVector3Values", out var value8))
		{
			DefaultVector3Values = ParseDefaultList(value8, ParseDefaultVector3);
		}
		if (data.TryGetValue("DefaultFloatValues", out var value9))
		{
			DefaultFloatValues = ParseDefaultList(value9, ParseDefaultFloat);
		}
		if (data.TryGetValue("DefaultIntValues", out var value10))
		{
			DefaultIntValues = ParseDefaultList(value10, ParseDefaultInt);
		}
	}

	private List<T> ParseDefaultList<T>(object obj, Func<Dictionary<string, object>, T> parser)
	{
		List<T> list = new List<T>();
		if (obj is object[] array)
		{
			object[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				if (array2[i] is Dictionary<string, object> arg)
				{
					T val = parser(arg);
					if (val != null)
					{
						list.Add(val);
					}
				}
			}
		}
		else if (obj is IEnumerable enumerable)
		{
			foreach (object item in enumerable)
			{
				if (item is Dictionary<string, object> arg2)
				{
					T val2 = parser(arg2);
					if (val2 != null)
					{
						list.Add(val2);
					}
				}
			}
		}
		return list;
	}

	private DefaultDofVector4 ParseDefaultVector4(Dictionary<string, object> dict)
	{
		DefaultDofVector4 defaultDofVector = new DefaultDofVector4();
		if (dict.TryGetValue("DofId", out var value))
		{
			defaultDofVector.DofId = Convert.ToUInt32(value);
		}
		if (dict.TryGetValue("Value", out var value2))
		{
			if (value2 is float[] value3)
			{
				defaultDofVector.Value = value3;
			}
			else if (value2 is object[] array && array.Length == 4)
			{
				defaultDofVector.Value = new float[4]
				{
					Convert.ToSingle(array[0]),
					Convert.ToSingle(array[1]),
					Convert.ToSingle(array[2]),
					Convert.ToSingle(array[3])
				};
			}
		}
		return defaultDofVector;
	}

	private DefaultDofVector3 ParseDefaultVector3(Dictionary<string, object> dict)
	{
		DefaultDofVector3 defaultDofVector = new DefaultDofVector3();
		if (dict.TryGetValue("DofId", out var value))
		{
			defaultDofVector.DofId = Convert.ToUInt32(value);
		}
		if (dict.TryGetValue("Value", out var value2))
		{
			if (value2 is float[] value3)
			{
				defaultDofVector.Value = value3;
			}
			else if (value2 is object[] array && array.Length == 3)
			{
				defaultDofVector.Value = new float[3]
				{
					Convert.ToSingle(array[0]),
					Convert.ToSingle(array[1]),
					Convert.ToSingle(array[2])
				};
			}
		}
		return defaultDofVector;
	}

	private DefaultDofFloat ParseDefaultFloat(Dictionary<string, object> dict)
	{
		DefaultDofFloat defaultDofFloat = new DefaultDofFloat();
		if (dict.TryGetValue("DofId", out var value))
		{
			defaultDofFloat.DofId = Convert.ToUInt32(value);
		}
		if (dict.TryGetValue("Value", out var value2))
		{
			defaultDofFloat.Value = Convert.ToSingle(value2);
		}
		return defaultDofFloat;
	}

	private DefaultDofInt ParseDefaultInt(Dictionary<string, object> dict)
	{
		DefaultDofInt defaultDofInt = new DefaultDofInt();
		if (dict.TryGetValue("DofId", out var value))
		{
			defaultDofInt.DofId = Convert.ToUInt32(value);
		}
		if (dict.TryGetValue("Value", out var value2))
		{
			defaultDofInt.Value = Convert.ToInt32(value2);
		}
		return defaultDofInt;
	}
}
