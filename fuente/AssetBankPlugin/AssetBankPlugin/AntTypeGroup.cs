using System.Collections.ObjectModel;

namespace AssetBankPlugin;

public class AntTypeGroup
{
	public string TypeName { get; set; }

	public int ItemCount => Assets.Count;

	public ObservableCollection<AntAssetViewModel> Assets { get; set; } = new ObservableCollection<AntAssetViewModel>();
}
