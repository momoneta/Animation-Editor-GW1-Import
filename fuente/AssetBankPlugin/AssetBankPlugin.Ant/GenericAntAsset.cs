using System;
using System.Collections.Generic;

namespace AssetBankPlugin.Ant;

public class GenericAntAsset : AntAsset
{
	public override string Name { get; set; } = "Unknown_Asset";

	public override Guid ID { get; set; } = Guid.Empty;

	public override void SetData(Dictionary<string, object> data)
	{
		ParseBasicData(data);
	}
}
