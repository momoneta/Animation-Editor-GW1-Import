using System.Collections.Generic;

namespace AssetBankPlugin.Export;

public class BlendShapeNode : CastNode
{
	public string Name => GetPropertyVal<string>("n");

	public List<uint> TargetShapeVertexIndices => GetPropertyArray<uint>("vi");

	public List<float> TargetShapeVertexPositions => GetPropertyArray<float>("vp");

	public float TargetWeightScale => GetPropertyVal("ts", 0f);

	public BlendShapeNode()
		: base(1752394850u)
	{
	}

	public void SetName(string name)
	{
		SetPropertyVal("n", "s", name);
	}

	public void SetBaseShape(ulong hash)
	{
		SetPropertyVal("b", "l", hash);
	}

	public void SetTargetShapeVertexIndices(IEnumerable<uint> indices)
	{
		SetPropertyArray("vi", Cast.CastTypeForMaximum(indices), indices);
	}

	public void SetTargetShapeVertexPositions(IEnumerable<float> positions)
	{
		SetPropertyArray("vp", "3v", positions);
	}

	public void SetTargetWeightScale(float scale)
	{
		SetPropertyVal("ts", "f", scale);
	}
}
