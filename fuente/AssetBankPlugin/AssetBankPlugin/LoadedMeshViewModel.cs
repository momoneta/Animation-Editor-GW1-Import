using System.ComponentModel;
using System.Windows.Input;
using Frosty.Core;

namespace AssetBankPlugin;

public class LoadedMeshViewModel : INotifyPropertyChanged
{
	private readonly AntStateAssetEditor _editor;

	private string _name;

	private bool _isVisible = true;

	public int InternalIndex { get; set; }

	public string Name
	{
		get
		{
			return _name;
		}
		set
		{
			_name = value;
			OnPropertyChanged("Name");
		}
	}

	public bool IsVisible
	{
		get
		{
			return _isVisible;
		}
		set
		{
			if (_isVisible != value)
			{
				_isVisible = value;
				OnPropertyChanged("IsVisible");
				_editor?.OnMeshVisibilityChanged();
			}
		}
	}

	public ICommand UseSelectedCommand => new RelayCommand(delegate
	{
		_editor?.ReplaceMeshFromExplorer(InternalIndex);
	});

	public ICommand RemoveCommand => new RelayCommand(delegate
	{
		_editor?.RemoveMeshAt(InternalIndex);
	});

	public event PropertyChangedEventHandler PropertyChanged;

	public LoadedMeshViewModel(AntStateAssetEditor editor)
	{
		_editor = editor;
	}

	protected void OnPropertyChanged(string p)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
	}
}
