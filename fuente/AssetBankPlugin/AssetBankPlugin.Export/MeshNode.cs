using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetBankPlugin.Export;

public class MeshNode : CastNode
{
	public string Name => GetPropertyVal<string>("n");

	public int UVLayerCount => GetPropertyVal("ul", (byte)0);

	public int MaximumWeightInfluence => GetPropertyVal("mi", (byte)0);

	public string SkinningMethod => GetPropertyVal("sm", "linear");

	public List<uint> FaceBuffer => GetPropertyArray<uint>("f");

	public List<float> VertexPositionBuffer => GetPropertyArray<float>("vp");

	public List<float> VertexNormalBuffer => GetPropertyArray<float>("vn");

	public List<float> VertexTangentBuffer => GetPropertyArray<float>("vt");

	public List<uint> VertexWeightBoneBuffer => GetPropertyArray<uint>("wb");

	public List<float> VertexWeightValueBuffer => GetPropertyArray<float>("wv");

	public MeshNode()
		: base(1752393069u)
	{
	}

	public void SetName(string name)
	{
		SetPropertyVal("n", "s", name);
	}

	public void SetUVLayerCount(byte count)
	{
		SetPropertyVal("ul", "b", count);
	}

	public int ColorLayerCount()
	{
		if (base.Properties.TryGetValue("cl", out var value) && value.Values.Count > 0)
		{
			return Convert.ToInt32(value.Values[0]);
		}
		if (base.Properties.ContainsKey("vc"))
		{
			return 1;
		}
		return 0;
	}

	public void SetColorLayerCount(byte count)
	{
		SetPropertyVal("cl", "b", count);
	}

	public List<object> VertexColorLayerBuffer(int index)
	{
		if (base.Properties.TryGetValue($"c{index}", out var value))
		{
			return value.Values;
		}
		if (index == 0 && base.Properties.TryGetValue("vc", out var value2))
		{
			return value2.Values;
		}
		return null;
	}

	public void SetVertexColorBuffer(int index, IEnumerable<uint> values)
	{
		SetPropertyArray($"c{index}", "i", values);
	}

	public void SetVertexColorBuffer(int index, IEnumerable<float[]> values)
	{
		IEnumerable<float> values2 = values.SelectMany((float[] v) => v);
		SetPropertyArray($"c{index}", "4v", values2);
	}

	public bool VertexColorLayerBufferPacked(int index)
	{
		if (base.Properties.TryGetValue($"c{index}", out var value))
		{
			return value.PropertyType.Identifier == "i";
		}
		return true;
	}

	public void SetMaximumWeightInfluence(byte maximum)
	{
		SetPropertyVal("mi", "b", maximum);
	}

	public void SetSkinningMethod(string method)
	{
		SetPropertyVal("sm", "s", method);
	}

	public void SetFaceBuffer(IEnumerable<uint> values)
	{
		SetPropertyArray("f", Cast.CastTypeForMaximum(values), values);
	}

	public void SetVertexPositionBuffer(IEnumerable<float> values)
	{
		SetPropertyArray("vp", "3v", values);
	}

	public void SetVertexNormalBuffer(IEnumerable<float> values)
	{
		SetPropertyArray("vn", "3v", values);
	}

	public void SetVertexTangentBuffer(IEnumerable<float> values)
	{
		SetPropertyArray("vt", "3v", values);
	}

	public List<float> VertexUVLayerBuffer(int index)
	{
		return GetPropertyArray<float>($"u{index}");
	}

	public void SetVertexUVLayerBuffer(int index, IEnumerable<float> values)
	{
		SetPropertyArray($"u{index}", "2v", values);
	}

	public void SetVertexWeightBoneBuffer(IEnumerable<uint> values)
	{
		SetPropertyArray("wb", Cast.CastTypeForMaximum(values), values);
	}

	public void SetVertexWeightValueBuffer(IEnumerable<float> values)
	{
		SetPropertyArray("wv", "f", values);
	}

	public void SetMaterial(ulong hash)
	{
		SetPropertyVal("m", "l", hash);
	}
}
