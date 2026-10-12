using System.Collections.Generic;

namespace AssetBankPlugin.Export;

public class HairNode : CastNode
{
	public string Name => GetPropertyVal<string>("n");

	public List<uint> SegmentsBuffer => GetPropertyArray<uint>("se");

	public List<float> ParticleBuffer => GetPropertyArray<float>("pt");

	public HairNode()
		: base(1919508840u)
	{
	}

	public void SetName(string name)
	{
		SetPropertyVal("n", "s", name);
	}

	public void SetSegmentBuffer(IEnumerable<uint> values)
	{
		SetPropertyArray("se", Cast.CastTypeForMaximum(values), values);
	}

	public void SetParticleBuffer(IEnumerable<float> values)
	{
		SetPropertyArray("pt", "3v", values);
	}

	public void SetMaterial(ulong hash)
	{
		SetPropertyVal("m", "l", hash);
	}
}
