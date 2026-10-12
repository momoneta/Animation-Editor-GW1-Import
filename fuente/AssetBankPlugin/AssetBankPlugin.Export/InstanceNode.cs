using System.Collections.Generic;

namespace AssetBankPlugin.Export;

public class InstanceNode : CastNode
{
	public string Name => GetPropertyVal<string>("n");

	public List<float> Position => GetPropertyArray<float>("p");

	public List<float> Rotation => GetPropertyArray<float>("r");

	public List<float> Scale => GetPropertyArray<float>("s");

	public InstanceNode()
		: base(1953721961u)
	{
	}

	public void SetName(string name)
	{
		SetPropertyVal("n", "s", name);
	}

	public void SetReferenceFile(ulong hash)
	{
		SetPropertyVal("rf", "l", hash);
	}

	public void SetPosition(float[] position)
	{
		SetPropertyArray("p", "3v", position);
	}

	public void SetRotation(float[] rotation)
	{
		SetPropertyArray("r", "4v", rotation);
	}

	public void SetScale(float[] scale)
	{
		SetPropertyArray("s", "3v", scale);
	}
}
