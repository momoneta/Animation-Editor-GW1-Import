using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;

namespace AssetBankPlugin.Windows;

public class AssetFilterMenu : UserControl, IComponentConnector
{
	internal Button PART_BtnReset;

	internal ToggleButton PART_ChkModifiedOnly;

	internal ToggleButton PART_ChkAnimationsOnly;

	internal ToggleButton PART_TogSortOrder;

	private bool _contentLoaded;

	public bool ShowModifiedOnly => PART_ChkModifiedOnly.IsChecked == true;

	public bool ShowAnimationsOnly => PART_ChkAnimationsOnly.IsChecked == true;

	public bool SortAscending => PART_TogSortOrder.IsChecked == false;

	public event EventHandler FilterChanged;

	public AssetFilterMenu()
	{
		InitializeComponent();
		PART_ChkModifiedOnly.Click += OnFilterOptionChanged;
		PART_ChkAnimationsOnly.Click += OnFilterOptionChanged;
		PART_TogSortOrder.Click += OnFilterOptionChanged;
		PART_BtnReset.Click += delegate
		{
			PART_ChkModifiedOnly.IsChecked = false;
			PART_ChkAnimationsOnly.IsChecked = false;
			PART_TogSortOrder.IsChecked = false;
			this.FilterChanged?.Invoke(this, EventArgs.Empty);
		};
	}

	private void OnFilterOptionChanged(object sender, RoutedEventArgs e)
	{
		this.FilterChanged?.Invoke(this, EventArgs.Empty);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "9.0.14.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/AssetBankPlugin;component/windows/assetfiltermenu.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "9.0.14.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			PART_BtnReset = (Button)target;
			break;
		case 2:
			PART_ChkModifiedOnly = (ToggleButton)target;
			break;
		case 3:
			PART_ChkAnimationsOnly = (ToggleButton)target;
			break;
		case 4:
			PART_TogSortOrder = (ToggleButton)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
