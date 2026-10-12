using System.Collections.Generic;

namespace AssetBankPlugin.Export;

public class InternalAnimation
{
	public List<Frame> Frames = new List<Frame>();

	public List<string> RotationChannels = new List<string>();

	public List<string> PositionChannels = new List<string>();

	public List<string> ScaleChannels = new List<string>();

	public bool Additive;

	public string Name { get; set; }
}
