using System;
using System.Collections.Generic;
using System.Linq;
using Frosty.Core;
using Frosty.Core.Controls.Editors;
using Frosty.Core.Misc;
using FrostySdk;
using FrostySdk.Attributes;
using MeshSetPlugin.Editors;

namespace AssetBankPlugin;

[DisplayName("Animation Options")]
public class AnimationOptions : OptionsExtension
{
	[Category("Export/Import")]
	[DisplayName("Export Skeleton")]
	[Description("Determines the default skeleton selected when exporting animations.")]
	[Editor(typeof(FrostySkeletonEditor))]
	public string ExportSkeletonAsset { get; set; }

	[Category("Export/Import")]
	[DisplayName("Use Cache")]
	[Description("Whether or not to use the AntRef cache to drastically improve exporting speeds.")]
	[Editor(typeof(FrostyBooleanEditor))]
	public bool UseCache { get; set; }

	[Category("Export/Import")]
	[DisplayName("Animation Quality")]
	[Description("Determines the compression quality when importing animations. Range is 0.0 (lowest quality, maximum error) to 1.0 (highest quality, no compression error).")]
	public float AnimationQuality { get; set; }

	[Category("Export/Import")]
	[DisplayName("AntBank Compression")]
	[Description("Determines the compression algorithm used when saving the AntBank. Lighter compression algorithms save much faster but result in larger mod files, while heavier compression algorithms produce significantly smaller mod files at the cost of longer save times. 'None' is the default and fastest option.")]
	[Editor(typeof(AntCompressionEditor))]
	public CustomComboData<AntBankCompressionMode, string> AntBankCompression { get; set; }

	public override void Load()
	{
		ExportSkeletonAsset = Config.Get("AnimationExportSkeleton", "", ConfigScope.Game);
		UseCache = Config.Get("UseCache", defaultValue: true, ConfigScope.Game);
		AnimationQuality = Config.Get("AnimationImportQuality", 1f, ConfigScope.Game);
		List<AntBankCompressionMode> list = Enum.GetValues(typeof(AntBankCompressionMode)).Cast<AntBankCompressionMode>().ToList();
		List<string> inNames = new List<string> { "None", "LZ4 (Light)", "Zlib (Recommended)", "Oodle (Heavy)" };
		AntBankCompressionMode result;
		AntBankCompressionMode item = (Enum.TryParse<AntBankCompressionMode>(Config.Get("AntBankCompression", "None", ConfigScope.Game), out result) ? result : AntBankCompressionMode.None);
		int num = list.IndexOf(item);
		AntBankCompression = new CustomComboData<AntBankCompressionMode, string>(list, inNames)
		{
			SelectedIndex = ((num >= 0) ? num : 0)
		};
	}

	public override void Save()
	{
		Config.Add("AnimationExportSkeleton", ExportSkeletonAsset, ConfigScope.Game);
		Config.Add("UseCache", UseCache, ConfigScope.Game);
		Config.Add("AnimationImportQuality", AnimationQuality, ConfigScope.Game);
		Config.Add("AntBankCompression", AntBankCompression.SelectedValue.ToString(), ConfigScope.Game);
		Config.Save();
	}

	public override bool Validate()
	{
		if (AnimationQuality < 0f)
		{
			AnimationQuality = 0f;
		}
		if (AnimationQuality > 1f)
		{
			AnimationQuality = 1f;
		}
		return true;
	}

	public CompressionType GetSelectedCompressionType()
	{
		return (AntBankCompression?.SelectedValue ?? AntBankCompressionMode.None) switch
		{
			AntBankCompressionMode.LZ4 => CompressionType.LZ4, 
			AntBankCompressionMode.Zlib => CompressionType.ZLib, 
			AntBankCompressionMode.Oodle => CompressionType.Oodle, 
			_ => CompressionType.None, 
		};
	}
}
