using System;
using System.Collections.Generic;

namespace AssetBankPlugin.Ant;

public class ClipControllerAsset : AntAsset
{
	public override string Name { get; set; }

	public override Guid ID { get; set; }

	public Guid[] Anims { get; set; }

	public Guid Anim { get; set; }

	public Guid DofCodecAnim { get; set; }

	public Guid Target { get; set; }

	public float NumTicks { get; set; }

	public float TickOffset { get; set; }

	public float FPS { get; set; }

	public float TimeScale { get; set; }

	public float Distance { get; set; }

	public int TrajectoryAnimIndex { get; set; }

	public int Modes { get; set; }

	public Guid TagCollectionSet { get; set; }

	public override void SetData(Dictionary<string, object> data)
	{
		ParseBasicData(data);
		if (data.TryGetValue("Target", out var value))
		{
			Target = SafeGuid(value);
		}
		if (data.TryGetValue("Anims", out var value2))
		{
			Anims = ConvertArray<Guid>(value2);
		}
		if (data.TryGetValue("Anim", out var value3))
		{
			Anim = SafeGuid(value3);
		}
		if (data.TryGetValue("DofCodecAnim", out var value4))
		{
			DofCodecAnim = SafeGuid(value4);
		}
		if (data.TryGetValue("NumTicks", out var value5))
		{
			NumTicks = Convert.ToSingle(value5);
		}
		if (data.TryGetValue("TickOffset", out var value6))
		{
			TickOffset = Convert.ToSingle(value6);
		}
		if (data.TryGetValue("FPS", out var value7))
		{
			FPS = Convert.ToSingle(value7);
		}
		if (data.TryGetValue("TimeScale", out var value8))
		{
			TimeScale = Convert.ToSingle(value8);
		}
		if (data.TryGetValue("FPSScale", out var value9))
		{
			TimeScale = Convert.ToSingle(value9);
		}
		if (data.TryGetValue("Distance", out var value10))
		{
			Distance = Convert.ToSingle(value10);
		}
		if (data.TryGetValue("TrajectoryAnimIndex", out var value11))
		{
			TrajectoryAnimIndex = Convert.ToInt32(value11);
		}
		if (data.TryGetValue("DeltaTrajectory", out var value12))
		{
			TrajectoryAnimIndex = Convert.ToInt32(value12);
		}
		if (data.TryGetValue("Modes", out var value13))
		{
			Modes = Convert.ToInt32(value13);
		}
		if (data.TryGetValue("TagCollectionSet", out var value14))
		{
			TagCollectionSet = SafeGuid(value14);
		}
		if (TagCollectionSet == Guid.Empty && data.TryGetValue("TagCollectionSetAsset", out var value15))
		{
			TagCollectionSet = SafeGuid(value15);
		}
	}
}
