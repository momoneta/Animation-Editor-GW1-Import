using System.Collections.Generic;

namespace AssetBankPlugin.Export;

public class ConstraintNode : CastNode
{
	public string Name => GetPropertyVal<string>("n");

	public string ConstraintType => GetPropertyVal<string>("ct");

	public bool MaintainOffset => GetPropertyVal("mo", (byte)0) >= 1;

	public List<float> CustomOffset => GetPropertyArray<float>("co");

	public float Weight => GetPropertyVal("wt", 1f);

	public bool SkipX => GetPropertyVal("sx", (byte)0) >= 1;

	public bool SkipY => GetPropertyVal("sy", (byte)0) >= 1;

	public bool SkipZ => GetPropertyVal("sz", (byte)0) >= 1;

	public ConstraintNode()
		: base(1953721955u)
	{
	}

	public void SetName(string name)
	{
		SetPropertyVal("n", "s", name);
	}

	public void SetConstraintType(string type)
	{
		SetPropertyVal("ct", "s", type);
	}

	public void SetConstraintBone(ulong hash)
	{
		SetPropertyVal("cb", "l", hash);
	}

	public void SetTargetBone(ulong hash)
	{
		SetPropertyVal("tb", "l", hash);
	}

	public void SetMaintainOffset(bool enabled)
	{
		SetPropertyVal("mo", "b", enabled ? ((byte)1) : ((byte)0));
	}

	public void SetCustomOffset(float[] offset)
	{
		if (offset.Length == 3)
		{
			SetPropertyArray("co", "3v", offset);
		}
		else if (offset.Length == 4)
		{
			SetPropertyArray("co", "4v", offset);
		}
	}

	public void SetWeight(float weight)
	{
		SetPropertyVal("wt", "f", weight);
	}

	public void SetSkipX(bool enabled)
	{
		SetPropertyVal("sx", "b", enabled ? ((byte)1) : ((byte)0));
	}

	public void SetSkipY(bool enabled)
	{
		SetPropertyVal("sy", "b", enabled ? ((byte)1) : ((byte)0));
	}

	public void SetSkipZ(bool enabled)
	{
		SetPropertyVal("sz", "b", enabled ? ((byte)1) : ((byte)0));
	}
}
