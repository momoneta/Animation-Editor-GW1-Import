using System.Collections.Generic;

namespace AssetBankPlugin.Export;

public class BoneNode : CastNode
{
	public string Name => GetPropertyVal<string>("n");

	public bool SegmentScaleCompensate => GetPropertyVal("ssc", (byte)1) >= 1;

	public List<float> LocalPosition => GetPropertyArray<float>("lp");

	public List<float> LocalRotation => GetPropertyArray<float>("lr");

	public List<float> WorldPosition => GetPropertyArray<float>("wp");

	public List<float> WorldRotation => GetPropertyArray<float>("wr");

	public List<float> Scale => GetPropertyArray<float>("s");

	public BoneNode()
		: base(1701736290u)
	{
	}

	public void SetName(string name)
	{
		SetPropertyVal("n", "s", name);
	}

	public int ParentIndex()
	{
		return (int)(GetPropertyVal("p", 0u) ^ 0x80000000u) - int.MinValue;
	}

	public void SetParentIndex(int index)
	{
		if (index < 0)
		{
			SetPropertyVal("p", "i", (uint)(index + 4294967296L));
		}
		else
		{
			SetPropertyVal("p", "i", (uint)index);
		}
	}

	public void SetSegmentScaleCompensate(bool enabled)
	{
		SetPropertyVal("ssc", "b", enabled ? ((byte)1) : ((byte)0));
	}

	public void SetLocalPosition(float[] position)
	{
		SetPropertyArray("lp", "3v", position);
	}

	public void SetLocalRotation(float[] rotation)
	{
		SetPropertyArray("lr", "4v", rotation);
	}

	public void SetWorldPosition(float[] position)
	{
		SetPropertyArray("wp", "3v", position);
	}

	public void SetWorldRotation(float[] rotation)
	{
		SetPropertyArray("wr", "4v", rotation);
	}

	public void SetScale(float[] scale)
	{
		SetPropertyArray("s", "3v", scale);
	}
}
