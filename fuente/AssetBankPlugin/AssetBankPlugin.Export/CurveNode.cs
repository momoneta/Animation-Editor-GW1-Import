using System.Collections.Generic;
using System.Linq;

namespace AssetBankPlugin.Export;

public class CurveNode : CastNode
{
	public string NodeName => GetPropertyVal<string>("nn");

	public string KeyPropertyName => GetPropertyVal<string>("kp");

	public List<uint> KeyFrameBuffer => GetPropertyArray<uint>("kb");

	public List<float> KeyValueBuffer => GetPropertyArray<float>("kv");

	public string Mode => GetPropertyVal<string>("m");

	public float AdditiveBlendWeight => GetPropertyVal("ab", 1f);

	public CurveNode()
		: base(1987212643u)
	{
	}

	public void SetNodeName(string name)
	{
		SetPropertyVal("nn", "s", name);
	}

	public void SetKeyPropertyName(string name)
	{
		SetPropertyVal("kp", "s", name);
	}

	public void SetKeyFrameBuffer(IEnumerable<uint> values)
	{
		SetPropertyArray("kb", Cast.CastTypeForMaximum(values), values);
	}

	public void SetFloatKeyValueBuffer(IEnumerable<float> values)
	{
		SetPropertyArray("kv", "f", values);
	}

	public void SetVec4KeyValueBuffer(IEnumerable<float[]> values)
	{
		IEnumerable<float> values2 = values.SelectMany((float[] v) => v);
		SetPropertyArray("kv", "4v", values2);
	}

	public void SetVec4KeyValueBuffer(IEnumerable<float> values)
	{
		SetPropertyArray("kv", "4v", values);
	}

	public void SetByteKeyValueBuffer(IEnumerable<byte> values)
	{
		SetPropertyArray("kv", "b", values);
	}

	public void SetMode(string mode)
	{
		SetPropertyVal("m", "s", mode);
	}

	public void SetAdditiveBlendWeight(float value)
	{
		SetPropertyVal("ab", "f", value);
	}
}
