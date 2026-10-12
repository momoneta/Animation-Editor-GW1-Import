using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using AssetBankPlugin.Ant;
using AssetBankPlugin.Export;
using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Windows;
using FrostySdk.Managers.Entries;

namespace AssetBankPlugin;

public class AntAssetViewModel : INotifyPropertyChanged
{
	private bool _isNew;

	private bool _isModified;

	private bool _isUnsaved;

	private bool _isSelectedForExport;

	private bool _showCheckbox;

	private ObservableCollection<AntPropertyViewModel> _properties;

	public string Name { get; set; }

	public Guid Id { get; set; }

	public object AssetInstance { get; set; }

	public AntStateAssetEditor ParentEditor { get; set; }

	public bool IsNew
	{
		get
		{
			return _isNew;
		}
		set
		{
			_isNew = value;
			OnPropertyChanged("IsNew");
			OnPropertyChanged("NameFontWeight");
			OnPropertyChanged("DisplayName");
			OnPropertyChanged("RevertVisibility");
		}
	}

	public bool IsModified
	{
		get
		{
			return _isModified;
		}
		set
		{
			_isModified = value;
			OnPropertyChanged("IsModified");
			OnPropertyChanged("NameFontWeight");
			OnPropertyChanged("DisplayName");
			OnPropertyChanged("RevertVisibility");
		}
	}

	public bool IsUnsaved
	{
		get
		{
			return _isUnsaved;
		}
		set
		{
			_isUnsaved = value;
			OnPropertyChanged("IsUnsaved");
			OnPropertyChanged("DisplayName");
			OnPropertyChanged("RevertVisibility");
		}
	}

	public string DisplayName
	{
		get
		{
			if (!IsUnsaved)
			{
				return Name;
			}
			return Name + " *";
		}
	}

	public FontWeight NameFontWeight
	{
		get
		{
			if (!IsNew && !IsModified)
			{
				return FontWeights.Normal;
			}
			return FontWeights.Bold;
		}
	}

	public bool IsSelectedForExport
	{
		get
		{
			return _isSelectedForExport;
		}
		set
		{
			_isSelectedForExport = value;
			OnPropertyChanged("IsSelectedForExport");
			ParentEditor?.NotifyExportStatus();
		}
	}

	public Visibility CheckboxVisibility
	{
		get
		{
			if (_showCheckbox && AssetInstance is AnimationAsset)
			{
				return Visibility.Visible;
			}
			return Visibility.Collapsed;
		}
	}

	public ICommand CopyGuidCommand => new RelayCommand(delegate
	{
		Clipboard.SetText(Id.ToString());
	});

	public Visibility ExportVisibility
	{
		get
		{
			if (AssetInstance is AnimationAsset)
			{
				return Visibility.Visible;
			}
			return Visibility.Collapsed;
		}
	}

	public ICommand ExportCommand => new RelayCommand(delegate
	{
		ExportAsset();
	});

	public Visibility ImportAnimVisibility
	{
		get
		{
			if (AssetInstance is VbrAnimationAsset)
			{
				return Visibility.Visible;
			}
			return Visibility.Collapsed;
		}
	}

	public ICommand ImportAnimationCommand => new RelayCommand(delegate
	{
		ParentEditor?.ImportAnimation(this);
	});

	public Visibility DuplicateVisibility
	{
		get
		{
			if (AssetInstance is AntAsset)
			{
				return Visibility.Visible;
			}
			return Visibility.Collapsed;
		}
	}

	public Visibility ShowTreeVisibility
	{
		get
		{
			if (AssetInstance is AntAsset)
			{
				return Visibility.Visible;
			}
			return Visibility.Collapsed;
		}
	}

	public ICommand DuplicateCommand => new RelayCommand(delegate
	{
		ParentEditor?.DuplicateAsset(this);
	});

	public ICommand ShowTreeCommand => new RelayCommand(delegate
	{
		ParentEditor?.ShowAssetTree(this);
	});

	public Visibility EditAnimAssetsVisibility
	{
		get
		{
			if (AssetInstance is AntAsset { AssetType: "ActorControllerAsset" })
			{
				return Visibility.Visible;
			}
			return Visibility.Collapsed;
		}
	}

	public ICommand EditAnimAssetsCommand => new RelayCommand(delegate
	{
		ParentEditor?.EditAnimAssets(this);
	});

	public Visibility RevertVisibility
	{
		get
		{
			if (ParentEditor != null && (IsModified || IsNew || IsUnsaved))
			{
				return Visibility.Visible;
			}
			return Visibility.Collapsed;
		}
	}

	public ICommand RevertCommand => new RelayCommand(delegate
	{
		ParentEditor?.RevertAsset(this);
	});

	public Visibility TestCompressorVisibility
	{
		get
		{
			if (AssetInstance is VbrAnimationAsset)
			{
				return Visibility.Visible;
			}
			return Visibility.Collapsed;
		}
	}

	public ICommand TestCompressorCommand => new RelayCommand(delegate
	{
		ParentEditor?.TestCompressor(this);
	});

	public Visibility ExportRigMeshVisibility
	{
		get
		{
			if (AssetInstance is RigAsset)
			{
				return Visibility.Visible;
			}
			return Visibility.Collapsed;
		}
	}

	public ICommand ExportRigMeshCommand => new RelayCommand(delegate
	{
		ParentEditor?.ExportRigWithMesh(this);
	});

	public ObservableCollection<AntPropertyViewModel> Properties
	{
		get
		{
			if (_properties == null)
			{
				_properties = new ObservableCollection<AntPropertyViewModel>();
				if (AssetInstance != null)
				{
					if (AssetInstance is GenericAntAsset { RawData: not null } genericAntAsset)
					{
						foreach (KeyValuePair<string, object> rawDatum in genericAntAsset.RawData)
						{
							if (!(rawDatum.Key == "__name") && !(rawDatum.Key == "__guid") && !(rawDatum.Key == "__key"))
							{
								AntPropertyViewModel antPropertyViewModel = new AntPropertyViewModel(rawDatum.Key, rawDatum.Value)
								{
									IsEditable = true
								};
								antPropertyViewModel.ModifiedCallback = MarkModified;
								_properties.Add(antPropertyViewModel);
							}
						}
					}
					else
					{
						Type type = AssetInstance.GetType();
						bool flag = true;
						foreach (PropertyInfo allProperty in ReflectionHelper.GetAllProperties(type))
						{
							if (allProperty.Name == "Name" || allProperty.Name == "ID" || allProperty.Name == "Bank" || allProperty.Name == "AssetType" || allProperty.Name == "RawData")
							{
								continue;
							}
							try
							{
								AntPropertyViewModel antPropertyViewModel2 = new AntPropertyViewModel(allProperty.Name, allProperty.GetValue(AssetInstance));
								if (flag)
								{
									antPropertyViewModel2.IsEditable = true;
									antPropertyViewModel2.ModifiedCallback = MarkModified;
								}
								_properties.Add(antPropertyViewModel2);
							}
							catch
							{
							}
						}
						foreach (FieldInfo allField in ReflectionHelper.GetAllFields(type))
						{
							if (allField.Name.Contains("<") || allField.Name.Contains("k__BackingField"))
							{
								continue;
							}
							try
							{
								AntPropertyViewModel antPropertyViewModel3 = new AntPropertyViewModel(allField.Name, allField.GetValue(AssetInstance));
								if (flag)
								{
									antPropertyViewModel3.IsEditable = true;
									antPropertyViewModel3.ModifiedCallback = MarkModified;
								}
								_properties.Add(antPropertyViewModel3);
							}
							catch
							{
							}
						}
					}
				}
			}
			return _properties;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public void MarkModified()
	{
		IsModified = true;
		IsUnsaved = true;
	}

	public void SetBulkMode(bool show)
	{
		_showCheckbox = show;
		if (!show)
		{
			IsSelectedForExport = false;
		}
		OnPropertyChanged("CheckboxVisibility");
	}

	private void ExportAsset()
	{
		object assetInstance = AssetInstance;
		AnimationAsset anim = assetInstance as AnimationAsset;
		if (anim == null || ParentEditor == null || ParentEditor.BankBytes == null)
		{
			return;
		}
		bool useRigPickerMenu = false;
		RigAsset selectedBankRig = null;
		AnimationOptions opt = null;
		if (useRigPickerMenu)
		{
			List<RigAsset> list = new List<RigAsset>();
			foreach (AntTypeGroup masterGroup in ParentEditor._masterGroups)
			{
				if (!(masterGroup.TypeName == "RigAsset"))
				{
					continue;
				}
				foreach (AntAssetViewModel asset in masterGroup.Assets)
				{
					if (asset.AssetInstance is RigAsset item)
					{
						list.Add(item);
					}
				}
			}
			if (list.Count == 0)
			{
				FrostyMessageBox.Show("No RigAssets found in this bank to extract a skeleton from.", "Export Failed");
				return;
			}
			AntStateAssetEditor.BankRigPickerDialog bankRigPickerDialog = new AntStateAssetEditor.BankRigPickerDialog(list)
			{
				Owner = Window.GetWindow(ParentEditor)
			};
			if (bankRigPickerDialog.ShowDialog() != true || bankRigPickerDialog.SelectedRig == null)
			{
				return;
			}
			selectedBankRig = bankRigPickerDialog.SelectedRig;
		}
		else
		{
			opt = new AnimationOptions();
			opt.Load();
			if (string.IsNullOrEmpty(opt.ExportSkeletonAsset))
			{
				FrostyMessageBox.Show("Please set an Export Skeleton in Options first.", "Missing Skeleton");
				return;
			}
		}
		string text = Name;
		int num = text.LastIndexOf(" (");
		if (num > 0 && text.EndsWith(")"))
		{
			text = text.Substring(0, num);
		}
		FrostySaveFileDialog frostySaveFileDialog = new FrostySaveFileDialog("Save Animation", "*.cast (Cast)|*.cast|*.seanim (SEAnim)|*.seanim", "Cast", text);
		if (!frostySaveFileDialog.ShowDialog())
		{
			return;
		}
		string exportDirectory = Path.GetDirectoryName(frostySaveFileDialog.FileName);
		string extension = Path.GetExtension(frostySaveFileDialog.FileName).ToLower();
		string assetName = text;
		FrostyTaskWindow.Show("Exporting Animation", "", delegate
		{
			Application.Current.Dispatcher.Invoke(delegate
			{
				try
				{
					InternalSkeleton internalSkeleton = null;
					if (useRigPickerMenu && selectedBankRig != null)
					{
						internalSkeleton = AntRigExporter.BuildInternalSkeletonFromRig(selectedBankRig);
					}
					else
					{
						EbxAssetEntry ebxEntry = App.AssetManager.GetEbxEntry(opt.ExportSkeletonAsset);
						internalSkeleton = SkeletonAssetExport.ConvertToInternal(App.AssetManager.GetEbx(ebxEntry).RootObject);
					}
					if (internalSkeleton == null)
					{
						App.Logger.LogError("[AntStateEditor] Failed to construct skeleton for animation export.");
					}
					else
					{
						anim.Name = assetName;
						anim.Channels = anim.GetChannels(anim.ChannelToDofAsset);
						InternalAnimation internalAnimation = anim.ConvertToInternal();
						if (internalAnimation != null)
						{
							if (extension == ".cast")
							{
								new AnimationExporterCAST().Export(internalAnimation, internalSkeleton, exportDirectory);
							}
							else
							{
								new AnimationExporterSEANIM().Export(internalAnimation, internalSkeleton, exportDirectory);
							}
							App.Logger.Log("[AntStateEditor] Successfully exported " + assetName);
						}
					}
				}
				catch (Exception ex)
				{
					App.Logger.LogError("[AntStateEditor] Failed to export " + assetName + ": " + ex.Message);
				}
			});
		});
	}

	public void ResetProperties()
	{
		_properties = null;
		OnPropertyChanged("Properties");
	}

	protected void OnPropertyChanged(string propName)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
	}
}
