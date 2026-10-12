using System.Collections.Generic;

namespace AssetBankPlugin.Export;

public class IKHandleNode : CastNode
{
	public string Name => GetPropertyVal<string>("n");

	public List<float> TargetOffset => GetPropertyArray<float>("to");

	public bool UseTargetRotation => GetPropertyVal("tr", (byte)0) >= 1;

	public IKHandleNode()
		: base(1684564841u)
	{
	}

	public void SetName(string name)
	{
		SetPropertyVal("n", "s", name);
	}

	public void SetStartBone(ulong hash)
	{
		SetPropertyVal("sb", "l", hash);
	}

	public void SetEndBone(ulong hash)
	{
		SetPropertyVal("eb", "l", hash);
	}

	public void SetTargetBone(ulong hash)
	{
		SetPropertyVal("tb", "l", hash);
	}

	public void SetTargetOffset(float[] offset)
	{
		SetPropertyArray("to", "3v", offset);
	}

	public void SetPoleVectorBone(ulong hash)
	{
		SetPropertyVal("pv", "l", hash);
	}

	public void SetPoleBone(ulong hash)
	{
		SetPropertyVal("pb", "l", hash);
	}

	public void SetUseTargetRotation(bool enabled)
	{
		SetPropertyVal("tr", "b", enabled ? ((byte)1) : ((byte)0));
	}
}
