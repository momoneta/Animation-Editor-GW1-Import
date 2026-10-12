using System.Collections.Generic;

namespace AssetBankPlugin.Formats;

public static class PrimitiveTypeMap
{
	private static readonly Dictionary<uint, string> NameMap = new Dictionary<uint, string>
	{
		{ 1u, "Bool" },
		{ 2u, "Int8" },
		{ 3u, "UInt8" },
		{ 4u, "Int16" },
		{ 5u, "UInt16" },
		{ 6u, "Int32" },
		{ 7u, "UInt32" },
		{ 8u, "Int64" },
		{ 9u, "UInt64" },
		{ 10u, "Float" },
		{ 11u, "Vector2" },
		{ 12u, "Vector3" },
		{ 13u, "Vector4" },
		{ 14u, "Quaternion" },
		{ 15u, "Matrix44" },
		{ 17u, "String" },
		{ 18u, "DataRef" },
		{ 19u, "Double" },
		{ 35u, "Key" }
	};

	private static readonly Dictionary<uint, int> SizeMap = new Dictionary<uint, int>
	{
		{ 1u, 1 },
		{ 2u, 1 },
		{ 3u, 1 },
		{ 4u, 2 },
		{ 5u, 2 },
		{ 6u, 4 },
		{ 7u, 4 },
		{ 8u, 8 },
		{ 9u, 8 },
		{ 10u, 4 },
		{ 11u, 8 },
		{ 12u, 12 },
		{ 13u, 16 },
		{ 14u, 16 },
		{ 15u, 64 },
		{ 17u, 8 },
		{ 18u, 8 },
		{ 19u, 8 },
		{ 35u, 8 }
	};

	public static string GetTypeName(uint hash)
	{
		NameMap.TryGetValue(hash, out var value);
		return value;
	}

	public static int GetTypeSize(uint hash)
	{
		if (!SizeMap.TryGetValue(hash, out var value))
		{
			return 0;
		}
		return value;
	}

	public static bool IsPrimitive(uint hash)
	{
		return NameMap.ContainsKey(hash);
	}
}
