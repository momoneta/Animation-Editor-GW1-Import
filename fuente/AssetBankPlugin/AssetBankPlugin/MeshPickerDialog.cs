using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Frosty.Core;
using FrostySdk.Managers.Entries;

namespace AssetBankPlugin;

public class MeshPickerDialog : AssetPickerDialog
{
	public MeshPickerDialog()
	{
		base.Title = "  Select Mesh Asset";
		base.Width = 720.0;
		base.Height = 600.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		IEnumerable<EbxAssetEntry> first = App.AssetManager.EnumerateEbx("SkinnedMeshAsset");
		IEnumerable<EbxAssetEntry> second = App.AssetManager.EnumerateEbx("RigidMeshAsset");
		IOrderedEnumerable<EbxAssetEntry> entries = from x in first.Concat(second)
			orderby x.Name
			select x;
		ConfigureFlatList("MeshAsset", "Search mesh assets by name or path...", entries);
	}
}
