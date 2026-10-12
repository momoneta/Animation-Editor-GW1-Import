using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using Frosty.Controls;
using FrostySdk.Managers.Entries;

namespace AssetBankPlugin;

public class AssetPickerDialog : FrostyWindow, IComponentConnector
{
	private List<EbxAssetEntry> _flatAll;

	private ObservableCollection<EbxAssetEntry> _flatView;

	internal Action<string> OnTreeSearch;

	internal Border m_typeBadge;

	internal TextBlock m_typeBadgeText;

	internal TextBlock m_selectionCountText;

	internal FrostyWatermarkTextBox m_searchBox;

	internal Border m_listPanel;

	internal ListBox m_itemList;

	internal Border m_treePanel;

	internal TreeView m_itemTree;

	internal TextBlock m_footerHint;

	internal Button m_cancelBtn;

	internal Button m_confirmBtn;

	internal TextBlock m_confirmBtnText;

	private bool _contentLoaded;

	public EbxAssetEntry SelectedEntry { get; private set; }

	public IEnumerable<EbxAssetEntry> SelectedEntries => BankPickerDialog.PeekSelection();

	public AssetPickerDialog()
	{
		InitializeComponent();
		if (m_cancelBtn != null)
		{
			m_cancelBtn.Click += delegate
			{
				base.DialogResult = false;
				Close();
			};
		}
		if (m_confirmBtn != null)
		{
			m_confirmBtn.Click += delegate
			{
				Confirm();
			};
		}
	}

	protected void ConfigureFlatList(string assetType, string watermark, IEnumerable<EbxAssetEntry> entries, string badgeColor = "#007ACC", string confirmLabel = "Select", string footerHint = "Double-click or press Select to confirm")
	{
		if (m_typeBadgeText != null)
		{
			m_typeBadgeText.Text = assetType;
		}
		if (m_searchBox != null)
		{
			m_searchBox.WatermarkText = watermark;
		}
		if (m_confirmBtnText != null)
		{
			m_confirmBtnText.Text = confirmLabel;
		}
		if (m_footerHint != null)
		{
			m_footerHint.Text = footerHint;
		}
		if (FindName("m_typeBadge") is Border border)
		{
			try
			{
				border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(badgeColor));
			}
			catch
			{
			}
		}
		m_listPanel.Visibility = Visibility.Visible;
		m_treePanel.Visibility = Visibility.Collapsed;
		_flatAll = entries.ToList();
		_flatView = new ObservableCollection<EbxAssetEntry>(_flatAll);
		if (m_itemList != null)
		{
			m_itemList.ItemsSource = _flatView;
			m_itemList.MouseDoubleClick += delegate
			{
				Confirm();
			};
		}
		if (m_searchBox != null)
		{
			m_searchBox.TextChanged += delegate
			{
				FilterFlatList(m_searchBox.Text);
			};
		}
		UpdateSelectionCount();
	}

	protected void ConfigureFolderTree(string assetType, string watermark, ObservableCollection<BankNode> rootNodes, string badgeColor = "#1C3852", string confirmLabel = "Load Selected", string footerHint = "Check assets then press Load Selected")
	{
		if (m_typeBadgeText != null)
		{
			m_typeBadgeText.Text = assetType;
		}
		if (m_searchBox != null)
		{
			m_searchBox.WatermarkText = watermark;
		}
		if (m_confirmBtnText != null)
		{
			m_confirmBtnText.Text = confirmLabel;
		}
		if (m_footerHint != null)
		{
			m_footerHint.Text = footerHint;
		}
		if (FindName("m_typeBadge") is Border border)
		{
			try
			{
				border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(badgeColor));
			}
			catch
			{
			}
		}
		m_listPanel.Visibility = Visibility.Collapsed;
		m_treePanel.Visibility = Visibility.Visible;
		if (m_itemTree != null)
		{
			m_itemTree.ItemsSource = rootNodes;
		}
		if (m_searchBox != null)
		{
			m_searchBox.TextChanged += delegate
			{
				OnTreeSearch?.Invoke(m_searchBox.Text);
			};
		}
		UpdateSelectionCount();
	}

	internal void NotifySelectionChanged()
	{
		UpdateSelectionCount();
	}

	private void FilterFlatList(string query)
	{
		if (_flatAll == null || _flatView == null)
		{
			return;
		}
		_flatView.Clear();
		foreach (EbxAssetEntry item in _flatAll)
		{
			if (string.IsNullOrEmpty(query) || item.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				_flatView.Add(item);
			}
		}
		UpdateSelectionCount();
	}

	private void UpdateSelectionCount()
	{
		if (m_selectionCountText != null)
		{
			Border listPanel = m_listPanel;
			if (listPanel != null && listPanel.Visibility == Visibility.Visible && _flatView != null)
			{
				m_selectionCountText.Text = string.Format("{0} asset{1}", _flatView.Count, (_flatView.Count == 1) ? "" : "s");
				return;
			}
			int num = BankPickerDialog.PeekSelection().Count();
			m_selectionCountText.Text = ((num > 0) ? $"{num} selected" : "");
		}
	}

	private void Confirm()
	{
		Border listPanel = m_listPanel;
		if (listPanel != null && listPanel.Visibility == Visibility.Visible)
		{
			object obj = m_itemList?.SelectedItem as EbxAssetEntry;
			if (obj == null)
			{
				ListBox itemList = m_itemList;
				obj = ((itemList != null && itemList.Items.Count > 0) ? (m_itemList.Items[0] as EbxAssetEntry) : null);
			}
			SelectedEntry = (EbxAssetEntry)obj;
			if (SelectedEntry == null)
			{
				return;
			}
		}
		base.DialogResult = true;
		Close();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "9.0.14.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/AssetBankPlugin;component/windows/assetpickerdialog.xaml", UriKind.Relative);
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
			m_typeBadge = (Border)target;
			break;
		case 2:
			m_typeBadgeText = (TextBlock)target;
			break;
		case 3:
			m_selectionCountText = (TextBlock)target;
			break;
		case 4:
			m_searchBox = (FrostyWatermarkTextBox)target;
			break;
		case 5:
			m_listPanel = (Border)target;
			break;
		case 6:
			m_itemList = (ListBox)target;
			break;
		case 7:
			m_treePanel = (Border)target;
			break;
		case 8:
			m_itemTree = (TreeView)target;
			break;
		case 9:
			m_footerHint = (TextBlock)target;
			break;
		case 10:
			m_cancelBtn = (Button)target;
			break;
		case 11:
			m_confirmBtn = (Button)target;
			break;
		case 12:
			m_confirmBtnText = (TextBlock)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
