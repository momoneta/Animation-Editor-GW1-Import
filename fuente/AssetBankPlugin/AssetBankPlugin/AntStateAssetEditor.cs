using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AssetBankPlugin.Ant;
using AssetBankPlugin.Ant.VBR;
using AssetBankPlugin.Export;
using AssetBankPlugin.Formats;
using AssetBankPlugin.Formats.GenericData2;
using AssetBankPlugin.Import;
using AssetBankPlugin.Render;
using AssetBankPlugin.Windows;
using Assimp;
using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Screens;
using Frosty.Core.Viewport;
using Frosty.Core.Windows;
using FrostySdk;
using FrostySdk.Ebx;
using FrostySdk.Interfaces;
using FrostySdk.IO;
using FrostySdk.Managers.Entries;
using MeshSetPlugin;
using MeshSetPlugin.Render;
using MeshSetPlugin.Resources;
using Microsoft.Win32;
using SharpDX;

namespace AssetBankPlugin;

[TemplatePart(Name = "PART_SearchBox", Type = typeof(TextBox))]
[TemplatePart(Name = "PART_FilterMenu", Type = typeof(AssetFilterMenu))]
[TemplatePart(Name = "PART_AssetTreeView", Type = typeof(TreeView))]
[TemplatePart(Name = "PART_LoadingOverlay", Type = typeof(Border))]
[TemplatePart(Name = "PART_LoadingText", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_BulkToggle", Type = typeof(ToggleButton))]
[TemplatePart(Name = "PART_ExportBulkButton", Type = typeof(Button))]
[TemplatePart(Name = "PART_AddRefButton", Type = typeof(Button))]
[TemplatePart(Name = "PART_Viewport", Type = typeof(FrostyViewport))]
[TemplatePart(Name = "PART_SkeletonPath", Type = typeof(TextBox))]
[TemplatePart(Name = "PART_SkeletonSetDefault", Type = typeof(Button))]
[TemplatePart(Name = "PART_SkeletonOverrideBtn", Type = typeof(ToggleButton))]
[TemplatePart(Name = "PART_MeshPath", Type = typeof(TextBox))]
[TemplatePart(Name = "PART_MeshLoad", Type = typeof(Button))]
[TemplatePart(Name = "PART_MeshUseSelected", Type = typeof(Button))]
[TemplatePart(Name = "PART_RestartBtn", Type = typeof(Button))]
[TemplatePart(Name = "PART_PlayPauseBtn", Type = typeof(ToggleButton))]
[TemplatePart(Name = "PART_TimelineSlider", Type = typeof(Slider))]
[TemplatePart(Name = "PART_FrameLabel", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_TotalFramesLabel", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_SpeedSlider", Type = typeof(Slider))]
[TemplatePart(Name = "PART_SpeedLabel", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_LodCombo", Type = typeof(ComboBox))]
[TemplatePart(Name = "PART_RenderModeCombo", Type = typeof(ComboBox))]
[TemplatePart(Name = "PART_MeshListPopup", Type = typeof(Popup))]
[TemplatePart(Name = "PART_SaveButton", Type = typeof(Button))]
public class AntStateAssetEditor : FrostyAssetEditor, INotifyPropertyChanged
{
	private sealed class LoadedMeshEntry
	{
		public string DisplayName;

		public MeshSet MeshSet;

		public MeshMaterialCollection Materials;

		public MeshRenderSkeleton PerMeshSkeleton;

		public EbxAssetEntry SourceEntry;

		public PointerRef VariationRef;

		public string BpbName;
	}

	public class DuplicateRenameWindow : FrostyWindow
	{
		private readonly IEnumerable<string> _existingNames;

		private TextBox m_nameBox;

		public string NewName { get; private set; }

		public DuplicateRenameWindow(string defaultName, IEnumerable<string> existingNames)
		{
			_existingNames = existingNames;
			NewName = defaultName;
			base.Title = "  Duplicate Asset";
			base.Width = 420.0;
			base.Height = 150.0;
			base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
			base.ResizeMode = ResizeMode.NoResize;
			Grid grid = new Grid
			{
				Margin = new Thickness(15.0)
			};
			grid.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			grid.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			grid.RowDefinitions.Add(new RowDefinition
			{
				Height = new GridLength(1.0, GridUnitType.Star)
			});
			grid.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			TextBlock element = new TextBlock
			{
				Text = "Enter a unique name for the duplicated asset:",
				Foreground = Brushes.White,
				Margin = new Thickness(0.0, 0.0, 0.0, 8.0),
				FontSize = 11.0
			};
			Grid.SetRow(element, 0);
			grid.Children.Add(element);
			m_nameBox = new TextBox
			{
				Text = defaultName,
				Height = 24.0,
				VerticalContentAlignment = VerticalAlignment.Center,
				Padding = new Thickness(4.0, 0.0, 4.0, 0.0),
				Foreground = Brushes.White,
				Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 40, 40)),
				BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(68, 68, 68)),
				CaretBrush = Brushes.White
			};
			m_nameBox.Loaded += delegate
			{
				m_nameBox.Focus();
				m_nameBox.SelectAll();
			};
			Grid.SetRow(m_nameBox, 1);
			grid.Children.Add(m_nameBox);
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				HorizontalAlignment = HorizontalAlignment.Right,
				Margin = new Thickness(0.0, 10.0, 0.0, 0.0)
			};
			Button button = new Button
			{
				Content = "Duplicate",
				Width = 90.0,
				Height = 26.0,
				Margin = new Thickness(0.0, 0.0, 10.0, 0.0),
				IsDefault = true
			};
			button.Click += delegate
			{
				Commit();
			};
			stackPanel.Children.Add(button);
			Button button2 = new Button
			{
				Content = "Cancel",
				Width = 80.0,
				Height = 26.0,
				IsCancel = true
			};
			button2.Click += delegate
			{
				base.DialogResult = false;
				Close();
			};
			stackPanel.Children.Add(button2);
			Grid.SetRow(stackPanel, 3);
			grid.Children.Add(stackPanel);
			base.Content = grid;
		}

		private void Commit()
		{
			string typed = m_nameBox.Text.Trim();
			if (string.IsNullOrWhiteSpace(typed))
			{
				FrostyMessageBox.Show("Asset name cannot be empty.", "Duplicate Asset");
				return;
			}
			if (_existingNames.Any((string x) => x.Equals(typed, StringComparison.OrdinalIgnoreCase)))
			{
				FrostyMessageBox.Show("An asset with this name already exists in the bank database. Please specify a unique name.", "Duplicate Asset");
				return;
			}
			NewName = typed;
			base.DialogResult = true;
			Close();
		}
	}

	public class BankRigPickerDialog : FrostyWindow
	{
		private ListBox m_listBox;

		public RigAsset SelectedRig { get; private set; }

		public BankRigPickerDialog(List<RigAsset> rigs)
		{
			base.Title = "  Select Export Skeleton Rig";
			base.Width = 450.0;
			base.Height = 350.0;
			base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
			base.ResizeMode = ResizeMode.NoResize;
			Grid grid = new Grid
			{
				Margin = new Thickness(15.0)
			};
			grid.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			grid.RowDefinitions.Add(new RowDefinition
			{
				Height = new GridLength(1.0, GridUnitType.Star)
			});
			grid.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			TextBlock element = new TextBlock
			{
				Text = "Select a Rig Asset from the active bank to use as the export skeleton:",
				Foreground = Brushes.White,
				Margin = new Thickness(0.0, 0.0, 0.0, 8.0),
				FontSize = 11.0,
				TextWrapping = TextWrapping.Wrap
			};
			Grid.SetRow(element, 0);
			grid.Children.Add(element);
			m_listBox = new ListBox
			{
				Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 40, 40)),
				BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(68, 68, 68)),
				Foreground = Brushes.White,
				DisplayMemberPath = "Name"
			};
			foreach (RigAsset rig in rigs)
			{
				m_listBox.Items.Add(rig);
			}
			if (m_listBox.Items.Count > 0)
			{
				m_listBox.SelectedIndex = 0;
			}
			m_listBox.MouseDoubleClick += delegate
			{
				Confirm();
			};
			Grid.SetRow(m_listBox, 1);
			grid.Children.Add(m_listBox);
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				HorizontalAlignment = HorizontalAlignment.Right,
				Margin = new Thickness(0.0, 10.0, 0.0, 0.0)
			};
			Button button = new Button
			{
				Content = "Select",
				Width = 90.0,
				Height = 26.0,
				Margin = new Thickness(0.0, 0.0, 10.0, 0.0),
				IsDefault = true
			};
			button.Click += delegate
			{
				Confirm();
			};
			stackPanel.Children.Add(button);
			Button button2 = new Button
			{
				Content = "Cancel",
				Width = 80.0,
				Height = 26.0,
				IsCancel = true
			};
			button2.Click += delegate
			{
				base.DialogResult = false;
				Close();
			};
			stackPanel.Children.Add(button2);
			Grid.SetRow(stackPanel, 2);
			grid.Children.Add(stackPanel);
			base.Content = grid;
		}

		private void Confirm()
		{
			SelectedRig = m_listBox.SelectedItem as RigAsset;
			if (SelectedRig != null)
			{
				base.DialogResult = true;
				Close();
			}
		}
	}

	internal class AnimAssetEntry
	{
		public int TrackIndex;

		public int AnimIndex;

		public int AssetOffset;

		public ulong CurrentKey;

		public ulong NewKey;

		public bool WasEdited;

		public string CurrentGuid => KeyToGuid(CurrentKey).ToString();
	}

	private TextBox m_searchBox;

	private AssetFilterMenu m_filterMenu;

	private TreeView m_assetTreeView;

	private Border m_loadingOverlay;

	private TextBlock m_loadingText;

	private ToggleButton m_bulkToggle;

	private Bank _bank;

	private byte[] _bankBytes;

	private ResAssetEntry _bankResEntry;

	private ChunkAssetEntry _bankChunkEntry;

	private bool _bankIsBigEndian;

	private FrostyViewport m_viewport;

	private readonly AntAnimPreviewScreen m_screen = new AntAnimPreviewScreen();

	private TextBox m_meshPathBox;

	private bool _meshLoaded;

	private string _currentSkeletonPath = "";

	private bool _suppressSkeletonToggle;

	private bool _skeletonOverrideActive;

	private Button m_restartBtn;

	private ToggleButton m_playPauseBtn;

	private Slider m_timelineSlider;

	private TextBlock m_frameLabel;

	private TextBlock m_totalFramesLabel;

	private Slider m_speedSlider;

	private TextBlock m_speedLabel;

	private ComboBox m_lodCombo;

	private ComboBox m_renderModeCombo;

	private Popup _meshListPopup;

	private readonly List<LoadedMeshEntry> _loadedMeshData = new List<LoadedMeshEntry>();

	private AnimationAsset _currentPreviewAsset;

	private string _currentPreviewName;

	private bool _isProgrammaticSliderUpdate;

	internal List<AntTypeGroup> _masterGroups = new List<AntTypeGroup>();

	private ObservableCollection<AntTypeGroup> _filteredGroups = new ObservableCollection<AntTypeGroup>();

	private AntAssetViewModel _selectedAsset;

	private bool _isBulkMode;

	private AntAssetViewModel _shiftAnchor;

	private bool _isBankBytesDirty;

	private const uint ClipControllerTypeHash = 2108648108u;

	private const uint ActorControllerTypeHash = 3127372384u;

	private const uint SequenceAnimTrackTypeHash = 1244245997u;

	private const uint SequenceAnimationTypeHash = 1488598287u;

	private CancellationTokenSource _searchCancellationTokenSource;

	public ObservableCollection<LoadedMeshViewModel> LoadedMeshes { get; } = new ObservableCollection<LoadedMeshViewModel>();

	public bool IsExportEnabled
	{
		get
		{
			if (_isBulkMode)
			{
				return _masterGroups.SelectMany((AntTypeGroup g) => g.Assets).Any((AntAssetViewModel a) => a.IsSelectedForExport);
			}
			if (_selectedAsset != null)
			{
				return _selectedAsset.AssetInstance is AnimationAsset;
			}
			return false;
		}
	}

	public byte[] BankBytes => _bankBytes;

	public bool BankIsBigEndian => _bankIsBigEndian;

	private bool IsLegacyBank
	{
		get
		{
			if (_bank != null && _bank.Classes2.Count == 0)
			{
				return _bank.Classes.Count > 0;
			}
			return false;
		}
	}

	public ICommand AddMeshFromExplorerCommand => new RelayCommand(delegate
	{
		AddMeshFromExplorer();
	});

	public event PropertyChangedEventHandler PropertyChanged;

	public void NotifyExportStatus()
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsExportEnabled"));
	}

	static AntStateAssetEditor()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(AntStateAssetEditor), new FrameworkPropertyMetadata(typeof(AntStateAssetEditor)));
	}

	public AntStateAssetEditor(ILogger inLogger)
		: base(inLogger)
	{
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		m_searchBox = GetTemplateChild("PART_SearchBox") as TextBox;
		m_filterMenu = GetTemplateChild("PART_FilterMenu") as AssetFilterMenu;
		ToggleButton filterBtn = GetTemplateChild("PART_FilterButton") as ToggleButton;
		Popup filterPopup = GetTemplateChild("PART_FilterPopup") as Popup;
		m_assetTreeView = GetTemplateChild("PART_AssetTreeView") as TreeView;
		m_loadingOverlay = GetTemplateChild("PART_LoadingOverlay") as Border;
		m_loadingText = GetTemplateChild("PART_LoadingText") as TextBlock;
		m_bulkToggle = GetTemplateChild("PART_BulkToggle") as ToggleButton;
		Button button = GetTemplateChild("PART_ExportBulkButton") as Button;
		Button button2 = GetTemplateChild("PART_AddRefButton") as Button;
		m_viewport = GetTemplateChild("PART_Viewport") as FrostyViewport;
		m_meshPathBox = GetTemplateChild("PART_MeshPath") as TextBox;
		Button button3 = GetTemplateChild("PART_MeshLoad") as Button;
		ToggleButton skelOverride = GetTemplateChild("PART_SkeletonOverrideBtn") as ToggleButton;
		Button button4 = GetTemplateChild("PART_MeshUseSelected") as Button;
		AnimationOptions animationOptions = new AnimationOptions();
		animationOptions.Load();
		_currentSkeletonPath = animationOptions.ExportSkeletonAsset ?? "";
		if (filterBtn != null && filterPopup != null)
		{
			DateTime lastPopupClose = DateTime.MinValue;
			filterPopup.Closed += delegate
			{
				lastPopupClose = DateTime.Now;
				filterBtn.IsChecked = false;
			};
			filterBtn.Click += delegate
			{
				if ((DateTime.Now - lastPopupClose).TotalMilliseconds < 150.0)
				{
					filterBtn.IsChecked = false;
					filterPopup.IsOpen = false;
				}
				else
				{
					filterPopup.IsOpen = filterBtn.IsChecked == true;
				}
			};
		}
		if (skelOverride != null)
		{
			skelOverride.Checked += async delegate
			{
				if (!_suppressSkeletonToggle)
				{
					SkeletonPickerDialog skeletonPickerDialog = new SkeletonPickerDialog
					{
						Owner = Window.GetWindow(this)
					};
					if (skeletonPickerDialog.ShowDialog() == true && skeletonPickerDialog.SelectedEntry != null)
					{
						_currentSkeletonPath = skeletonPickerDialog.SelectedEntry.Name;
						_skeletonOverrideActive = true;
						AnimationOptions animationOptions2 = new AnimationOptions();
						animationOptions2.Load();
						animationOptions2.ExportSkeletonAsset = _currentSkeletonPath;
						animationOptions2.Save();
						if (_currentPreviewAsset != null)
						{
							await LoadPreviewAsync(_currentPreviewAsset, _currentPreviewName);
						}
						if (m_meshPathBox != null && !string.IsNullOrEmpty(m_meshPathBox.Text))
						{
							await LoadMeshAsync(m_meshPathBox.Text.Trim());
						}
					}
					else
					{
						_suppressSkeletonToggle = true;
						skelOverride.IsChecked = false;
						_suppressSkeletonToggle = false;
					}
				}
			};
			skelOverride.Unchecked += async delegate
			{
				if (!_suppressSkeletonToggle)
				{
					_skeletonOverrideActive = false;
					_currentSkeletonPath = "";
					if (_currentPreviewAsset != null)
					{
						await LoadPreviewAsync(_currentPreviewAsset, _currentPreviewName);
					}
					if (m_meshPathBox != null && !string.IsNullOrEmpty(m_meshPathBox.Text))
					{
						await LoadMeshAsync(m_meshPathBox.Text.Trim());
					}
				}
			};
		}
		if (button3 != null)
		{
			button3.Click += delegate
			{
				LoadMeshAsync(m_meshPathBox?.Text?.Trim() ?? "");
			};
		}
		if (button4 != null)
		{
			button4.Click += delegate
			{
				try
				{
					if (((dynamic)App.EditorWindow)?.DataExplorer?.SelectedAsset is EbxAssetEntry ebxAssetEntry && m_meshPathBox != null)
					{
						m_meshPathBox.Text = ebxAssetEntry.Name;
						App.Logger.Log("[AntStateEditor] Mesh path set to: " + ebxAssetEntry.Name);
					}
					else
					{
						App.Logger.LogWarning("[AntStateEditor] No asset selected in data explorer.");
					}
				}
				catch (Exception ex)
				{
					App.Logger.LogError("[AntStateEditor] Could not read data explorer selection: " + ex.Message);
				}
			};
		}
		if (m_bulkToggle != null)
		{
			m_bulkToggle.Checked += delegate
			{
				_isBulkMode = true;
				ToggleBulkMode(isBulk: true);
				NotifyExportStatus();
			};
			m_bulkToggle.Unchecked += delegate
			{
				_isBulkMode = false;
				ToggleBulkMode(isBulk: false);
				NotifyExportStatus();
			};
		}
		if (button != null)
		{
			button.Click += delegate
			{
				ExportSelected();
			};
		}
		if (button2 != null)
		{
			button2.Click += delegate
			{
				OpenReferenceSelector();
			};
		}
		if (GetTemplateChild("PART_SaveButton") is Button button5)
		{
			button5.Click += delegate
			{
				SaveAllUnsaved();
			};
		}
		if (m_assetTreeView != null)
		{
			AddImportAllBanksMenuItem();
			m_assetTreeView.ItemsSource = _filteredGroups;
			m_assetTreeView.SelectedItemChanged += delegate(object s, RoutedPropertyChangedEventArgs<object> e)
			{
				_selectedAsset = e.NewValue as AntAssetViewModel;
				NotifyExportStatus();
				if (e.NewValue is AntAssetViewModel { AssetInstance: AnimationAsset assetInstance, Name: var text })
				{
					int num = text.LastIndexOf(" (");
					if (num > 0 && text.EndsWith(")"))
					{
						text = text.Substring(0, num);
					}
					_currentPreviewAsset = assetInstance;
					_currentPreviewName = text;
					LoadPreviewAsync(assetInstance, text);
				}
			};
			m_assetTreeView.PreviewMouseLeftButtonDown += OnTreeViewPreviewMouseDown;
		}
		if (m_searchBox != null)
		{
			m_searchBox.TextChanged += delegate
			{
				ApplySearchFilter(m_searchBox.Text);
			};
		}
		if (m_filterMenu != null)
		{
			m_filterMenu.FilterChanged += delegate
			{
				ApplySearchFilter(m_searchBox?.Text);
			};
		}
		if (m_viewport != null)
		{
			m_viewport.Screen = m_screen;
		}
		_meshListPopup = GetTemplateChild("PART_MeshListPopup") as Popup;
		if (_meshListPopup != null)
		{
			_meshListPopup.DataContext = this;
		}
		if (GetTemplateChild("PART_PreviewSkeletonBtn") is ToggleButton toggleButton)
		{
			toggleButton.Checked += delegate
			{
				m_screen.ShowSkeleton = true;
			};
			toggleButton.Unchecked += delegate
			{
				m_screen.ShowSkeleton = false;
			};
		}
		m_restartBtn = GetTemplateChild("PART_RestartBtn") as Button;
		m_playPauseBtn = GetTemplateChild("PART_PlayPauseBtn") as ToggleButton;
		m_timelineSlider = GetTemplateChild("PART_TimelineSlider") as Slider;
		m_frameLabel = GetTemplateChild("PART_FrameLabel") as TextBlock;
		m_totalFramesLabel = GetTemplateChild("PART_TotalFramesLabel") as TextBlock;
		m_speedSlider = GetTemplateChild("PART_SpeedSlider") as Slider;
		m_speedLabel = GetTemplateChild("PART_SpeedLabel") as TextBlock;
		m_lodCombo = GetTemplateChild("PART_LodCombo") as ComboBox;
		m_renderModeCombo = GetTemplateChild("PART_RenderModeCombo") as ComboBox;
		if (m_restartBtn != null)
		{
			m_restartBtn.Click += delegate
			{
				m_screen.Restart();
			};
		}
		if (m_playPauseBtn != null)
		{
			m_playPauseBtn.Checked += delegate
			{
				m_screen.IsPaused = false;
			};
			m_playPauseBtn.Unchecked += delegate
			{
				m_screen.IsPaused = true;
			};
		}
		if (m_speedSlider != null)
		{
			m_speedSlider.ValueChanged += delegate(object s, RoutedPropertyChangedEventArgs<double> e)
			{
				m_screen.SpeedMultiplier = e.NewValue;
				if (m_speedLabel != null)
				{
					m_speedLabel.Text = $"{e.NewValue:F1}x";
				}
			};
			m_speedSlider.PreviewMouseWheel += delegate(object s, MouseWheelEventArgs e)
			{
				double num = ((e.Delta > 0) ? 0.1 : (-0.1));
				double value = Math.Max(m_speedSlider.Minimum, Math.Min(m_speedSlider.Maximum, m_speedSlider.Value + num));
				m_speedSlider.Value = value;
				e.Handled = true;
			};
		}
		if (m_lodCombo != null)
		{
			m_lodCombo.ItemsSource = new string[6] { "0", "1", "2", "3", "4", "5" };
			m_lodCombo.SelectedIndex = 0;
			m_lodCombo.SelectionChanged += delegate
			{
				m_screen.CurrentLOD = m_lodCombo.SelectedIndex;
			};
		}
		if (m_renderModeCombo != null)
		{
			m_renderModeCombo.SelectedIndex = 0;
			m_renderModeCombo.SelectionChanged += delegate
			{
				m_screen.RenderMode = (DebugRenderMode)m_renderModeCombo.SelectedIndex;
			};
		}
		m_screen.FrameChanged += delegate(int frame)
		{
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				_isProgrammaticSliderUpdate = true;
				if (m_timelineSlider != null)
				{
					m_timelineSlider.Value = frame;
				}
				if (m_frameLabel != null)
				{
					m_frameLabel.Text = frame.ToString();
				}
				_isProgrammaticSliderUpdate = false;
			});
		};
		if (m_timelineSlider != null)
		{
			m_timelineSlider.ValueChanged += delegate(object s, RoutedPropertyChangedEventArgs<double> e)
			{
				if (!_isProgrammaticSliderUpdate)
				{
					m_screen.SeekToFrame((int)e.NewValue);
				}
			};
			m_timelineSlider.PreviewMouseWheel += delegate(object s, MouseWheelEventArgs e)
			{
				if (!_isProgrammaticSliderUpdate)
				{
					int targetFrame = (int)Math.Max(0.0, Math.Min(m_screen.TotalFrames - 1, m_timelineSlider.Value + (double)((e.Delta > 0) ? 1 : (-1))));
					m_screen.SeekToFrame(targetFrame);
					e.Handled = true;
				}
			};
		}
		LoadAsync();
	}

	public override void Closed()
	{
		if (m_viewport != null)
		{
			try
			{
				m_viewport.Shutdown();
			}
			catch (Exception ex)
			{
				App.Logger.LogWarning("[AntStateEditor] Viewport shutdown: " + ex.Message);
			}
		}
		if (m_assetTreeView != null)
		{
			m_assetTreeView.ItemsSource = null;
			m_assetTreeView.Items.Clear();
		}
		if (_masterGroups != null)
		{
			foreach (AntTypeGroup masterGroup in _masterGroups)
			{
				masterGroup.Assets?.Clear();
			}
			_masterGroups.Clear();
		}
		_filteredGroups?.Clear();
		if (LoadedMeshes != null)
		{
			LoadedMeshes.Clear();
		}
		_loadedMeshData?.Clear();
		_selectedAsset = null;
		_currentPreviewAsset = null;
		_shiftAnchor = null;
		_bankBytes = null;
		_bank = null;
		AntRefTable.Clear();
		GC.Collect();
		base.Closed();
	}

	public void ShowAssetTree(AntAssetViewModel sourceVm)
	{
		if (sourceVm != null && sourceVm.AssetInstance != null && sourceVm.AssetInstance is AntAsset rootAsset)
		{
			List<AntAssetViewModel> allVms = _masterGroups.SelectMany((AntTypeGroup g) => g.Assets).ToList();
			TreePreviewer treePreviewer = new TreePreviewer(rootAsset, sourceVm.Id, allVms);
			treePreviewer.Owner = Window.GetWindow(this);
			treePreviewer.AssetDoubleClicked = delegate(AntAsset asset)
			{
				SelectAndRevealAsset(asset);
			};
			treePreviewer.Show();
		}
	}

	private void SelectAndRevealAsset(AntAsset asset)
	{
		AntAssetViewModel targetVm = null;
		foreach (AntTypeGroup masterGroup in _masterGroups)
		{
			foreach (AntAssetViewModel asset2 in masterGroup.Assets)
			{
				if (asset2.AssetInstance == asset)
				{
					targetVm = asset2;
					break;
				}
			}
			if (targetVm != null)
			{
				break;
			}
		}
		if (targetVm != null)
		{
			string text = asset.ID.ToString();
			if (m_searchBox != null)
			{
				m_searchBox.Text = text;
			}
			ApplySearchFilter(text);
			_selectedAsset = targetVm;
			base.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
			{
				ExpandAndSelectInTree(targetVm, asset);
			});
		}
	}

	private void ExpandAndSelectInTree(AntAssetViewModel targetVm, AntAsset asset)
	{
		if (m_assetTreeView == null)
		{
			return;
		}
		AntTypeGroup antTypeGroup = null;
		foreach (AntTypeGroup filteredGroup in _filteredGroups)
		{
			foreach (AntAssetViewModel asset2 in filteredGroup.Assets)
			{
				if (asset2.AssetInstance == asset)
				{
					antTypeGroup = filteredGroup;
					break;
				}
			}
			if (antTypeGroup != null)
			{
				break;
			}
		}
		if (antTypeGroup == null)
		{
			return;
		}
		TreeViewItem groupItem = m_assetTreeView.ItemContainerGenerator.ContainerFromItem(antTypeGroup) as TreeViewItem;
		if (groupItem == null)
		{
			return;
		}
		groupItem.IsExpanded = true;
		base.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
		{
			groupItem.UpdateLayout();
			if (groupItem.ItemContainerGenerator.ContainerFromItem(targetVm) is TreeViewItem treeViewItem)
			{
				treeViewItem.IsSelected = true;
				treeViewItem.BringIntoView();
				Window.GetWindow(this)?.Activate();
			}
		});
	}

	private void OpenReferenceSelector()
	{
		BankPickerDialog bankPickerDialog = new BankPickerDialog
		{
			Owner = Window.GetWindow(this)
		};
		if (bankPickerDialog.ShowDialog() != true)
		{
			return;
		}
		List<EbxAssetEntry> selectedBanks = bankPickerDialog.SelectedEntries.ToList();
		if (selectedBanks.Count == 0)
		{
			return;
		}
		FrostyTaskWindow.Show("Loading Reference Banks", "", delegate(FrostyTaskWindow task)
		{
			int num = 0;
			foreach (EbxAssetEntry item in selectedBanks)
			{
				task.Update("Caching " + item.Name + "...", (double)num / (double)selectedBanks.Count * 100.0);
				try
				{
					dynamic rootObject = App.AssetManager.GetEbx(item).RootObject;
					Stream stream = null;
					int bundleId = ((item.Bundles.Count > 0) ? item.Bundles[0] : 0);
					if (rootObject.StreamingGuid == Guid.Empty)
					{
						ResAssetEntry resEntry = App.AssetManager.GetResEntry(item.Name);
						if (resEntry != null)
						{
							stream = App.AssetManager.GetRes(resEntry);
						}
					}
					else
					{
						dynamic chunkEntry = App.AssetManager.GetChunkEntry(rootObject.StreamingGuid);
						if (chunkEntry != null)
						{
							bundleId = chunkEntry.Bundles[0];
							stream = App.AssetManager.GetChunk(chunkEntry);
						}
					}
					if (stream != null)
					{
						using NativeReader r = new NativeReader(stream);
						new Bank(r, bundleId);
					}
				}
				catch (Exception ex)
				{
					App.Logger.LogError("[AntStateEditor] Failed to load reference bank " + item.Name + ": " + ex.Message);
				}
				num++;
			}
		});
		App.Logger.Log($"[AntStateEditor] Successfully loaded {selectedBanks.Count} reference banks into cache.");
	}

	internal void TestCompressor(AntAssetViewModel vm)
	{
		if (!(vm.AssetInstance is VbrAnimationAsset vbrAnimationAsset))
		{
			return;
		}
		try
		{
			List<System.Numerics.Vector4> list = vbrAnimationAsset.Decompress();
			ushort quaternionCount = vbrAnimationAsset.QuaternionCount;
			int vector3Count = vbrAnimationAsset.Vector3Count;
			int numFloat = vbrAnimationAsset.NumFloat;
			int numKeys = vbrAnimationAsset.NumKeys;
			int num = quaternionCount + vector3Count + numFloat;
			int num2 = numKeys * num;
			if (list.Count < num2)
			{
				App.Logger.LogError($"Decompressed data size mismatch. Expected {num2}, got {list.Count}");
				return;
			}
			int num3 = vbrAnimationAsset.QuaternionCount + vbrAnimationAsset.ConstQuaternionCount;
			int num4 = vbrAnimationAsset.Vector3Count + vbrAnimationAsset.ConstVector3Count;
			int num5 = vbrAnimationAsset.NumFloat + vbrAnimationAsset.ConstFloatCount;
			int num6 = num3 + num4 + num5;
			bool[] array = new bool[num6];
			if (vbrAnimationAsset.ConstChanMap != null && vbrAnimationAsset.ConstChanMap.Length != 0)
			{
				bool flag = false;
				int num7 = 0;
				byte[] constChanMap = vbrAnimationAsset.ConstChanMap;
				foreach (byte b in constChanMap)
				{
					for (int j = 0; j < b; j++)
					{
						if (num7 >= num6)
						{
							break;
						}
						array[num7++] = flag;
					}
					flag = !flag;
				}
			}
			else
			{
				int num8 = 0;
				for (int k = 0; k < num3; k++)
				{
					array[num8++] = k >= vbrAnimationAsset.QuaternionCount;
				}
				for (int l = 0; l < num4; l++)
				{
					array[num8++] = l >= vbrAnimationAsset.Vector3Count;
				}
				for (int m = 0; m < num5; m++)
				{
					array[num8++] = m >= vbrAnimationAsset.NumFloat;
				}
			}
			List<int> list2 = new List<int>();
			List<int> list3 = new List<int>();
			for (int n = 0; n < num3; n++)
			{
				if (array[n])
				{
					list2.Add(n);
				}
				else
				{
					list3.Add(n);
				}
			}
			List<int> list4 = new List<int>();
			List<int> list5 = new List<int>();
			for (int num9 = num3; num9 < num3 + num4; num9++)
			{
				if (array[num9])
				{
					list4.Add(num9 - num3);
				}
				else
				{
					list5.Add(num9 - num3);
				}
			}
			List<int> list6 = new List<int>();
			List<int> list7 = new List<int>();
			for (int num10 = num3 + num4; num10 < num6; num10++)
			{
				if (array[num10])
				{
					list6.Add(num10 - (num3 + num4));
				}
				else
				{
					list7.Add(num10 - (num3 + num4));
				}
			}
			float[][][] array2 = new float[num3][][];
			for (int num11 = 0; num11 < num3; num11++)
			{
				array2[num11] = new float[numKeys][];
			}
			float[][][] array3 = new float[num4][][];
			for (int num12 = 0; num12 < num4; num12++)
			{
				array3[num12] = new float[numKeys][];
			}
			float[][] array4 = new float[num5][];
			for (int num13 = 0; num13 < num5; num13++)
			{
				array4[num13] = new float[numKeys];
			}
			ushort[] array5 = vbrAnimationAsset.PaletteIndexes ?? Array.Empty<ushort>();
			float[] array6 = vbrAnimationAsset.ConstantPalette ?? Array.Empty<float>();
			int num14 = 0;
			for (int num15 = 0; num15 < list2.Count; num15++)
			{
				int num16 = list2[num15];
				System.Numerics.Vector4 vector = new System.Numerics.Vector4(0f, 0f, 0f, 1f);
				if (num14 + 3 < array5.Length)
				{
					int num17 = array5[num14];
					int num18 = array5[num14 + 1];
					int num19 = array5[num14 + 2];
					int num20 = array5[num14 + 3];
					if (num17 < array6.Length && num18 < array6.Length && num19 < array6.Length && num20 < array6.Length)
					{
						vector = new System.Numerics.Vector4(array6[num17], array6[num18], array6[num19], array6[num20]);
						vector = vector * (vbrAnimationAsset.QuatMax - vbrAnimationAsset.QuatMin) + new System.Numerics.Vector4(vbrAnimationAsset.QuatMin);
					}
				}
				num14 += 4;
				for (int num21 = 0; num21 < numKeys; num21++)
				{
					array2[num16][num21] = new float[4] { vector.X, vector.Y, vector.Z, vector.W };
				}
			}
			for (int num22 = 0; num22 < list4.Count; num22++)
			{
				int num23 = list4[num22];
				System.Numerics.Vector3 vector2 = new System.Numerics.Vector3(0f, 0f, 0f);
				if (num14 + 2 < array5.Length)
				{
					int num24 = array5[num14];
					int num25 = array5[num14 + 1];
					int num26 = array5[num14 + 2];
					if (num24 < array6.Length && num25 < array6.Length && num26 < array6.Length)
					{
						vector2 = new System.Numerics.Vector3(array6[num24], array6[num25], array6[num26]);
						vector2 = vector2 * (vbrAnimationAsset.Vec3Max - vbrAnimationAsset.Vec3Min) + new System.Numerics.Vector3(vbrAnimationAsset.Vec3Min);
					}
				}
				num14 += 3;
				for (int num27 = 0; num27 < numKeys; num27++)
				{
					array3[num23][num27] = new float[3] { vector2.X, vector2.Y, vector2.Z };
				}
			}
			for (int num28 = 0; num28 < list6.Count; num28++)
			{
				int num29 = list6[num28];
				float num30 = 0f;
				if (num14 < array5.Length)
				{
					int num31 = array5[num14];
					if (num31 < array6.Length)
					{
						num30 = array6[num31];
						num30 = num30 * (vbrAnimationAsset.FloatMax - vbrAnimationAsset.FloatMin) + vbrAnimationAsset.FloatMin;
					}
				}
				num14++;
				for (int num32 = 0; num32 < numKeys; num32++)
				{
					array4[num29][num32] = num30;
				}
			}
			for (int num33 = 0; num33 < numKeys; num33++)
			{
				int num34 = num33 * num;
				for (int num35 = 0; num35 < list3.Count; num35++)
				{
					int num36 = list3[num35];
					int num37 = num34 + num35;
					System.Numerics.Vector4 vector3 = ((num37 < list.Count) ? list[num37] : new System.Numerics.Vector4(0f, 0f, 0f, 1f));
					array2[num36][num33] = new float[4] { vector3.X, vector3.Y, vector3.Z, vector3.W };
				}
				for (int num38 = 0; num38 < list5.Count; num38++)
				{
					int num39 = list5[num38];
					int num40 = num34 + vbrAnimationAsset.QuaternionCount + num38;
					System.Numerics.Vector4 vector4 = ((num40 < list.Count) ? list[num40] : new System.Numerics.Vector4(0f, 0f, 0f, 0f));
					array3[num39][num33] = new float[3] { vector4.X, vector4.Y, vector4.Z };
				}
				for (int num41 = 0; num41 < list7.Count; num41++)
				{
					int num42 = list7[num41];
					int num43 = num34 + vbrAnimationAsset.QuaternionCount + vbrAnimationAsset.Vector3Count + num41;
					System.Numerics.Vector4 vector5 = ((num43 < list.Count) ? list[num43] : new System.Numerics.Vector4(0f, 0f, 0f, 0f));
					array4[num42][num33] = vector5.X;
				}
			}
			bool cycle = (vbrAnimationAsset.Flags & 1) != 0;
			VbrAnimationAsset vbrAnimationAsset2 = VbrCompressor.CompressFromData(array2, array3, array4, numKeys, cycle, vbrAnimationAsset);
			if (vbrAnimationAsset2.NumKeys == vbrAnimationAsset.NumKeys)
			{
				vbrAnimationAsset.Data = vbrAnimationAsset2.Data;
				vbrAnimationAsset.ConstantPalette = vbrAnimationAsset2.ConstantPalette;
				vbrAnimationAsset.FrameBlockSizes = vbrAnimationAsset2.FrameBlockSizes;
				vbrAnimationAsset.PaletteIndexes = vbrAnimationAsset2.PaletteIndexes;
				vbrAnimationAsset.ConstChanMap = vbrAnimationAsset2.ConstChanMap;
				vbrAnimationAsset.QuaternionCount = vbrAnimationAsset2.QuaternionCount;
				vbrAnimationAsset.Vector3Count = vbrAnimationAsset2.Vector3Count;
				vbrAnimationAsset.NumFloat = vbrAnimationAsset2.NumFloat;
				vbrAnimationAsset.ConstQuaternionCount = vbrAnimationAsset2.ConstQuaternionCount;
				vbrAnimationAsset.ConstVector3Count = vbrAnimationAsset2.ConstVector3Count;
				vbrAnimationAsset.ConstFloatCount = vbrAnimationAsset2.ConstFloatCount;
				vbrAnimationAsset.QuatMin = vbrAnimationAsset2.QuatMin;
				vbrAnimationAsset.QuatMax = vbrAnimationAsset2.QuatMax;
				vbrAnimationAsset.TrajMin = vbrAnimationAsset2.TrajMin;
				vbrAnimationAsset.TrajMax = vbrAnimationAsset2.TrajMax;
				vbrAnimationAsset.Vec3Min = vbrAnimationAsset2.Vec3Min;
				vbrAnimationAsset.Vec3Max = vbrAnimationAsset2.Vec3Max;
				vbrAnimationAsset.FloatMin = vbrAnimationAsset2.FloatMin;
				vbrAnimationAsset.FloatMax = vbrAnimationAsset2.FloatMax;
				vbrAnimationAsset.Dct = vbrAnimationAsset2.Dct;
				vbrAnimationAsset.KeyTimeSize = vbrAnimationAsset2.KeyTimeSize;
				vbrAnimationAsset.ConstChanMapSize = vbrAnimationAsset2.ConstChanMapSize;
				vbrAnimationAsset.ConstPaletteSize = vbrAnimationAsset2.ConstPaletteSize;
				vbrAnimationAsset.Flags = vbrAnimationAsset2.Flags;
				vbrAnimationAsset.EndFrame = vbrAnimationAsset2.EndFrame;
				vbrAnimationAsset.VectorOffsetScale = vbrAnimationAsset2.VectorOffsetScale;
				vbrAnimationAsset.FloatOffsetScale = vbrAnimationAsset2.FloatOffsetScale;
				vbrAnimationAsset.VectorOffsetSize = vbrAnimationAsset2.VectorOffsetSize;
				vbrAnimationAsset.FloatOffsetSize = vbrAnimationAsset2.FloatOffsetSize;
				vbrAnimationAsset.VectorOffsets = vbrAnimationAsset2.VectorOffsets;
				vbrAnimationAsset.RawData = new Dictionary<string, object>(vbrAnimationAsset2.RawData);
				vbrAnimationAsset.RawData["__name"] = vbrAnimationAsset.Name;
				vbrAnimationAsset.RawData["__guid"] = vbrAnimationAsset.ID;
				byte[] value = vbrAnimationAsset.ID.ToByteArray();
				vbrAnimationAsset.RawData["__key"] = BitConverter.ToUInt64(value, 0);
				FieldInfo field = typeof(VbrAnimationAsset).GetField("DecompressedData", BindingFlags.Instance | BindingFlags.NonPublic);
				if (field != null && field.GetValue(vbrAnimationAsset) is List<System.Numerics.Vector4> list8)
				{
					list8.Clear();
				}
				vm.MarkModified();
				vm.ResetProperties();
				LoadPreviewAsync(vbrAnimationAsset, _currentPreviewName);
				ILogger obj = App.Logger;
				byte[] data = vbrAnimationAsset.Data;
				object arg = ((data != null) ? data.Length : 0);
				byte[] data2 = vbrAnimationAsset2.Data;
				obj.Log($"[TestCompressor] Round-trip applied. Original size: {arg} bytes, Recompressed size: {((data2 != null) ? data2.Length : 0)} bytes");
			}
			else
			{
				App.Logger.LogError($"[TestCompressor] Round-trip failed: frame count mismatch. Original: {vbrAnimationAsset.NumKeys}, New: {vbrAnimationAsset2.NumKeys}");
			}
		}
		catch (Exception ex)
		{
			App.Logger.LogError("[TestCompressor] Test failed: " + ex.Message);
		}
	}

	public void ExportRigWithMesh(AntAssetViewModel sourceVm)
	{
		object obj = sourceVm?.AssetInstance;
		RigAsset rig = obj as RigAsset;
		if (rig == null)
		{
			return;
		}
		EbxAssetEntry initialMesh = null;
		if (((dynamic)App.EditorWindow)?.DataExplorer?.SelectedAsset is EbxAssetEntry ebxAssetEntry && (ebxAssetEntry.Type == "SkinnedMeshAsset" || ebxAssetEntry.Type == "RigidMeshAsset"))
		{
			initialMesh = ebxAssetEntry;
		}
		else if (m_meshPathBox != null && !string.IsNullOrWhiteSpace(m_meshPathBox.Text))
		{
			initialMesh = App.AssetManager.GetEbxEntry(m_meshPathBox.Text);
		}
		RigExportSettingsWindow rigExportSettingsWindow = new RigExportSettingsWindow(initialMesh)
		{
			Owner = Window.GetWindow(this)
		};
		if (rigExportSettingsWindow.ShowDialog() != true || rigExportSettingsWindow.SelectedMesh == null)
		{
			return;
		}
		EbxAssetEntry targetMeshEntry = rigExportSettingsWindow.SelectedMesh;
		string fbxVersion = rigExportSettingsWindow.SelectedFbxVersion;
		string fbxScale = rigExportSettingsWindow.SelectedScale;
		bool exportSingleLod = rigExportSettingsWindow.ExportSingleLod;
		FrostySaveFileDialog frostySaveFileDialog = new FrostySaveFileDialog("Save Rig + Mesh", "*.fbx (Autodesk FBX)|*.fbx", "Mesh", rig.Name);
		if (!frostySaveFileDialog.ShowDialog())
		{
			return;
		}
		string exportPath = frostySaveFileDialog.FileName;
		FrostyTaskWindow.Show("Exporting Rig + Mesh", "", delegate(FrostyTaskWindow task)
		{
			string text = null;
			try
			{
				InternalSkeleton internalSkeleton = AntRigExporter.BuildInternalSkeletonFromRig(rig);
				if (internalSkeleton == null)
				{
					App.Logger.LogError("[ExportRig] Failed to construct skeleton for Rig '" + rig.Name + "'.");
				}
				else if (internalSkeleton == null)
				{
					App.Logger.LogError("[ExportRig] Failed to construct skeleton for Rig '" + rig.Name + "'.");
				}
				else
				{
					dynamic val = AntRigExporter.CreateEbxSkeletonFromInternal(internalSkeleton);
					text = VirtualSkeletonManager.RegisterVirtualSkeleton(val);
					dynamic rootObject = App.AssetManager.GetEbx(targetMeshEntry).RootObject;
					ulong resRid = rootObject.MeshSetResource;
					ResAssetEntry resEntry = App.AssetManager.GetResEntry(resRid);
					MeshSet resAs = App.AssetManager.GetResAs<MeshSet>(resEntry);
					FBXExporter fBXExporter = new FBXExporter(task);
					fBXExporter.ExportFBX(rootObject, exportPath, fbxVersion, fbxScale, false, exportSingleLod, false, text, "binary", resAs);
					App.Logger.Log("[ExportRig] Successfully exported Rig '" + rig.Name + "' with Mesh '" + targetMeshEntry.Name + "' to " + exportPath);
					Application.Current.Dispatcher.Invoke(() => FrostyMessageBox.Show("Exported successfully to:\n" + exportPath, "Export Complete"));
				}
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Exception ex3 = ex2;
				App.Logger.LogError("[ExportRig] Failed to export Rig + Mesh: " + ex3.Message + "\n" + ex3.StackTrace);
				Application.Current.Dispatcher.Invoke(() => FrostyMessageBox.Show("Failed to export: " + ex3.Message, "Export Failed"));
			}
			finally
			{
				if (!string.IsNullOrEmpty(text))
				{
					VirtualSkeletonManager.UnregisterVirtualSkeleton(text);
				}
			}
		});
	}

	private void UpdateMeshSkeletons()
	{
		if (m_screen.CurrentSkeleton == null || _loadedMeshData.Count == 0)
		{
			return;
		}
		foreach (LoadedMeshEntry loadedMeshDatum in _loadedMeshData)
		{
			if (loadedMeshDatum.SourceEntry != null)
			{
				try
				{
					EbxAsset ebx = App.AssetManager.GetEbx(loadedMeshDatum.SourceEntry);
					loadedMeshDatum.PerMeshSkeleton = BuildPerMeshSkeleton(ebx.RootObject);
				}
				catch (Exception ex)
				{
					App.Logger.LogWarning("[AntStateEditor] Failed to bind skeleton for mesh '" + loadedMeshDatum.DisplayName + "': " + ex.Message);
				}
			}
		}
		OnMeshVisibilityChanged();
	}

	public void UpdateBankBytes(byte[] newBytes)
	{
		_bankBytes = newBytes;
		try
		{
			if (_bankResEntry != null)
			{
				App.AssetManager.ModifyRes(_bankResEntry.Name, _bankBytes);
			}
			else if (_bankChunkEntry != null)
			{
				App.AssetManager.ModifyChunk(_bankChunkEntry.Id, _bankBytes);
			}
		}
		catch (Exception ex)
		{
			App.Logger.LogError("[AntStateEditor] Failed to save updated bank bytes: " + ex.Message);
		}
	}

	private void AddImportAllBanksMenuItem()
	{
		if (m_assetTreeView == null)
		{
			return;
		}
		InsertImportAllBanksMenuItem(m_assetTreeView.ContextMenu);
		m_assetTreeView.ContextMenuOpening += delegate(object sender, ContextMenuEventArgs e)
		{
			ContextMenu contextMenu = m_assetTreeView.ContextMenu;
			if (contextMenu == null)
			{
				DependencyObject dependencyObject = e.OriginalSource as DependencyObject;
				while (dependencyObject != null && contextMenu == null)
				{
					if (dependencyObject is FrameworkElement { ContextMenu: not null } frameworkElement)
					{
						contextMenu = frameworkElement.ContextMenu;
						break;
					}
					dependencyObject = ((dependencyObject is FrameworkElement) ? LogicalTreeHelper.GetParent(dependencyObject) : null);
				}
			}
			InsertImportAllBanksMenuItem(contextMenu);
		};
	}

	private void InsertImportAllBanksMenuItem(ContextMenu contextMenu)
	{
		if (contextMenu == null)
		{
			return;
		}
		foreach (object item in (IEnumerable)contextMenu.Items)
		{
			if (item is MenuItem menuItem && menuItem.Header as string == "Import Animation In All Banks")
			{
				return;
			}
		}
		MenuItem menuItem2 = new MenuItem
		{
			Header = "Import Animation In All Banks",
			ToolTip = "Writes the animation to every bank of the game that has an asset with this name (one per level plus the frontend). No need to press Save afterwards."
		};
		menuItem2.Click += delegate
		{
			ImportAnimationAllBanks(m_assetTreeView.SelectedItem as AntAssetViewModel);
		};
		MenuItem menuItem3 = null;
		foreach (object item2 in (IEnumerable)contextMenu.Items)
		{
			if (item2 is MenuItem menuItem4 && menuItem4.Header as string == "Import Animation")
			{
				menuItem3 = menuItem4;
				break;
			}
		}
		contextMenu.Items.Insert((menuItem3 != null) ? (contextMenu.Items.IndexOf(menuItem3) + 1) : contextMenu.Items.Count, menuItem2);
		logger.Log("[AntStateEditor] Menu 'Import Animation In All Banks' added to the asset tree context menu.");
	}

	private static Dictionary<Guid, AntAsset> BuildAnimToControllerMap()
	{
		Dictionary<Guid, AntAsset> dictionary = new Dictionary<Guid, AntAsset>();
		foreach (AntAsset value in AntRefTable.Refs.Values)
		{
			if (value.AssetType == "ClipControllerAsset")
			{
				Guid property = value.GetProperty<Guid>("Anim");
				Guid[] propertyArray = value.GetPropertyArray<Guid>("Anims");
				if (property != Guid.Empty)
				{
					dictionary[property] = value;
				}
				if (propertyArray == null)
				{
					continue;
				}
				Guid[] array = propertyArray;
				foreach (Guid guid in array)
				{
					if (guid != Guid.Empty)
					{
						dictionary[guid] = value;
					}
				}
			}
			else if (value.AssetType == "ClipControllerData")
			{
				Guid property2 = value.GetProperty<Guid>("Anim");
				if (property2 != Guid.Empty)
				{
					dictionary[property2] = value;
				}
			}
		}
		return dictionary;
	}

	private void ModifyResWithFastCompression(ResAssetEntry entry, byte[] data, CompressionType compression = CompressionType.None)
	{
		byte[] array = Utils.CompressFile(data, null, (ResourceType)entry.ResType, compression);
		if (entry.ModifiedEntry == null)
		{
			entry.ModifiedEntry = new ModifiedAssetEntry();
		}
		entry.ModifiedEntry.Data = array;
		entry.ModifiedEntry.OriginalSize = data.Length;
		entry.ModifiedEntry.Sha1 = ComputeSha1(array);
		entry.IsDirty = true;
	}

	private static Sha1 ComputeSha1(byte[] buffer)
	{
		using SHA1 sHA = SHA1.Create();
		return new Sha1(sHA.ComputeHash(buffer));
	}

	private void SaveAllUnsaved()
	{
		if (_bankBytes == null)
		{
			FrostyMessageBox.Show("Bank not loaded.", "Save");
			return;
		}
		List<AntAssetViewModel> unsavedVms = null;
		List<AntAsset> unsavedAssets = null;
		bool hasChangesToSave = false;
		string bankEntryName = null;
		Application.Current.Dispatcher.Invoke(delegate
		{
			bankEntryName = base.AssetEntry?.Name;
			unsavedVms = (from vm in _masterGroups.SelectMany((AntTypeGroup g) => g.Assets)
				where vm.IsUnsaved
				select vm).ToList();
			hasChangesToSave = unsavedVms.Count > 0 || _isBankBytesDirty || base.AssetModified;
			if (hasChangesToSave)
			{
				unsavedAssets = new List<AntAsset>(unsavedVms.Count);
				foreach (AntAssetViewModel item2 in unsavedVms)
				{
					if (item2.AssetInstance is AntAsset item)
					{
						if (item2.IsModified)
						{
							ApplyUiEditsToAsset(item2);
						}
						unsavedAssets.Add(item);
					}
				}
			}
		});
		if (!hasChangesToSave)
		{
			Application.Current.Dispatcher.Invoke(() => FrostyMessageBox.Show("No unsaved changes.", "Save"));
			return;
		}
		FrostyTaskWindow.Show("Saving Assets", "", delegate(FrostyTaskWindow task)
		{
			bool bankIsBigEndian = _bankIsBigEndian;
			Dictionary<uint, GenericClass2> classes = _bank.Classes2;
			int num = 0;
			int count = unsavedAssets.Count;
			List<AntAssetViewModel> succeededVms = new List<AntAssetViewModel>(count);
			for (int i = 0; i < count; i++)
			{
				AntAssetViewModel item = unsavedVms[i];
				AntAsset asset = unsavedAssets[i];
				if (task != null && count > 0)
				{
					double value = (double)i / (double)count * 80.0;
					task.Update($"Serializing {asset.Name} ({i + 1}/{count})...", value);
				}
				try
				{
					if (IsLegacyBank)
					{
						if (asset is VbrAnimationAsset)
						{
							num++;
							succeededVms.Add(item);
						}
						else
						{
							App.Logger.LogWarning("[AntStateEditor] '" + asset.Name + "' was not saved: only animation imports are supported for this game.");
						}
					}
					else
					{
						byte[] newSection = DynamicDat2Serializer.Serialize(asset, classes, bankIsBigEndian);
						uint typeHashForAsset = DynamicDat2Serializer.GetTypeHashForAsset(asset, classes);
						ulong assetKey = GuidToKey(asset.ID);
						int keyFieldOffset = 16;
						if (classes.TryGetValue(typeHashForAsset, out var value2))
						{
							GenericField2 genericField = value2.Elements.FirstOrDefault((GenericField2 f) => f.Name == "__key" || f.Name == "__guid");
							if (genericField != null)
							{
								keyFieldOffset = genericField.Offset;
							}
						}
						int num2 = FindDat2SectionStart(_bankBytes, typeHashForAsset, assetKey, keyFieldOffset, bankIsBigEndian);
						if (num2 < 0)
						{
							App.Logger.LogError($"[AntStateEditor] Could not locate binary for '{asset.Name}' (0x{typeHashForAsset:X8}).");
						}
						else
						{
							_bankBytes = SpliceSection(_bankBytes, num2, newSection, bankIsBigEndian);
							num++;
							succeededVms.Add(item);
						}
					}
				}
				catch (Exception ex)
				{
					Exception ex3 = ex;
					App.Logger.LogError("[AntStateEditor] Serialize failed for '" + asset.Name + "': " + ex3.Message + "\n" + ex3.StackTrace);
					Application.Current.Dispatcher.Invoke(() => FrostyMessageBox.Show("Failed to serialize '" + asset.Name + "': " + ex3.Message, "Save Failed"));
				}
			}
			task?.Update("Compressing and writing bank bytes...", 90.0);
			try
			{
				AnimationOptions animationOptions = new AnimationOptions();
				animationOptions.Load();
				CompressionType compressionType = animationOptions.GetSelectedCompressionType();
				if (_bankResEntry != null)
				{
					ModifyResWithFastCompression(_bankResEntry, _bankBytes, compressionType);
				}
				else
				{
					if (_bankChunkEntry == null)
					{
						throw new InvalidOperationException("the bank (res or chunk) behind '" + bankEntryName + "' was not found; nothing was written.");
					}
					CompressionType compressionType2 = ((compressionType != CompressionType.None) ? compressionType : CompressionType.Default);
					App.AssetManager.ModifyChunk(_bankChunkEntry.Id, _bankBytes, compressionType2);
					if (_bankChunkEntry.ModifiedEntry == null || _bankChunkEntry.ModifiedEntry.Data == null)
					{
						Guid id = _bankChunkEntry.Id;
						throw new InvalidOperationException("chunk " + id.ToString() + " is not in the asset manager; nothing was written.");
					}
					_bankChunkEntry.ModifiedEntry.OriginalSize = (uint)_bankBytes.Length;
					compressionType = compressionType2;
				}
				ILogger obj = App.Logger;
				string[] obj2 = new string[7] { "[AntStateEditor] Bank written: ", bankEntryName, " | ", null, null, null, null };
				string text;
				if (_bankResEntry == null)
				{
					Guid id = _bankChunkEntry.Id;
					text = "chunk " + id.ToString() + ", ";
				}
				else
				{
					text = "res '" + _bankResEntry.Name + "' (tipo " + _bankResEntry.ResType + "), ";
				}
				obj2[3] = text;
				obj2[4] = _bankBytes.Length.ToString("N0");
				obj2[5] = " bytes, compresion ";
				obj2[6] = compressionType.ToString();
				obj.Log(string.Concat(obj2));
			}
			catch (Exception ex4)
			{
				Exception ex5 = ex4;
				Exception ex6 = ex5;
				App.Logger.LogError("[AntStateEditor] Failed to write bank bytes: " + ex6.Message);
				Application.Current.Dispatcher.Invoke(() => FrostyMessageBox.Show("Failed to write bank: " + ex6.Message, "Save Failed"));
				return;
			}
			Application.Current.Dispatcher.Invoke(delegate
			{
				foreach (AntAssetViewModel item3 in succeededVms)
				{
					item3.IsUnsaved = false;
				}
				if (base.AssetEntry != null)
				{
					base.AssetEntry.IsDirty = true;
					if (_bankResEntry != null)
					{
						_bankResEntry.IsDirty = true;
						base.AssetEntry.LinkAsset(_bankResEntry);
					}
					if (_bankChunkEntry != null)
					{
						_bankChunkEntry.IsDirty = true;
						base.AssetEntry.LinkAsset(_bankChunkEntry);
					}
				}
				_isBankBytesDirty = false;
				base.AssetModified = true;
			});
			if (num > 0)
			{
				App.Logger.Log($"[AntStateEditor] Saved {num} asset(s) to bank.");
			}
			else
			{
				App.Logger.Log("[AntStateEditor] Bank updated successfully.");
			}
		});
	}

	private void ApplyUiEditsToAsset(AntAssetViewModel assetVm)
	{
		AntAsset antAsset = (AntAsset)assetVm.AssetInstance;
		if (antAsset.RawData == null)
		{
			antAsset.RawData = new Dictionary<string, object>();
		}
		foreach (AntPropertyViewModel property in assetVm.Properties)
		{
			CommitPropertyEditsRecursive(property, antAsset.RawData);
		}
		antAsset.SetData(antAsset.RawData);
	}

	private void CommitPropertyEditsRecursive(AntPropertyViewModel prop, object parentContainer)
	{
		bool flag = prop.ValueObj is IEnumerable && !(prop.ValueObj is string);
		if ((prop.WasEdited && !flag) || prop.IsStructureModified)
		{
			object obj = (prop.IsStructureModified ? prop.ValueObj : ParseEditedValue(prop.EditedValue, prop.TypeStr));
			if (parentContainer is Dictionary<string, object> dictionary)
			{
				dictionary[prop.Name] = obj;
			}
			else if (parentContainer is IList<object> list)
			{
				int num = ParseIndex(prop.Name);
				if (num >= 0 && num < list.Count)
				{
					list[num] = obj;
				}
			}
			else if (parentContainer is object[] array)
			{
				int num2 = ParseIndex(prop.Name);
				if (num2 >= 0 && num2 < array.Length)
				{
					array[num2] = obj;
				}
			}
			else if (parentContainer is IList list2)
			{
				int num3 = ParseIndex(prop.Name);
				if (num3 >= 0 && num3 < list2.Count)
				{
					try
					{
						list2[num3] = obj;
					}
					catch
					{
					}
				}
			}
			prop.ValueStr = (prop.IsStructureModified ? prop.GetString(obj) : prop.EditedValue);
			prop.ValueObj = obj;
			prop.IsStructureModified = false;
		}
		if (prop.Children == null || prop.Children.Count <= 0)
		{
			return;
		}
		foreach (AntPropertyViewModel child in prop.Children)
		{
			CommitPropertyEditsRecursive(child, prop.ValueObj);
		}
	}

	private int ParseIndex(string name)
	{
		if (name.StartsWith("[") && name.EndsWith("]") && int.TryParse(name.Substring(1, name.Length - 2), out var result))
		{
			return result;
		}
		return -1;
	}

	private object ParseEditedValue(string valueStr, string typeStr)
	{
		return typeStr switch
		{
			"Boolean" => bool.Parse(valueStr), 
			"SByte" => sbyte.Parse(valueStr), 
			"Byte" => byte.Parse(valueStr), 
			"Int16" => short.Parse(valueStr), 
			"UInt16" => ushort.Parse(valueStr), 
			"Int32" => int.Parse(valueStr), 
			"UInt32" => uint.Parse(valueStr), 
			"Int64" => long.Parse(valueStr), 
			"UInt64" => ulong.Parse(valueStr), 
			"Single" => float.Parse(valueStr, CultureInfo.InvariantCulture), 
			"Double" => double.Parse(valueStr, CultureInfo.InvariantCulture), 
			"Guid" => Guid.Parse(valueStr), 
			_ => valueStr, 
		};
	}

	internal static object DeepCopyRawData(object obj)
	{
		if (obj == null)
		{
			return null;
		}
		if (obj is Dictionary<string, object> dictionary)
		{
			Dictionary<string, object> dictionary2 = new Dictionary<string, object>(dictionary.Count);
			{
				foreach (KeyValuePair<string, object> item in dictionary)
				{
					dictionary2[item.Key] = DeepCopyRawData(item.Value);
				}
				return dictionary2;
			}
		}
		if (obj is List<object> list)
		{
			List<object> list2 = new List<object>(list.Count);
			{
				foreach (object item2 in list)
				{
					list2.Add(DeepCopyRawData(item2));
				}
				return list2;
			}
		}
		if (obj is object[] array)
		{
			object[] array2 = new object[array.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array2[i] = DeepCopyRawData(array[i]);
			}
			return array2;
		}
		if (obj is float[] array3)
		{
			float[] array4 = new float[array3.Length];
			Buffer.BlockCopy(array3, 0, array4, 0, array3.Length * 4);
			return array4;
		}
		if (obj is byte[] array5)
		{
			byte[] array6 = new byte[array5.Length];
			Buffer.BlockCopy(array5, 0, array6, 0, array5.Length);
			return array6;
		}
		if (obj is uint[] array7)
		{
			uint[] array8 = new uint[array7.Length];
			Buffer.BlockCopy(array7, 0, array8, 0, array7.Length * 4);
			return array8;
		}
		if (obj is ushort[] array9)
		{
			ushort[] array10 = new ushort[array9.Length];
			Buffer.BlockCopy(array9, 0, array10, 0, array9.Length * 2);
			return array10;
		}
		if (obj is IEnumerable enumerable && !(obj is string))
		{
			List<object> list3 = new List<object>();
			{
				foreach (object item3 in enumerable)
				{
					list3.Add(DeepCopyRawData(item3));
				}
				return list3;
			}
		}
		return obj;
	}

	private static ulong GenerateRandom64BitKey()
	{
		using RNGCryptoServiceProvider rNGCryptoServiceProvider = new RNGCryptoServiceProvider();
		byte[] array = new byte[8];
		rNGCryptoServiceProvider.GetBytes(array);
		return BitConverter.ToUInt64(array, 0);
	}

	public void DuplicateAsset(AntAssetViewModel sourceVm)
	{
		if (sourceVm == null || sourceVm.AssetInstance == null || !(sourceVm.AssetInstance is AntAsset antAsset) || _bankBytes == null)
		{
			return;
		}
		bool bankIsBigEndian = _bankIsBigEndian;
		Dictionary<uint, GenericClass2> classes = _bank.Classes2;
		try
		{
			Dictionary<string, object> dictionary = (Dictionary<string, object>)DeepCopyRawData(antAsset.RawData);
			ulong num = GenerateRandom64BitKey();
			Guid guid = KeyToGuid(num);
			DuplicateRenameWindow duplicateRenameWindow = new DuplicateRenameWindow(MakeCopyName(antAsset.Name), _bank.DataNames.Keys)
			{
				Owner = Window.GetWindow(this)
			};
			if (duplicateRenameWindow.ShowDialog() != true)
			{
				return;
			}
			object obj = (dictionary["__name"] = duplicateRenameWindow.NewName);
			string text = (string)obj;
			dictionary["__guid"] = guid;
			dictionary["__key"] = num;
			Type type = antAsset.GetType();
			AntAsset newAsset = (AntAsset)Activator.CreateInstance(type);
			newAsset.AssetType = antAsset.AssetType;
			newAsset.Bank = antAsset.Bank;
			newAsset.RawData = dictionary;
			newAsset.SetData(dictionary);
			byte[] section = DynamicDat2Serializer.Serialize(newAsset, classes, bankIsBigEndian);
			_bankBytes = AppendSection(_bankBytes, section);
			_bankBytes = BankHeaderPatcher.PatchHeaderForKeys(_bankBytes, new ulong[1] { num }, bankIsBigEndian);
			_bank.DataNames[text] = guid;
			if (Cache.AntStateBundleIndices.TryGetValue(antAsset.ID, out var value))
			{
				Cache.AntStateBundleIndices[guid] = value;
			}
			AntRefTable.Add(newAsset);
			AntAssetViewModel antAssetViewModel = new AntAssetViewModel
			{
				Name = text,
				Id = guid,
				AssetInstance = newAsset,
				ParentEditor = this,
				IsNew = true,
				IsUnsaved = true
			};
			AntTypeGroup antTypeGroup = _masterGroups.FirstOrDefault((AntTypeGroup g) => g.TypeName == newAsset.AssetType);
			if (antTypeGroup != null)
			{
				int num2 = antTypeGroup.Assets.IndexOf(sourceVm);
				if (num2 < 0)
				{
					num2 = antTypeGroup.Assets.Count - 1;
				}
				antTypeGroup.Assets.Insert(num2 + 1, antAssetViewModel);
				_selectedAsset = antAssetViewModel;
			}
			AntTypeGroup antTypeGroup2 = _filteredGroups.FirstOrDefault((AntTypeGroup g) => g.TypeName == newAsset.AssetType);
			if (antTypeGroup2 == null)
			{
				antTypeGroup2 = new AntTypeGroup
				{
					TypeName = newAsset.AssetType
				};
				_filteredGroups.Add(antTypeGroup2);
				antTypeGroup2.Assets.Add(antAssetViewModel);
			}
			else if (antTypeGroup2 != antTypeGroup)
			{
				int num3 = antTypeGroup2.Assets.IndexOf(sourceVm);
				antTypeGroup2.Assets.Insert((num3 >= 0) ? (num3 + 1) : antTypeGroup2.Assets.Count, antAssetViewModel);
			}
			App.Logger.Log($"[AntStateEditor] Duplicated asset '{antAsset.Name}' -> '{text}' dynamically (key: {num:X16}).");
		}
		catch (Exception ex)
		{
			App.Logger.LogError("[AntStateEditor] Dynamic duplication failed: " + ex.Message + "\n" + ex.StackTrace);
			FrostyMessageBox.Show("Failed to duplicate asset: " + ex.Message, "Duplication Failed");
		}
	}

	public void ImportAnimation(AntAssetViewModel sourceVm)
	{
		object obj = sourceVm?.AssetInstance;
		VbrAnimationAsset template = obj as VbrAnimationAsset;
		if (template == null || _bankBytes == null)
		{
			return;
		}
		if (template.OrderedChannels == null || template.OrderedChannels.Count == 0)
		{
			template.Channels = template.GetChannels(template.ChannelToDofAsset);
		}
		if (IsLegacyBank && (template.OrderedChannels == null || template.OrderedChannels.Count == 0))
		{
			// GW1/GW2 banks that only hold animations: the bone names are in the other banks of the level.
			try
			{
				int bundleId = ((_bankResEntry != null) ? _bankResEntry.Bundles[0] : _bankChunkEntry.Bundles[0]);
				int num2 = BankImporter.LoadLevelBanks(bundleId);
				App.Logger.Log("[AntStateEditor] This bank has no rig of its own: read " + num2 + " bundle(s) of its level to resolve the bones.");
				template.Channels = template.GetChannels(template.ChannelToDofAsset);
			}
			catch (Exception ex4)
			{
				App.Logger.LogWarning("[AntStateEditor] Could not read the other banks of the level: " + ex4.Message);
			}
		}
		if (template.OrderedChannels != null && template.OrderedChannels.Count > 0)
		{
			App.Logger.Log("[DBG] OrderedChannels count=" + template.OrderedChannels.Count);
			int num = Math.Min(8, template.OrderedChannels.Count);
			for (int i = 0; i < num; i++)
			{
				ChannelInfo channelInfo = template.OrderedChannels[i];
				App.Logger.Log("[DBG] ch[" + i + "] name='" + channelInfo.Name + "' type=" + channelInfo.Type);
			}
		}
		else
		{
			App.Logger.Log("[DBG] OrderedChannels is NULL or EMPTY after GetChannels");
		}
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = "Import Animation",
			Filter = "Supported Animations (*.fbx;*.dae;*.gltf;*.glb)|*.fbx;*.dae;*.gltf;*.glb|All Files (*.*)|*.*",
			DefaultExt = ".fbx"
		};
		if (openFileDialog.ShowDialog() != true)
		{
			return;
		}
		string filePath = openFileDialog.FileName;
		FrostyTaskWindow.Show("Importing Animation", "", delegate
		{
			try
			{
				byte[] array = null;
				Scene scene = new AssimpContext().ImportFile(filePath, PostProcessSteps.None);
				array = (IsLegacyBank ? AnimationImporter.ImportLegacy(scene, _bankBytes, template, _bank.Classes) : AnimationImporter.Import(scene, _bankBytes, template, _bank.Classes2, _bankIsBigEndian));
				if (array != null)
				{
					_bankBytes = array;
					App.Logger.Log("[AntStateEditor] Imported animation into '" + template.Name + "' from '" + Path.GetFileName(filePath) + "'.");
					Application.Current.Dispatcher.Invoke(delegate
					{
						sourceVm.MarkModified();
						sourceVm.ResetProperties();
						_currentPreviewAsset = template;
						_currentPreviewName = template.Name;
						LoadPreviewAsync(template, _currentPreviewName);
						FrostyMessageBox.Show("Animation imported successfully.\nClick Save to commit changes.", "Import Complete");
					});
				}
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Exception ex3 = ex2;
				App.Logger.LogError("[AntStateEditor] Import failed: " + ex3.Message + "\n" + ex3.StackTrace);
				Application.Current.Dispatcher.Invoke(() => FrostyMessageBox.Show("Import failed: " + ex3.Message, "Import Failed"));
			}
		});
	}

	public void ImportAnimationAllBanks(AntAssetViewModel sourceVm)
	{
		if (!(sourceVm?.AssetInstance is VbrAnimationAsset vbrAnimationAsset) || _bankBytes == null)
		{
			return;
		}
		string animationName = vbrAnimationAsset.Name;
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = "Import Animation In All Banks",
			Filter = "Supported Animations (*.fbx;*.dae;*.gltf;*.glb)|*.fbx;*.dae;*.gltf;*.glb|All Files (*.*)|*.*",
			DefaultExt = ".fbx"
		};
		if (openFileDialog.ShowDialog() != true)
		{
			return;
		}
		string filePath = openFileDialog.FileName;
		bool cancelled = false;
		FrostyTaskWindow.Show("Importing in all banks", "Reading file...", delegate(FrostyTaskWindow task)
		{
			BankImporter.Report report = null;
			string text = null;
			try
			{
				report = BankImporter.Import(filePath, animationName, delegate(string message)
				{
					App.Logger.Log("[AntStateEditor] " + message);
					task?.Update(message);
				}, delegate(string message, double value)
				{
					task?.Update(message, value);
				}, () => cancelled);
			}
			catch (Exception ex)
			{
				text = ex.Message;
				App.Logger.LogError("[AntStateEditor] Import into all banks failed: " + ex.Message + "\n" + ex.StackTrace);
			}
			BankImporter.Report finalReport = report;
			string finalError = text;
			Application.Current.Dispatcher.Invoke((Func<Task>)async delegate
			{
				if (finalError != null)
				{
					FrostyMessageBox.Show("Import failed: " + finalError, "Import Failed");
				}
				else
				{
					await LoadAsync();
					StringBuilder stringBuilder = new StringBuilder();
					stringBuilder.Append("'").Append(animationName).Append("' written to ")
						.Append(finalReport.Written.Count)
						.Append(" bank(s).");
					if (finalReport.Written.Count > 0)
					{
						stringBuilder.Append("\n\n");
						foreach (string item in finalReport.Written)
						{
							stringBuilder.Append("· ").Append(item).Append('\n');
						}
					}
					if (finalReport.Failed.Count > 0)
					{
						stringBuilder.Append("\nFailed (").Append(finalReport.Failed.Count).Append("), see the log:");
						foreach (string item2 in finalReport.Failed)
						{
							stringBuilder.Append("\n· ").Append(item2);
						}
					}
					stringBuilder.Append("\n\nNo need to press Save. Export the mod and launch the game from this Frosty.");
					App.Logger.Log("[AntStateEditor] Import into all banks finished: " + finalReport.Written.Count + " written, " + finalReport.NotFound + " without the animation, " + finalReport.Failed.Count + " failed.");
					FrostyMessageBox.Show(stringBuilder.ToString(), "Import Complete");
				}
			});
		}, showCancelButton: true, delegate
		{
			cancelled = true;
		});
	}

	public void EditAnimAssets(AntAssetViewModel sourceVm)
	{
		if (sourceVm == null || sourceVm.AssetInstance == null || !(sourceVm.AssetInstance is AntAsset antAsset) || antAsset.AssetType != "ActorControllerAsset" || _bankBytes == null)
		{
			return;
		}
		bool bankIsBigEndian = _bankIsBigEndian;
		ulong assetKey = GuidToKey(sourceVm.Id);
		int num = FindDat2SectionStart(_bankBytes, 3127372384u, assetKey, 48, bankIsBigEndian);
		if (num < 0)
		{
			FrostyMessageBox.Show("Could not find ActorControllerAsset section.", "Error");
			return;
		}
		int num2 = (int)ReadU32(_bankBytes, num + 64, bankIsBigEndian);
		if (num2 <= 0)
		{
			FrostyMessageBox.Show("No tracks found in this asset.", "Edit Anim Assets");
			return;
		}
		List<AnimAssetEntry> list = new List<AnimAssetEntry>();
		for (int i = 0; i < num2; i++)
		{
			int num3 = num + 68;
			long num4 = Decode60Bit(ReadU64(_bankBytes, num3, bankIsBigEndian));
			int num5 = (int)(num3 + num4) + i * 8;
			if (num5 + 8 > _bankBytes.Length)
			{
				break;
			}
			long num6 = Decode60Bit(ReadU64(_bankBytes, num5, bankIsBigEndian));
			int num7 = (int)(num5 + num6);
			if (num7 + 52 + 4 > _bankBytes.Length)
			{
				break;
			}
			int num8 = (int)ReadU32(_bankBytes, num7 + 52, bankIsBigEndian);
			for (int j = 0; j < num8; j++)
			{
				int num9 = num7 + 56;
				if (num9 + 8 > _bankBytes.Length)
				{
					break;
				}
				long num10 = Decode60Bit(ReadU64(_bankBytes, num9, bankIsBigEndian));
				int num11 = (int)(num9 + num10) + j * 8;
				if (num11 + 8 > _bankBytes.Length)
				{
					break;
				}
				long num12 = Decode60Bit(ReadU64(_bankBytes, num11, bankIsBigEndian));
				int num13 = (int)(num11 + num12) + 32;
				if (num13 + 8 > _bankBytes.Length)
				{
					break;
				}
				ulong currentKey = ReadU64(_bankBytes, num13, bankIsBigEndian);
				list.Add(new AnimAssetEntry
				{
					TrackIndex = i,
					AnimIndex = j,
					AssetOffset = num13,
					CurrentKey = currentKey
				});
			}
		}
		if (list.Count == 0)
		{
			FrostyMessageBox.Show("No animation entries found in track list.", "Edit Anim Assets");
		}
		else
		{
			if (new EditAnimAssetsWindow(list)
			{
				Owner = Window.GetWindow(this)
			}.ShowDialog() != true)
			{
				return;
			}
			bool flag = false;
			foreach (AnimAssetEntry item in list)
			{
				if (item.WasEdited)
				{
					PatchU64InPlace(_bankBytes, item.AssetOffset, item.NewKey, bankIsBigEndian);
					flag = true;
					App.Logger.Log($"[AntStateEditor] Patched Track[{item.TrackIndex}] Anim[{item.AnimIndex}] Asset -> {item.NewKey:X16}");
				}
			}
			if (!flag)
			{
				return;
			}
			try
			{
				AnimationOptions animationOptions = new AnimationOptions();
				animationOptions.Load();
				CompressionType selectedCompressionType = animationOptions.GetSelectedCompressionType();
				if (_bankResEntry != null)
				{
					ModifyResWithFastCompression(_bankResEntry, _bankBytes, selectedCompressionType);
				}
				else
				{
					if (_bankChunkEntry == null)
					{
						throw new InvalidOperationException("the bank (res or chunk) behind '" + sourceVm?.Name + "' was not found; nothing was written.");
					}
					App.AssetManager.ModifyChunk(_bankChunkEntry.Id, _bankBytes, selectedCompressionType);
					if (_bankChunkEntry.ModifiedEntry == null || _bankChunkEntry.ModifiedEntry.Data == null)
					{
						Guid id = _bankChunkEntry.Id;
						throw new InvalidOperationException("chunk " + id.ToString() + " is not in the asset manager; nothing was written.");
					}
					_bankChunkEntry.ModifiedEntry.OriginalSize = (uint)_bankBytes.Length;
				}
				ILogger obj = App.Logger;
				string[] obj2 = new string[7]
				{
					"[AntStateEditor] Bank written: ",
					sourceVm?.Name,
					" | ",
					null,
					null,
					null,
					null
				};
				string text;
				if (_bankResEntry == null)
				{
					Guid id = _bankChunkEntry.Id;
					text = "chunk " + id.ToString() + ", ";
				}
				else
				{
					text = "res '" + _bankResEntry.Name + "', ";
				}
				obj2[3] = text;
				obj2[4] = _bankBytes.Length.ToString("N0");
				obj2[5] = " bytes, compresion ";
				obj2[6] = selectedCompressionType.ToString();
				obj.Log(string.Concat(obj2));
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Exception ex3 = ex2;
				App.Logger.LogError("[AntStateEditor] Failed to write bank bytes: " + ex3.Message);
				Application.Current.Dispatcher.Invoke(() => FrostyMessageBox.Show("Failed to write bank: " + ex3.Message, "Save Failed"));
			}
		}
	}

	private static Dictionary<string, object> GetOriginalAssetRawData(ResAssetEntry resEntry, ChunkAssetEntry chunkEntry, int bundleId, Guid assetId)
	{
		byte[] array = null;
		if (resEntry != null)
		{
			ModifiedAssetEntry modifiedEntry = resEntry.ModifiedEntry;
			resEntry.ModifiedEntry = null;
			using (Stream stream = App.AssetManager.GetRes(resEntry))
			{
				if (stream != null)
				{
					using MemoryStream memoryStream = new MemoryStream();
					stream.CopyTo(memoryStream);
					array = memoryStream.ToArray();
				}
			}
			resEntry.ModifiedEntry = modifiedEntry;
		}
		else if (chunkEntry != null)
		{
			ModifiedAssetEntry modifiedEntry2 = chunkEntry.ModifiedEntry;
			chunkEntry.ModifiedEntry = null;
			using (Stream stream2 = App.AssetManager.GetChunk(chunkEntry))
			{
				if (stream2 != null)
				{
					using MemoryStream memoryStream2 = new MemoryStream();
					stream2.CopyTo(memoryStream2);
					array = memoryStream2.ToArray();
				}
			}
			chunkEntry.ModifiedEntry = modifiedEntry2;
		}
		if (array == null)
		{
			return null;
		}
		Dictionary<string, object> result = null;
		using NativeReader r = new NativeReader(new MemoryStream(array));
		ConcurrentDictionary<Guid, AntAsset> refs = AntRefTable.Refs;
		ConcurrentDictionary<Guid, Guid> internalRefs = AntRefTable.InternalRefs;
		ConcurrentDictionary<Guid, AntAsset> concurrentDictionary = new ConcurrentDictionary<Guid, AntAsset>();
		ConcurrentDictionary<Guid, Guid> concurrentDictionary2 = new ConcurrentDictionary<Guid, Guid>();
		AntRefTable.Refs = concurrentDictionary;
		AntRefTable.InternalRefs = concurrentDictionary2;
		new Bank(r, bundleId);
		AntAsset antAsset = AntRefTable.Get(assetId);
		if (antAsset != null)
		{
			result = (Dictionary<string, object>)DeepCopyRawData(antAsset.RawData);
		}
		concurrentDictionary.Clear();
		concurrentDictionary2.Clear();
		AntRefTable.Refs = refs;
		AntRefTable.InternalRefs = internalRefs;
		return result;
	}

	public void RevertAsset(AntAssetViewModel vm)
	{
		if (vm == null)
		{
			return;
		}
		if (vm.IsNew)
		{
			if (FrostyMessageBox.Show("Are you sure you want to delete the duplicated asset '" + vm.Name + "'?", "Delete Asset", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
			{
				return;
			}
			object assetInstance = vm.AssetInstance;
			AntAsset asset = assetInstance as AntAsset;
			if (asset == null)
			{
				return;
			}
			AntRefTable.Refs.TryRemove(vm.Id, out var _);
			_bank?.DataNames.Remove(vm.Name);
			AntTypeGroup antTypeGroup = _masterGroups.FirstOrDefault((AntTypeGroup g) => g.TypeName == asset.AssetType);
			antTypeGroup?.Assets.Remove(vm);
			AntTypeGroup antTypeGroup2 = _filteredGroups.FirstOrDefault((AntTypeGroup g) => g.TypeName == asset.AssetType);
			antTypeGroup2?.Assets.Remove(vm);
			if (antTypeGroup != null && antTypeGroup.Assets.Count == 0)
			{
				_masterGroups.Remove(antTypeGroup);
			}
			if (antTypeGroup2 != null && antTypeGroup2.Assets.Count == 0)
			{
				_filteredGroups.Remove(antTypeGroup2);
			}
			bool bankIsBigEndian = _bankIsBigEndian;
			Dictionary<uint, GenericClass2> classes = _bank.Classes2;
			uint typeHashForAsset = DynamicDat2Serializer.GetTypeHashForAsset(asset, classes);
			ulong num = GuidToKey(asset.ID);
			int keyFieldOffset = 16;
			if (classes.TryGetValue(typeHashForAsset, out var value2))
			{
				GenericField2 genericField = value2.Elements.FirstOrDefault((GenericField2 f) => f.Name == "__key" || f.Name == "__guid");
				if (genericField != null)
				{
					keyFieldOffset = genericField.Offset;
				}
			}
			int num2 = FindDat2SectionStart(_bankBytes, typeHashForAsset, num, keyFieldOffset, bankIsBigEndian);
			if (num2 >= 0)
			{
				int num3 = (int)ReadU32(_bankBytes, num2 + 8, bankIsBigEndian);
				byte[] array = new byte[_bankBytes.Length - num3];
				Buffer.BlockCopy(_bankBytes, 0, array, 0, num2);
				Buffer.BlockCopy(_bankBytes, num2 + num3, array, num2, _bankBytes.Length - num2 - num3);
				_bankBytes = array;
				_bankBytes = BankHeaderPatcher.PatchHeaderForKeys(_bankBytes, Array.Empty<ulong>(), bankIsBigEndian, new ulong[1] { num });
			}
			_isBankBytesDirty = true;
			base.AssetModified = true;
			App.Logger.Log("[AntStateEditor] Deleted duplicated asset '" + vm.Name + "'. Click 'Save' to commit changes.");
		}
		else
		{
			if (FrostyMessageBox.Show("Are you sure you want to revert '" + vm.Name + "' to its original base-game state?", "Revert Asset", MessageBoxButton.YesNo) != MessageBoxResult.Yes || !(vm.AssetInstance is AntAsset antAsset))
			{
				return;
			}
			int bundleId = ((_bankResEntry != null) ? _bankResEntry.Bundles[0] : _bankChunkEntry.Bundles[0]);
			Dictionary<string, object> originalAssetRawData = GetOriginalAssetRawData(_bankResEntry, _bankChunkEntry, bundleId, vm.Id);
			if (originalAssetRawData == null)
			{
				FrostyMessageBox.Show("Failed to retrieve original asset data.", "Error");
				return;
			}
			antAsset.RawData = originalAssetRawData;
			antAsset.SetData(originalAssetRawData);
			vm.ResetProperties();
			vm.IsModified = false;
			vm.IsUnsaved = true;
			_isBankBytesDirty = true;
			base.AssetModified = true;
			if (antAsset is AnimationAsset animationAsset && _currentPreviewAsset == animationAsset)
			{
				LoadPreviewAsync(animationAsset, _currentPreviewName);
			}
			App.Logger.Log("[AntStateEditor] Reverted asset '" + vm.Name + "' back to original. Click 'Save' to write back.");
		}
	}

	public static ulong GuidToKey(Guid g)
	{
		return BitConverter.ToUInt64(g.ToByteArray(), 0);
	}

	public static Guid KeyToGuid(ulong key)
	{
		byte[] array = new byte[16];
		BitConverter.GetBytes(key).CopyTo(array, 0);
		return new Guid(array);
	}

	private static int FindDat2SectionStart(byte[] bank, uint typeHash, ulong assetKey, int keyFieldOffset, bool big)
	{
		int num = 44 + keyFieldOffset;
		int num2 = 0;
		while (num2 + 12 <= bank.Length)
		{
			if (bank[num2] == 71 && bank[num2 + 1] == 68 && bank[num2 + 2] == 46 && bank[num2 + 3] == 68 && bank[num2 + 4] == 65 && bank[num2 + 5] == 84 && bank[num2 + 6] == 50)
			{
				uint val = ReadU32(bank, num2 + 8, big);
				if (num2 + 32 <= bank.Length && ReadU32(bank, num2 + 28, big) == typeHash && num2 + num + 8 <= bank.Length && ReadU64(bank, num2 + num, big) == assetKey)
				{
					return num2;
				}
				num2 += (int)Math.Max(val, 1u);
			}
			else
			{
				num2++;
			}
		}
		return -1;
	}

	private static byte[] SpliceSection(byte[] bank, int sectionStart, byte[] newSection, bool big)
	{
		int num = (int)ReadU32(bank, sectionStart + 8, big);
		byte[] array = new byte[bank.Length - num + newSection.Length];
		Buffer.BlockCopy(bank, 0, array, 0, sectionStart);
		Buffer.BlockCopy(newSection, 0, array, sectionStart, newSection.Length);
		Buffer.BlockCopy(bank, sectionStart + num, array, sectionStart + newSection.Length, bank.Length - sectionStart - num);
		return array;
	}

	private static string MakeCopyName(string original)
	{
		if (!original.Contains("_copy"))
		{
			return original + "_copy";
		}
		int num = original.LastIndexOf("_copy", StringComparison.Ordinal);
		if (int.TryParse(original.Substring(num + 5), out var result))
		{
			return original.Substring(0, num + 5) + (result + 1);
		}
		return original + "2";
	}

	private static byte[] AppendSection(byte[] bank, byte[] section)
	{
		byte[] array = new byte[bank.Length + section.Length];
		Buffer.BlockCopy(bank, 0, array, 0, bank.Length);
		Buffer.BlockCopy(section, 0, array, bank.Length, section.Length);
		return array;
	}

	private static bool DetectBigEndian(byte[] bank)
	{
		for (int i = 0; i + 8 <= bank.Length; i++)
		{
			if (bank[i] == 71 && bank[i + 1] == 68 && bank[i + 2] == 46)
			{
				return bank[i + 7] == 98;
			}
		}
		return false;
	}

	private static uint ReadU32(byte[] b, int o, bool big)
	{
		if (!big)
		{
			return (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));
		}
		return (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);
	}

	private static ulong ReadU64(byte[] b, int o, bool big)
	{
		if (!big)
		{
			return ReadU64LE(b, o);
		}
		return ReadU64BE(b, o);
	}

	private static float ReadF32(byte[] b, int o, bool big)
	{
		return BitConverter.ToSingle(big ? new byte[4]
		{
			b[o + 3],
			b[o + 2],
			b[o + 1],
			b[o]
		} : new byte[4]
		{
			b[o],
			b[o + 1],
			b[o + 2],
			b[o + 3]
		}, 0);
	}

	private static ulong ReadU64LE(byte[] b, int o)
	{
		return b[o] | ((ulong)b[o + 1] << 8) | ((ulong)b[o + 2] << 16) | ((ulong)b[o + 3] << 24) | ((ulong)b[o + 4] << 32) | ((ulong)b[o + 5] << 40) | ((ulong)b[o + 6] << 48) | ((ulong)b[o + 7] << 56);
	}

	private static ulong ReadU64BE(byte[] b, int o)
	{
		return ((ulong)b[o] << 56) | ((ulong)b[o + 1] << 48) | ((ulong)b[o + 2] << 40) | ((ulong)b[o + 3] << 32) | ((ulong)b[o + 4] << 24) | ((ulong)b[o + 5] << 16) | ((ulong)b[o + 6] << 8) | b[o + 7];
	}

	private static long Decode60Bit(ulong rawEncoded)
	{
		return (long)(rawEncoded << 4) >> 4;
	}

	private static void PatchU64InPlace(byte[] bank, int offset, ulong value, bool big)
	{
		if (big)
		{
			ulong num = value;
			num = ((num & 0xFF) << 56) | (((num >> 8) & 0xFF) << 48) | (((num >> 16) & 0xFF) << 40) | (((num >> 24) & 0xFF) << 32) | (((num >> 32) & 0xFF) << 24) | (((num >> 40) & 0xFF) << 16) | (((num >> 48) & 0xFF) << 8) | ((num >> 56) & 0xFF);
			value = num;
		}
		for (int i = 0; i < 8; i++)
		{
			bank[offset + i] = (byte)(value >> i * 8);
		}
	}

	private async Task LoadPreviewAsync(AnimationAsset animAsset, string displayName = null)
	{
		try
		{
			await Task.Run(delegate
			{
				if (!string.IsNullOrEmpty(displayName))
				{
					animAsset.Name = displayName;
				}
				animAsset.Channels = animAsset.GetChannels(animAsset.ChannelToDofAsset);
				InternalAnimation internalAnim = animAsset.ConvertToInternal();
				if (internalAnim == null)
				{
					App.Logger.LogError("[AntStateEditor] ConvertToInternal returned null for '" + animAsset.Name + "'. Check that reference banks are loaded.");
				}
				else
				{
					InternalSkeleton internalSkeleton = null;
					if (!_skeletonOverrideActive)
					{
						try
						{
							EbxAssetEntry ebxAssetEntry = null;
							Guid channelToDofAsset = animAsset.ChannelToDofAsset;
							Guid guid = channelToDofAsset;
							if (guid != Guid.Empty)
							{
								ebxAssetEntry = App.AssetManager.GetEbxEntry(guid);
							}
							if (ebxAssetEntry == null)
							{
								ebxAssetEntry = TryResolveEbxRef(channelToDofAsset);
							}
							if (ebxAssetEntry != null)
							{
								App.Logger.Log("[AntStateEditor] Rig asset: " + ebxAssetEntry.Name);
								dynamic rootObject = App.AssetManager.GetEbx(ebxAssetEntry).RootObject;
								try
								{
									internalSkeleton = SkeletonAssetExport.ConvertToInternal(rootObject);
									if (internalSkeleton != null)
									{
										List<string> boneNames = internalSkeleton.BoneNames;
										if (boneNames != null && boneNames.Count == 0)
										{
											internalSkeleton = null;
										}
									}
								}
								catch
								{
									internalSkeleton = null;
								}
								if (internalSkeleton == null)
								{
									try
									{
										EbxAssetEntry ebxAssetEntry2 = TryResolveEbxRef((object)rootObject.Skeleton);
										if (ebxAssetEntry2 != null)
										{
											internalSkeleton = SkeletonAssetExport.ConvertToInternal(App.AssetManager.GetEbx(ebxAssetEntry2).RootObject);
										}
									}
									catch
									{
									}
								}
							}
						}
						catch (Exception ex2)
						{
							App.Logger.LogWarning("[AntStateEditor] Rig skeleton failed: " + ex2.Message);
						}
					}
					if (internalSkeleton == null)
					{
						string text = ((!_skeletonOverrideActive) ? "" : _currentSkeletonPath?.Trim());
						if (string.IsNullOrEmpty(text))
						{
							AnimationOptions animationOptions = new AnimationOptions();
							animationOptions.Load();
							text = animationOptions.ExportSkeletonAsset?.Trim() ?? "";
						}
						App.Logger.Log($"[AntStateEditor] Fallback skeleton: '{text}' (override={_skeletonOverrideActive})");
						if (string.IsNullOrEmpty(text))
						{
							App.Logger.LogWarning("[AntStateEditor] No skeleton available.");
							return;
						}
						EbxAssetEntry ebxEntry = App.AssetManager.GetEbxEntry(text);
						if (ebxEntry == null)
						{
							App.Logger.LogError("[AntStateEditor] Skeleton not found: " + text);
							return;
						}
						internalSkeleton = SkeletonAssetExport.ConvertToInternal(App.AssetManager.GetEbx(ebxEntry).RootObject);
					}
					MeshRenderSkeleton renderSkel = AntSkeletonConverter.Convert(internalSkeleton);
					int val = ((internalAnim.Frames.Count <= 0) ? 1 : (internalAnim.Frames[internalAnim.Frames.Count - 1].FrameIndex + 1));
					int endFrame = Math.Max(animAsset.EndFrame + 1, val);
					MeshRenderAnim renderAnim = AntAnimationConverter.Convert(internalAnim, endFrame);
					base.Dispatcher.Invoke(delegate
					{
						m_screen.LoadSkeleton(renderSkel);
						m_screen.LoadAnimation(renderAnim, internalAnim, endFrame);
						UpdateMeshSkeletons();
						if (m_playPauseBtn != null)
						{
							m_playPauseBtn.IsChecked = true;
						}
						if (m_timelineSlider != null)
						{
							m_timelineSlider.Maximum = endFrame;
							m_timelineSlider.Value = 0.0;
						}
						if (m_totalFramesLabel != null)
						{
							m_totalFramesLabel.Text = endFrame.ToString();
						}
						if (m_frameLabel != null)
						{
							m_frameLabel.Text = "0";
						}
					});
				}
			});
		}
		catch (Exception ex)
		{
			App.Logger.LogError("[AntStateEditor] Preview failed for '" + animAsset.Name + "': " + ex.Message);
		}
	}

	private async Task LoadMeshAsync(string meshEbxPath)
	{
		if (string.IsNullOrEmpty(meshEbxPath))
		{
			return;
		}
		if (ProfilesLibrary.DataVersion != 20190905)
		{
			if (!MeshVariationDb.IsLoaded)
			{
				Application.Current.Dispatcher.Invoke(delegate
				{
					FrostyTaskWindow.Show("Loading Variations", "", MeshVariationDb.LoadVariations);
				});
			}
			MeshVariationDb.LoadModifiedVariations();
		}
		EbxAssetEntry ebxEntry = App.AssetManager.GetEbxEntry(meshEbxPath);
		if (ebxEntry == null)
		{
			App.Logger.LogError("[AntStateEditor] Asset not found: " + meshEbxPath);
			return;
		}
		try
		{
			EbxAsset ebx = App.AssetManager.GetEbx(ebxEntry);
			string name = ebx.RootObject.GetType().Name;
			if (name == "VisualCustomizationSetAsset")
			{
				await LoadSkinAsync(ebx);
			}
			else if (name == "VisualCustomizationAsset")
			{
				await LoadVisualAssetAsync(ebx, clearFirst: true, -1);
			}
			else
			{
				await LoadSingleMeshAsync(ebx, ebxEntry, clearFirst: true);
			}
		}
		catch (Exception ex)
		{
			App.Logger.LogError("[AntStateEditor] Load failed: " + ex.Message);
		}
	}

	private static Guid GetFileGuid(object refObj)
	{
		if (refObj == null)
		{
			return Guid.Empty;
		}
		try
		{
			try
			{
				return (Guid)((dynamic)refObj).External.FileGuid;
			}
			catch
			{
			}
			try
			{
				return (Guid)((dynamic)refObj).External.ExternalGuid;
			}
			catch
			{
			}
			try
			{
				return (Guid)((dynamic)refObj).ExternalGuid;
			}
			catch
			{
			}
		}
		catch
		{
		}
		return Guid.Empty;
	}

	private static Guid GetClassGuid(object refObj)
	{
		if (refObj == null)
		{
			return Guid.Empty;
		}
		try
		{
			try
			{
				return (Guid)((dynamic)refObj).External.ClassGuid;
			}
			catch
			{
			}
			try
			{
				return (Guid)((dynamic)refObj).Internal.GetInstanceGuid().ExportedGuid;
			}
			catch
			{
			}
		}
		catch
		{
		}
		return Guid.Empty;
	}

	private async Task LoadSingleMeshAsync(EbxAsset ebx, EbxAssetEntry entry, bool clearFirst, PointerRef variationRef = default(PointerRef), string bpbName = null, int replaceIndex = -1)
	{
		await Task.Run(delegate
		{
			dynamic root = ebx.RootObject;
			ulong resRid = root.MeshSetResource;
			ResAssetEntry resEntry = App.AssetManager.GetResEntry(resRid);
			MeshSet meshSet = App.AssetManager.GetResAs<MeshSet>(resEntry);
			MeshMaterialCollection materials = new MeshMaterialCollection(ebx, (bpbName != null && variationRef.Type != PointerRefType.Null) ? default(PointerRef) : variationRef);
			if (!string.IsNullOrEmpty(bpbName) && variationRef.Type != PointerRefType.Null)
			{
				try
				{
					string name = bpbName + "/MeshVariationDb_Win32";
					EbxAssetEntry ebxEntry = App.AssetManager.GetEbxEntry(name);
					if (ebxEntry != null)
					{
						dynamic rootObject = App.AssetManager.GetEbx(ebxEntry).RootObject;
						foreach (dynamic item in rootObject.Entries)
						{
							if (GetFileGuid((object)item.Mesh) == entry.Guid)
							{
								foreach (dynamic item2 in item.Materials)
								{
									object refObj = item2.Material;
									object obj = item2.MaterialVariation;
									if (obj != null)
									{
										Guid fileGuid = GetFileGuid(obj);
										Guid classGuid = GetClassGuid(obj);
										Guid classGuid2 = GetClassGuid(refObj);
										if (fileGuid != Guid.Empty && classGuid != Guid.Empty && classGuid2 != Guid.Empty)
										{
											MeshMaterial meshMaterial = null;
											for (int i = 0; i < materials.Count; i++)
											{
												if (materials[i].Guid == classGuid2)
												{
													meshMaterial = materials[i];
													break;
												}
											}
											if (meshMaterial != null)
											{
												EbxAssetEntry ebxEntry2 = App.AssetManager.GetEbxEntry(fileGuid);
												if (ebxEntry2 != null)
												{
													EbxAsset ebx2 = App.AssetManager.GetEbx(ebxEntry2);
													dynamic val = null;
													foreach (dynamic @object in ebx2.Objects)
													{
														if (((AssetClassGuid)@object.GetInstanceGuid()).ExportedGuid == classGuid)
														{
															val = @object;
															break;
														}
													}
													if (val != null)
													{
														dynamic val2 = val.Shader.Shader;
														if ((object)val2 != null && GetFileGuid((object)val2) != Guid.Empty)
														{
															meshMaterial.Shader = val2;
															meshMaterial.TextureParameters.Clear();
															meshMaterial.VectorParameters.Clear();
															meshMaterial.BoolParameters.Clear();
															meshMaterial.ConditionalParameters.Clear();
														}
														foreach (dynamic param in val.Shader.BoolParameters)
														{
															int num = meshMaterial.BoolParameters.FindIndex((dynamic a) => a.ParameterName.Equals(param.ParameterName, StringComparison.OrdinalIgnoreCase));
															if (num != -1)
															{
																meshMaterial.BoolParameters[num] = (object)param;
															}
															else
															{
																((List<object>)meshMaterial.BoolParameters).Add(param);
															}
														}
														foreach (dynamic param2 in val.Shader.VectorParameters)
														{
															int num2 = meshMaterial.VectorParameters.FindIndex((dynamic a) => a.ParameterName.Equals(param2.ParameterName, StringComparison.OrdinalIgnoreCase));
															if (num2 != -1)
															{
																meshMaterial.VectorParameters[num2] = (object)param2;
															}
															else
															{
																((List<object>)meshMaterial.VectorParameters).Add(param2);
															}
														}
														foreach (dynamic param3 in val.Shader.TextureParameters)
														{
															int num3 = meshMaterial.TextureParameters.FindIndex((dynamic a) => a.ParameterName.Equals(param3.ParameterName, StringComparison.OrdinalIgnoreCase));
															if (num3 != -1)
															{
																meshMaterial.TextureParameters[num3] = (object)param3;
															}
															else
															{
																((List<object>)meshMaterial.TextureParameters).Add(param3);
															}
														}
														try
														{
															foreach (dynamic param4 in val.Shader.ConditionalParameters)
															{
																int num4 = meshMaterial.ConditionalParameters.FindIndex((dynamic a) => a.ConditionalAsset.Equals(param4.ConditionalAsset));
																if (num4 != -1)
																{
																	meshMaterial.ConditionalParameters[num4] = (object)param4;
																}
																else
																{
																	((List<object>)meshMaterial.ConditionalParameters).Add(param4);
																}
															}
														}
														catch
														{
														}
													}
												}
											}
										}
									}
								}
							}
						}
					}
				}
				catch (Exception ex)
				{
					App.Logger.LogWarning("[AntStateEditor] Companion MeshVariationDatabase manual mapping failed: " + ex.Message);
				}
			}
			base.Dispatcher.Invoke(delegate
			{
				dynamic val3 = BuildPerMeshSkeleton(root);
				LoadedMeshEntry loadedMeshEntry = new LoadedMeshEntry
				{
					DisplayName = entry.Filename,
					MeshSet = meshSet,
					Materials = materials,
					PerMeshSkeleton = val3,
					SourceEntry = entry,
					VariationRef = variationRef,
					BpbName = bpbName
				};
				if (replaceIndex >= 0 && replaceIndex < _loadedMeshData.Count)
				{
					_loadedMeshData[replaceIndex] = loadedMeshEntry;
					LoadedMeshes[replaceIndex].Name = entry.Filename;
					OnMeshVisibilityChanged();
				}
				else
				{
					if (clearFirst && _meshLoaded)
					{
						m_screen.ClearMeshes(clearAll: true);
						_loadedMeshData.Clear();
						LoadedMeshes.Clear();
					}
					int count = _loadedMeshData.Count;
					_loadedMeshData.Add(loadedMeshEntry);
					LoadedMeshes.Add(new LoadedMeshViewModel(this)
					{
						InternalIndex = count,
						Name = entry.Filename
					});
					m_screen.AddMesh(meshSet, materials, SharpDX.Matrix.Identity, val3);
					_meshLoaded = true;
					m_screen.RefreshPose();
				}
			});
		});
	}

	private MeshRenderSkeleton BuildPerMeshSkeleton(dynamic meshRoot)
	{
		if (m_screen.CurrentSkeleton == null)
		{
			return null;
		}
		bool flag = false;
		try
		{
			int num = 0;
			foreach (object item in meshRoot.SkinnedProceduralAnimation.Bones)
			{
				_ = item;
				num++;
			}
			flag = num > 0;
		}
		catch
		{
		}
		if (!flag)
		{
			return m_screen.CurrentSkeleton;
		}
		MeshRenderSkeleton meshRenderSkeleton = new MeshRenderSkeleton();
		foreach (MeshRenderSkeleton.Bone bone in m_screen.CurrentSkeleton.Bones)
		{
			meshRenderSkeleton.AddBone(new MeshRenderSkeleton.Bone
			{
				NameHash = bone.NameHash,
				ParentBoneId = bone.ParentBoneId,
				ModelPose = bone.ModelPose,
				LocalPose = bone.LocalPose,
				IsProcedural = bone.IsProcedural
			});
		}
		AppendSpaBones(meshRenderSkeleton, meshRoot);
		return meshRenderSkeleton;
	}

	private static void AppendSpaBones(MeshRenderSkeleton skeleton, dynamic meshRoot)
	{
		try
		{
			int num = 0;
			foreach (dynamic item in meshRoot.SkinnedProceduralAnimation.Bones)
			{
				try
				{
					int num2 = (int)item.ParentIndex;
					SharpDX.Matrix matrix = new SharpDX.Matrix(item.Pose.right.x, item.Pose.right.y, item.Pose.right.z, 0f, item.Pose.up.x, item.Pose.up.y, item.Pose.up.z, 0f, item.Pose.forward.x, item.Pose.forward.y, item.Pose.forward.z, 0f, item.Pose.trans.x, item.Pose.trans.y, item.Pose.trans.z, 1f);
					SharpDX.Matrix matrix2 = matrix;
					matrix2.Invert();
					SharpDX.Matrix modelPose = skeleton.GetBone(num2).ModelPose * matrix2;
					skeleton.AddBone(new MeshRenderSkeleton.Bone
					{
						NameHash = int.MinValue + num,
						ParentBoneId = num2,
						ModelPose = modelPose,
						LocalPose = matrix,
						IsProcedural = true
					});
					num++;
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
	}

	private async Task LoadSkinAsync(EbxAsset skinEbx)
	{
		base.Dispatcher.Invoke(delegate
		{
			if (_meshLoaded)
			{
				m_screen.ClearMeshes(clearAll: true);
			}
			_meshLoaded = false;
			_loadedMeshData.Clear();
			LoadedMeshes.Clear();
		});
		dynamic rootObject = skinEbx.RootObject;
		int loaded = 0;
		try
		{
			dynamic val = rootObject.VisualCustomizations;
			foreach (dynamic item4 in val)
			{
				try
				{
					dynamic val2 = item4.Selectables;
					foreach (dynamic item5 in val2)
					{
						try
						{
							EbxAssetEntry ebxAssetEntry = TryResolveEbxRef((object)item5.Selectable);
							if (ebxAssetEntry != null)
							{
								Tuple<EbxAssetEntry, PointerRef, string> tuple = FindMeshOnVisualAsset(App.AssetManager.GetEbx(ebxAssetEntry));
								if (tuple != null)
								{
									EbxAssetEntry item = tuple.Item1;
									PointerRef item2 = tuple.Item2;
									string item3 = tuple.Item3;
									EbxAsset ebx = App.AssetManager.GetEbx(item);
									await LoadSingleMeshAsync(ebx, item, clearFirst: false, item2, item3);
									loaded++;
								}
							}
						}
						catch (Exception ex)
						{
							App.Logger.LogWarning("[AntStateEditor][Skin] Part failed: " + ex.Message);
						}
					}
				}
				catch (Exception ex2)
				{
					App.Logger.LogWarning("[AntStateEditor][Skin] Group failed: " + ex2.Message);
				}
			}
		}
		catch (Exception ex3)
		{
			App.Logger.LogError("[AntStateEditor][Skin] Iteration failed: " + ex3.Message);
		}
		App.Logger.Log($"[AntStateEditor] Skin loaded: {loaded} mesh part(s).");
	}

	private static EbxAssetEntry TryResolveEbxRef(object refObj)
	{
		if (refObj == null)
		{
			return null;
		}
		try
		{
			try
			{
				return App.AssetManager.GetEbxEntry((Guid)((dynamic)refObj).External.FileGuid);
			}
			catch
			{
			}
			try
			{
				return App.AssetManager.GetEbxEntry((Guid)((dynamic)refObj).External.ExternalGuid);
			}
			catch
			{
			}
			try
			{
				return App.AssetManager.GetEbxEntry((Guid)((dynamic)refObj).ExternalGuid);
			}
			catch
			{
			}
		}
		catch
		{
		}
		return null;
	}

	private static Tuple<EbxAssetEntry, PointerRef, string> FindMeshOnVisualAsset(EbxAsset visualEbx)
	{
		dynamic rootObject = visualEbx.RootObject;
		string text = null;
		try
		{
			text = (string)rootObject.BlueprintBundleReference.Name;
		}
		catch
		{
		}
		if (string.IsNullOrEmpty(text))
		{
			try
			{
				text = (string)rootObject.BlueprintBundleReference.BlueprintBundleReference.Name;
			}
			catch
			{
			}
		}
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		EbxAssetEntry ebxEntry = App.AssetManager.GetEbxEntry(text);
		if (ebxEntry == null)
		{
			App.Logger.Log("[AntStateEditor][Skin] BPB not found");
			return null;
		}
		EbxAsset ebx = App.AssetManager.GetEbx(ebxEntry);
		try
		{
			foreach (dynamic @object in ebx.Objects)
			{
				if (!((@object.GetType().Name == "VisualCustomizationAsset") ? true : false))
				{
					continue;
				}
				foreach (dynamic item3 in @object.Visuals)
				{
					try
					{
						dynamic val = null;
						object obj3 = item3.OutObjectBlueprint;
						if (obj3 is PointerRef pointerRef)
						{
							if (pointerRef.Type == PointerRefType.Internal)
							{
								val = pointerRef.Internal;
							}
							else if (pointerRef.Type == PointerRefType.External)
							{
								EbxAssetEntry ebxAssetEntry = TryResolveEbxRef(obj3);
								if (ebxAssetEntry != null)
								{
									val = App.AssetManager.GetEbx(ebxAssetEntry).RootObject;
								}
							}
						}
						if (!((val != null) ? true : false))
						{
							continue;
						}
						PointerRef item = default(PointerRef);
						try
						{
							if (val.Variation != null && val.Variation is PointerRef)
							{
								item = (PointerRef)val.Variation;
							}
						}
						catch
						{
						}
						object obj5 = null;
						try
						{
							obj5 = val.MeshAsset;
						}
						catch
						{
						}
						if (obj5 != null)
						{
							EbxAssetEntry ebxAssetEntry2 = TryResolveEbxRef(obj5);
							if (ebxAssetEntry2 != null)
							{
								return Tuple.Create(ebxAssetEntry2, item, text);
							}
						}
						object obj7 = null;
						try
						{
							obj7 = val.Object;
						}
						catch
						{
						}
						if (obj7 != null)
						{
							dynamic val2 = obj7;
							if (obj7 is PointerRef { Type: PointerRefType.Internal } pointerRef2)
							{
								val2 = pointerRef2.Internal;
							}
							if (val2 != null)
							{
								EbxAssetEntry ebxAssetEntry3 = TryResolveEbxRef((object)val2.Mesh);
								if (ebxAssetEntry3 != null)
								{
									return Tuple.Create(ebxAssetEntry3, item, text);
								}
							}
						}
						EbxAssetEntry ebxAssetEntry4 = TryResolveEbxRef((object)val.Mesh);
						if (ebxAssetEntry4 == null)
						{
							continue;
						}
						return Tuple.Create(ebxAssetEntry4, item, text);
					}
					catch (Exception ex)
					{
						App.Logger.LogWarning("[AntStateEditor][Skin] Visual parsing failed: " + ex.Message);
					}
				}
			}
		}
		catch (Exception ex2)
		{
			App.Logger.LogWarning("[AntStateEditor][Skin] BPB object scan failed: " + ex2.Message);
		}
		try
		{
			foreach (Guid dependency in ebx.Dependencies)
			{
				EbxAssetEntry ebxEntry2 = App.AssetManager.GetEbxEntry(dependency);
				if (ebxEntry2 == null)
				{
					continue;
				}
				try
				{
					dynamic rootObject2 = App.AssetManager.GetEbx(ebxEntry2).RootObject;
					PointerRef item2 = default(PointerRef);
					try
					{
						if (rootObject2.Variation != null && rootObject2.Variation is PointerRef)
						{
							item2 = (PointerRef)rootObject2.Variation;
						}
					}
					catch
					{
					}
					object obj10 = null;
					try
					{
						obj10 = rootObject2.MeshAsset;
					}
					catch
					{
					}
					if (obj10 != null)
					{
						EbxAssetEntry ebxAssetEntry5 = TryResolveEbxRef(obj10);
						if (ebxAssetEntry5 != null)
						{
							return Tuple.Create(ebxAssetEntry5, item2, text);
						}
					}
					object obj12 = null;
					try
					{
						obj12 = rootObject2.Object;
					}
					catch
					{
					}
					if (obj12 != null)
					{
						dynamic val3 = obj12;
						if (obj12 is PointerRef { Type: PointerRefType.Internal } pointerRef3)
						{
							val3 = pointerRef3.Internal;
						}
						if (val3 != null)
						{
							EbxAssetEntry ebxAssetEntry6 = TryResolveEbxRef((object)val3.Mesh);
							if (ebxAssetEntry6 != null)
							{
								return Tuple.Create(ebxAssetEntry6, item2, text);
							}
						}
					}
					EbxAssetEntry ebxAssetEntry7 = TryResolveEbxRef((object)rootObject2.Mesh);
					if (ebxAssetEntry7 != null)
					{
						return Tuple.Create(ebxAssetEntry7, item2, text);
					}
				}
				catch
				{
				}
			}
		}
		catch (Exception ex3)
		{
			App.Logger.LogWarning("[AntStateEditor][Skin] Dependency scan failed: " + ex3.Message);
		}
		return null;
	}

	private static Dictionary<Guid, ulong> GetOriginalHashes(ResAssetEntry resEntry, ChunkAssetEntry chunkEntry, int bundleId)
	{
		byte[] array = null;
		if (resEntry != null)
		{
			ModifiedAssetEntry modifiedEntry = resEntry.ModifiedEntry;
			resEntry.ModifiedEntry = null;
			using (Stream stream = App.AssetManager.GetRes(resEntry))
			{
				if (stream != null)
				{
					using MemoryStream memoryStream = new MemoryStream();
					stream.CopyTo(memoryStream);
					array = memoryStream.ToArray();
				}
			}
			resEntry.ModifiedEntry = modifiedEntry;
		}
		else if (chunkEntry != null)
		{
			ModifiedAssetEntry modifiedEntry2 = chunkEntry.ModifiedEntry;
			chunkEntry.ModifiedEntry = null;
			using (Stream stream2 = App.AssetManager.GetChunk(chunkEntry))
			{
				if (stream2 != null)
				{
					using MemoryStream memoryStream2 = new MemoryStream();
					stream2.CopyTo(memoryStream2);
					array = memoryStream2.ToArray();
				}
			}
			chunkEntry.ModifiedEntry = modifiedEntry2;
		}
		if (array == null)
		{
			return null;
		}
		using NativeReader r = new NativeReader(new MemoryStream(array));
		return new Bank(r, bundleId, isHashMode: true).OriginalHashes;
	}

	private async Task LoadAsync()
	{
		_ = 1;
		try
		{
			SetLoadingState(isVisible: true, "Locating data stream...");
			dynamic rootObject = base.RootObject;
			Stream stream = null;
			int bundleId = 0;
			ResAssetEntry resAssetEntry = null;
			ChunkAssetEntry chunkAssetEntry = null;
			if (rootObject.StreamingGuid == Guid.Empty)
			{
				resAssetEntry = App.AssetManager.GetResEntry(base.AssetEntry.Name);
				if (resAssetEntry != null)
				{
					bundleId = resAssetEntry.Bundles[0];
					stream = App.AssetManager.GetRes(resAssetEntry);
				}
			}
			else
			{
				chunkAssetEntry = App.AssetManager.GetChunkEntry(rootObject.StreamingGuid);
				if (chunkAssetEntry != null)
				{
					bundleId = chunkAssetEntry.Bundles[0];
					stream = App.AssetManager.GetChunk(chunkAssetEntry);
				}
			}
			if (stream == null)
			{
				SetLoadingState(isVisible: false, "");
				logger.LogError("Failed to locate AntState stream.");
				return;
			}
			SetLoadingState(isVisible: true, $"Parsing {stream.Length} bytes...");
			byte[] array;
			using (MemoryStream memoryStream = new MemoryStream())
			{
				stream.CopyTo(memoryStream);
				array = memoryStream.ToArray();
			}
			_bankBytes = array;
			_bankResEntry = resAssetEntry;
			_bankChunkEntry = chunkAssetEntry;
			_bankIsBigEndian = DetectBigEndian(array);
			Dictionary<Guid, ulong> originalHashes = null;
			await Task.Run(delegate
			{
				ResAssetEntry bankResEntry = _bankResEntry;
				bool flag = (bankResEntry != null && bankResEntry.HasModifiedData) || (_bankChunkEntry?.HasModifiedData ?? false);
				if (flag)
				{
					SetLoadingState(isVisible: true, "Comparing against original bank...");
					originalHashes = GetOriginalHashes(_bankResEntry, _bankChunkEntry, bundleId);
					GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
					GC.WaitForPendingFinalizers();
				}
				SetLoadingState(isVisible: true, flag ? "Parsing modified bank..." : "Parsing bank data...");
				using NativeReader r = new NativeReader(new MemoryStream(_bankBytes));
				_bank = new Bank(r, bundleId);
			});
			SetLoadingState(isVisible: true, "Building asset hierarchy...");
			_masterGroups = await Task.Run(delegate
			{
				Dictionary<Guid, AntAsset> dictionary = BuildAnimToControllerMap();
				Dictionary<string, List<AntAssetViewModel>> dictionary2 = new Dictionary<string, List<AntAssetViewModel>>();
				foreach (KeyValuePair<string, Guid> dataName in _bank.DataNames)
				{
					AntAsset antAsset = AntRefTable.Get(dataName.Value);
					string key = ((antAsset != null) ? antAsset.AssetType : "Unknown");
					if (!dictionary2.TryGetValue(key, out var value))
					{
						List<AntAssetViewModel> list = (dictionary2[key] = new List<AntAssetViewModel>());
						value = list;
					}
					string name = dataName.Key;
					if (antAsset is AnimationAsset && dictionary.TryGetValue(dataName.Value, out var value2))
					{
						name = value2.Name + " (" + dataName.Key + ")";
					}
					AntAssetViewModel antAssetViewModel = new AntAssetViewModel
					{
						ParentEditor = this,
						Name = name,
						Id = dataName.Value,
						AssetInstance = antAsset
					};
					if (originalHashes != null && antAsset != null)
					{
						if (originalHashes.TryGetValue(antAsset.ID, out var value3))
						{
							if (Bank.ComputeAssetHash(antAsset) != value3)
							{
								antAssetViewModel.IsModified = true;
								antAssetViewModel.IsUnsaved = false;
							}
						}
						else
						{
							antAssetViewModel.IsNew = true;
							antAssetViewModel.IsModified = true;
							antAssetViewModel.IsUnsaved = false;
						}
					}
					value.Add(antAssetViewModel);
				}
				return dictionary2.OrderBy(delegate(KeyValuePair<string, List<AntAssetViewModel>> kvp)
				{
					KeyValuePair<string, List<AntAssetViewModel>> keyValuePair = kvp;
					return keyValuePair.Key;
				}).Select(delegate(KeyValuePair<string, List<AntAssetViewModel>> kvp)
				{
					AntTypeGroup antTypeGroup = new AntTypeGroup();
					KeyValuePair<string, List<AntAssetViewModel>> keyValuePair = kvp;
					antTypeGroup.TypeName = keyValuePair.Key;
					keyValuePair = kvp;
					antTypeGroup.Assets = new ObservableCollection<AntAssetViewModel>(keyValuePair.Value.OrderBy((AntAssetViewModel a) => a.Name));
					return antTypeGroup;
				}).ToList();
			});
			base.Dispatcher.Invoke(delegate
			{
				ApplySearchFilter("");
			});
			SetLoadingState(isVisible: false, "");
		}
		catch (Exception ex)
		{
			SetLoadingState(isVisible: false, "");
			logger.LogError("[AntStateEditor] " + ex.Message);
		}
	}

	private void SetLoadingState(bool isVisible, string text)
	{
		base.Dispatcher.Invoke(delegate
		{
			if (m_loadingOverlay != null)
			{
				m_loadingOverlay.Visibility = ((!isVisible) ? Visibility.Collapsed : Visibility.Visible);
			}
			if (m_loadingText != null)
			{
				m_loadingText.Text = text;
			}
		});
	}

	private async void ApplySearchFilter(string searchText)
	{
		_searchCancellationTokenSource?.Cancel();
		_searchCancellationTokenSource?.Dispose();
		_searchCancellationTokenSource = new CancellationTokenSource();
		CancellationToken cancellationToken = _searchCancellationTokenSource.Token;
		searchText = searchText?.Trim() ?? "";
		bool showModified = m_filterMenu?.ShowModifiedOnly ?? false;
		bool showAnimsOnly = m_filterMenu?.ShowAnimationsOnly ?? false;
		bool sortAsc = m_filterMenu?.SortAscending ?? true;
		List<AntTypeGroup> masterGroupsSnapshot = _masterGroups.ToList();
		try
		{
			List<AntTypeGroup> list = await Task.Run(() => ProcessFilterOnBackgroundThread(masterGroupsSnapshot, searchText, showModified, showAnimsOnly, sortAsc, cancellationToken), cancellationToken);
			if (cancellationToken.IsCancellationRequested)
			{
				return;
			}
			_filteredGroups.Clear();
			foreach (AntTypeGroup item in list)
			{
				_filteredGroups.Add(item);
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex2)
		{
			App.Logger.LogError("[AntStateEditor] Search filter error: " + ex2.Message);
		}
	}

	private List<AntTypeGroup> ProcessFilterOnBackgroundThread(List<AntTypeGroup> masterGroups, string searchText, bool showModified, bool showAnimsOnly, bool sortAsc, CancellationToken token)
	{
		string typeQuery = null;
		string textQuery = searchText;
		if (searchText.Contains("@"))
		{
			string[] source = searchText.Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			string typeToken = source.FirstOrDefault((string p) => p.StartsWith("@"));
			if (typeToken != null)
			{
				typeQuery = typeToken.Substring(1);
				textQuery = string.Join(" ", source.Where((string p) => p != typeToken));
			}
		}
		IEnumerable<AntTypeGroup> enumerable = (sortAsc ? masterGroups.OrderBy((AntTypeGroup g) => g.TypeName) : masterGroups.OrderByDescending((AntTypeGroup g) => g.TypeName));
		if (!string.IsNullOrEmpty(typeQuery))
		{
			enumerable = enumerable.Where((AntTypeGroup g) => g.TypeName.IndexOf(typeQuery, StringComparison.OrdinalIgnoreCase) >= 0);
		}
		List<AntTypeGroup> list = new List<AntTypeGroup>();
		foreach (AntTypeGroup item in enumerable)
		{
			if (token.IsCancellationRequested)
			{
				return new List<AntTypeGroup>();
			}
			IEnumerable<AntAssetViewModel> source2 = item.Assets.Where(delegate(AntAssetViewModel vm)
			{
				if (showAnimsOnly && !(vm.AssetInstance is AnimationAsset))
				{
					return false;
				}
				if (showModified && !vm.IsModified && !vm.IsNew)
				{
					return false;
				}
				if (!string.IsNullOrEmpty(textQuery))
				{
					bool num = vm.Name.IndexOf(textQuery, StringComparison.OrdinalIgnoreCase) >= 0;
					bool flag = vm.Id.ToString().IndexOf(textQuery, StringComparison.OrdinalIgnoreCase) >= 0;
					if (!num && !flag)
					{
						return false;
					}
				}
				return true;
			});
			List<AntAssetViewModel> list2 = (sortAsc ? source2.OrderBy((AntAssetViewModel a) => a.Name).ToList() : source2.OrderByDescending((AntAssetViewModel a) => a.Name).ToList());
			if (list2.Count > 0)
			{
				list.Add(new AntTypeGroup
				{
					TypeName = item.TypeName,
					Assets = new ObservableCollection<AntAssetViewModel>(list2)
				});
			}
		}
		return list;
	}

	private void ToggleBulkMode(bool isBulk)
	{
		if (!isBulk)
		{
			_shiftAnchor = null;
		}
		foreach (AntTypeGroup masterGroup in _masterGroups)
		{
			foreach (AntAssetViewModel asset in masterGroup.Assets)
			{
				asset.SetBulkMode(isBulk);
			}
		}
	}

	private void OnTreeViewPreviewMouseDown(object sender, MouseButtonEventArgs e)
	{
		if (!_isBulkMode)
		{
			return;
		}
		AntAssetViewModel viewModelFromSource = GetViewModelFromSource(e.OriginalSource as DependencyObject);
		if (viewModelFromSource != null)
		{
			if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift && _shiftAnchor != null)
			{
				SelectRange(_shiftAnchor, viewModelFromSource);
				e.Handled = true;
			}
			else
			{
				_shiftAnchor = viewModelFromSource;
			}
		}
	}

	private AntAssetViewModel GetViewModelFromSource(DependencyObject source)
	{
		while (source != null && !(source is TreeViewItem))
		{
			source = VisualTreeHelper.GetParent(source);
		}
		return (source as TreeViewItem)?.DataContext as AntAssetViewModel;
	}

	private void SelectRange(AntAssetViewModel anchor, AntAssetViewModel target)
	{
		List<AntAssetViewModel> list = _filteredGroups.SelectMany((AntTypeGroup g) => g.Assets).ToList();
		int num = list.IndexOf(anchor);
		int num2 = list.IndexOf(target);
		if (num >= 0 && num2 >= 0)
		{
			int num3 = Math.Min(num, num2);
			int num4 = Math.Max(num, num2);
			for (int num5 = num3; num5 <= num4; num5++)
			{
				list[num5].IsSelectedForExport = true;
			}
		}
	}

	private void ExportSelected()
	{
		List<AntAssetViewModel> assetsToExport = new List<AntAssetViewModel>();
		if (_isBulkMode)
		{
			assetsToExport = (from a in _masterGroups.SelectMany((AntTypeGroup g) => g.Assets)
				where a.IsSelectedForExport && a.AssetInstance is AnimationAsset
				select a).ToList();
		}
		else if (_selectedAsset != null && _selectedAsset.AssetInstance is AnimationAsset)
		{
			assetsToExport.Add(_selectedAsset);
		}
		if (assetsToExport.Count == 0)
		{
			return;
		}
		AnimationOptions opt = new AnimationOptions();
		opt.Load();
		if (string.IsNullOrEmpty(opt.ExportSkeletonAsset))
		{
			FrostyMessageBox.Show("Please set an Export Skeleton in Options first.", "Missing Skeleton");
			return;
		}
		FrostySaveFileDialog frostySaveFileDialog = new FrostySaveFileDialog("Select Export Directory", "*.cast (Cast)|*.cast|*.seanim (SEAnim)|*.seanim", "Cast", "FolderSelection");
		if (!frostySaveFileDialog.ShowDialog())
		{
			return;
		}
		string exportDirectory = Path.GetDirectoryName(frostySaveFileDialog.FileName);
		string extension = Path.GetExtension(frostySaveFileDialog.FileName).ToLower();
		FrostyTaskWindow.Show($"Exporting {assetsToExport.Count} Animations", "", delegate(FrostyTaskWindow task)
		{
			InternalSkeleton skeleton = null;
			Application.Current.Dispatcher.Invoke(delegate
			{
				EbxAssetEntry ebxEntry = App.AssetManager.GetEbxEntry(opt.ExportSkeletonAsset);
				EbxAsset ebx = App.AssetManager.GetEbx(ebxEntry);
				skeleton = SkeletonAssetExport.ConvertToInternal(ebx.RootObject);
			});
			Dictionary<Guid, AntAsset> dictionary = BuildAnimToControllerMap();
			int num = 0;
			foreach (AntAssetViewModel item in assetsToExport)
			{
				AnimationAsset anim = item.AssetInstance as AnimationAsset;
				dictionary.TryGetValue(item.Id, out var value);
				string finalFileName = ((value != null) ? value.Name : item.Name);
				task.Update("Exporting " + finalFileName + "...", (double)num / (double)assetsToExport.Count * 100.0);
				Application.Current.Dispatcher.Invoke(delegate
				{
					try
					{
						anim.Name = finalFileName;
						anim.Channels = anim.GetChannels(anim.ChannelToDofAsset);
						InternalAnimation internalAnimation = anim.ConvertToInternal();
						if (internalAnimation != null)
						{
							if (extension == ".cast")
							{
								new AnimationExporterCAST().Export(internalAnimation, skeleton, exportDirectory);
							}
							else
							{
								new AnimationExporterSEANIM().Export(internalAnimation, skeleton, exportDirectory);
							}
						}
					}
					catch (Exception ex)
					{
						App.Logger.LogError("[AntStateEditor] Failed " + finalFileName + ": " + ex.Message);
					}
				});
				num++;
			}
			App.Logger.Log($"[AntStateEditor] Exported {assetsToExport.Count} assets.");
		});
	}

	public override List<ToolbarItem> RegisterToolbarItems()
	{
		return new List<ToolbarItem>
		{
			new ToolbarItem("Refresh", "Reload data", null, new RelayCommand(delegate
			{
				LoadAsync();
			}))
		};
	}

	internal void OnMeshVisibilityChanged()
	{
		base.Dispatcher.Invoke(delegate
		{
			m_screen.ClearMeshes(clearAll: true);
			for (int i = 0; i < _loadedMeshData.Count; i++)
			{
				if (i < LoadedMeshes.Count && LoadedMeshes[i].IsVisible)
				{
					LoadedMeshEntry loadedMeshEntry = _loadedMeshData[i];
					m_screen.AddMesh(loadedMeshEntry.MeshSet, loadedMeshEntry.Materials, SharpDX.Matrix.Identity, loadedMeshEntry.PerMeshSkeleton);
				}
			}
			_meshLoaded = _loadedMeshData.Count > 0;
			m_screen.RefreshPose();
		});
	}

	internal async void ReplaceMeshFromExplorer(int index)
	{
		_ = 1;
		try
		{
			if (!(((dynamic)App.EditorWindow)?.DataExplorer?.SelectedAsset is EbxAssetEntry entry))
			{
				App.Logger.LogWarning("[AntStateEditor] No asset selected in data explorer.");
				return;
			}
			EbxAsset ebx = App.AssetManager.GetEbx(entry);
			string name = ebx.RootObject.GetType().Name;
			if (name == "VisualCustomizationSetAsset")
			{
				App.Logger.LogWarning("[AntStateEditor] Use 'Load Mesh' to swap a full skin - the arrow replaces a single slot.");
			}
			else if (name == "VisualCustomizationAsset")
			{
				await LoadVisualAssetAsync(ebx, clearFirst: false, index);
			}
			else
			{
				await LoadSingleMeshAsync(ebx, entry, clearFirst: false, default(PointerRef), null, index);
			}
		}
		catch (Exception ex)
		{
			App.Logger.LogError("[AntStateEditor] Replace mesh failed: " + ex.Message);
		}
	}

	internal async void AddMeshFromExplorer()
	{
		_ = 1;
		try
		{
			if (!(((dynamic)App.EditorWindow)?.DataExplorer?.SelectedAsset is EbxAssetEntry entry))
			{
				App.Logger.LogWarning("[AntStateEditor] No asset selected in data explorer.");
				return;
			}
			EbxAsset ebx = App.AssetManager.GetEbx(entry);
			string name = ebx.RootObject.GetType().Name;
			if (name == "VisualCustomizationSetAsset")
			{
				App.Logger.LogWarning("[AntStateEditor] Use 'Load Mesh' to load a full skin - add appends a single mesh.");
			}
			else if (name == "VisualCustomizationAsset")
			{
				await LoadVisualAssetAsync(ebx, clearFirst: false, -1);
			}
			else
			{
				await LoadSingleMeshAsync(ebx, entry, clearFirst: false);
			}
		}
		catch (Exception ex)
		{
			App.Logger.LogError("[AntStateEditor] Add mesh failed: " + ex.Message);
		}
	}

	private async Task LoadVisualAssetAsync(EbxAsset visualEbx, bool clearFirst, int replaceIndex)
	{
		Tuple<EbxAssetEntry, PointerRef, string> tuple = FindMeshOnVisualAsset(visualEbx);
		if (tuple == null)
		{
			App.Logger.LogError("[AntStateEditor] Could not resolve mesh from VisualCustomizationAsset.");
			return;
		}
		EbxAssetEntry item = tuple.Item1;
		PointerRef item2 = tuple.Item2;
		string item3 = tuple.Item3;
		EbxAsset ebx = App.AssetManager.GetEbx(item);
		await LoadSingleMeshAsync(ebx, item, clearFirst, item2, item3, replaceIndex);
	}

	internal void RemoveMeshAt(int index)
	{
		if (index >= 0 && index < _loadedMeshData.Count)
		{
			_loadedMeshData.RemoveAt(index);
			LoadedMeshes.RemoveAt(index);
			for (int i = index; i < LoadedMeshes.Count; i++)
			{
				LoadedMeshes[i].InternalIndex = i;
			}
			OnMeshVisibilityChanged();
		}
	}
}
