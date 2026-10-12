using System.Collections.Generic;

namespace AssetBankPlugin.Export;

public class NotificationTrackNode : CastNode
{
	public string Name => GetPropertyVal<string>("n");

	public List<uint> KeyFrameBuffer => GetPropertyArray<uint>("kb");

	public NotificationTrackNode()
		: base(1718187118u)
	{
	}

	public void SetName(string name)
	{
		SetPropertyVal("n", "s", name);
	}

	public void SetKeyFrameBuffer(IEnumerable<uint> values)
	{
		SetPropertyArray("kb", Cast.CastTypeForMaximum(values), values);
	}
}
