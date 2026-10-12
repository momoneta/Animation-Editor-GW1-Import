using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FrostySdk.Managers.Entries;

namespace AssetBankPlugin;

public class BankNode : INotifyPropertyChanged
{
	public static readonly ImageSource FolderIcon = new BitmapImage(new Uri("pack://application:,,,/FrostyEditor;component/Images/CloseFolder.png"));

	public static readonly ImageSource OpenFolderIcon = new BitmapImage(new Uri("pack://application:,,,/FrostyEditor;component/Images/OpenFolder.png"));

	private bool _isExpanded;

	private bool _isSelected;

	public string Name { get; set; }

	public bool IsFolder { get; set; }

	public EbxAssetEntry Entry { get; set; }

	public ObservableCollection<BankNode> Children { get; } = new ObservableCollection<BankNode>();

	public bool IsExpanded
	{
		get
		{
			return _isExpanded;
		}
		set
		{
			_isExpanded = value;
			OnPropertyChanged("IsExpanded");
			if (IsFolder)
			{
				OnPropertyChanged("Icon");
			}
		}
	}

	public bool IsSelected
	{
		get
		{
			return _isSelected;
		}
		set
		{
			_isSelected = value;
			OnPropertyChanged("IsSelected");
			if (Entry != null)
			{
				BankPickerDialog.UpdateSelection(Entry, value);
			}
		}
	}

	public ImageSource Icon
	{
		get
		{
			if (IsFolder)
			{
				if (!_isExpanded)
				{
					return FolderIcon;
				}
				return OpenFolderIcon;
			}
			return null;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	protected void OnPropertyChanged(string p)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
	}
}
