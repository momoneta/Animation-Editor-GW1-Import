using System.Collections.Generic;

namespace AssetBankPlugin.Export;

public class ColorNode : CastNode
{
	public string Name => GetPropertyVal<string>("n");

	public string ColorSpace => GetPropertyVal("cs", "srgb");

	public List<float> Rgba => GetPropertyArray<float>("rgba");

	public ColorNode()
		: base(1919709027u)
	{
	}

	public void SetName(string name)
	{
		SetPropertyVal("n", "s", name);
	}

	public void SetColorSpace(string value)
	{
		SetPropertyVal("cs", "s", value);
	}

	public void SetRgba(float[] rgba)
	{
		SetPropertyArray("rgba", "4v", rgba);
	}
}
