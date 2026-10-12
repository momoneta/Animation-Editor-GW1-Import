using System.Linq;
using System.Windows;
using Frosty.Core;
using FrostySdk.Managers.Entries;

namespace AssetBankPlugin;

public class SkeletonPickerDialog : AssetPickerDialog
{
	public SkeletonPickerDialog()
	{
		base.Title = "  Select Preview Skeleton";
		base.Width = 720.0;
		base.Height = 600.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		IOrderedEnumerable<EbxAssetEntry> entries = from x in App.AssetManager.EnumerateEbx("SkeletonAsset")
			orderby x.Name
			select x;
		ConfigureFlatList("SkeletonAsset", "Search skeleton assets by name or path…", entries, "#1A6B38");
	}
}
