using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Frosty.Controls;
using Frosty.Core;
using FrostySdk.Managers.Entries;

namespace AssetBankPlugin.Windows;

public class RigExportSettingsWindow : FrostyWindow, IComponentConnector
{
	internal TextBlock m_meshNameText;

	internal Image m_meshIcon;

	internal TextBlock m_meshPathText;

	internal ComboBox m_fbxCombo;

	internal ComboBox m_scaleCombo;

	internal ToggleButton m_singleLodCheck;

	private bool _contentLoaded;

	public EbxAssetEntry SelectedMesh { get; private set; }

	public string SelectedFbxVersion { get; private set; }

	public string SelectedScale { get; private set; }

	public bool ExportSingleLod { get; private set; }

	public RigExportSettingsWindow(EbxAssetEntry initialMesh)
	{
		InitializeComponent();
		SelectedMesh = initialMesh;
		SelectedFbxVersion = Config.Get("AntRigExportFbxVersion", "2017", ConfigScope.Game);
		SelectedScale = Config.Get("AntRigExportScale", "Centimeters", ConfigScope.Game);
		ExportSingleLod = Config.Get("AntRigExportSingleLod", defaultValue: false, ConfigScope.Game);
		m_singleLodCheck.IsChecked = ExportSingleLod;
		string[] array = new string[5] { "2012", "2013", "2014", "2016", "2017" };
		foreach (string newItem in array)
		{
			m_fbxCombo.Items.Add(newItem);
		}
		m_fbxCombo.SelectedItem = SelectedFbxVersion;
		array = new string[5] { "Millimeters", "Centimeters", "Decimeters", "Meters", "Kilometers" };
		foreach (string newItem2 in array)
		{
			m_scaleCombo.Items.Add(newItem2);
		}
		m_scaleCombo.SelectedItem = SelectedScale;
		UpdateMeshUI();
	}

	private void UpdateMeshUI()
	{
		m_meshNameText.Text = ((SelectedMesh != null) ? SelectedMesh.Filename : "No Mesh Selected");
		m_meshPathText.Text = ((SelectedMesh != null) ? SelectedMesh.Name : "Click Browse to select a mesh...");
		m_meshIcon.Source = GetAssetIcon(SelectedMesh);
	}

	private void BrowseMesh_Click(object sender, RoutedEventArgs e)
	{
		MeshPickerDialog meshPickerDialog = new MeshPickerDialog
		{
			Owner = this
		};
		if (meshPickerDialog.ShowDialog() == true && meshPickerDialog.SelectedEntry != null)
		{
			SelectedMesh = meshPickerDialog.SelectedEntry;
			UpdateMeshUI();
		}
	}

	private void FbxCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (m_fbxCombo.SelectedItem != null)
		{
			SelectedFbxVersion = m_fbxCombo.SelectedItem.ToString();
		}
	}

	private void ScaleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (m_scaleCombo.SelectedItem != null)
		{
			SelectedScale = m_scaleCombo.SelectedItem.ToString();
		}
	}

	private void Export_Click(object sender, RoutedEventArgs e)
	{
		if (SelectedMesh == null)
		{
			FrostyMessageBox.Show("Please select a target mesh asset before exporting.", "Missing Mesh");
			return;
		}
		ExportSingleLod = m_singleLodCheck.IsChecked == true;
		Config.Add("AntRigExportFbxVersion", SelectedFbxVersion, ConfigScope.Game);
		Config.Add("AntRigExportScale", SelectedScale, ConfigScope.Game);
		Config.Add("AntRigExportSingleLod", ExportSingleLod, ConfigScope.Game);
		Config.Save();
		base.DialogResult = true;
		Close();
	}

	private void Cancel_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
		Close();
	}

	private ImageSource GetAssetIcon(EbxAssetEntry entry)
	{
		if (entry == null)
		{
			return null;
		}
		string uriString = ((entry.Type == "SkeletonAsset") ? "pack://application:,,,/FrostyCore;component/Images/Assets/SkeletonFileType.png" : ((!(entry.Type == "RigidMeshAsset")) ? "pack://application:,,,/AssetBankPlugin;component/Images/SkinnedMeshFileType.png" : "pack://application:,,,/AssetBankPlugin;component/Images/RigidMeshFileType.png"));
		try
		{
			return new BitmapImage(new Uri(uriString, UriKind.Absolute));
		}
		catch
		{
			return null;
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "9.0.14.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/AssetBankPlugin;component/windows/rigexportsettingswindow.xaml", UriKind.Relative);
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
			m_meshNameText = (TextBlock)target;
			break;
		case 2:
			m_meshIcon = (Image)target;
			break;
		case 3:
			m_meshPathText = (TextBlock)target;
			break;
		case 4:
			((Button)target).Click += BrowseMesh_Click;
			break;
		case 5:
			m_fbxCombo = (ComboBox)target;
			m_fbxCombo.SelectionChanged += FbxCombo_SelectionChanged;
			break;
		case 6:
			m_scaleCombo = (ComboBox)target;
			m_scaleCombo.SelectionChanged += ScaleCombo_SelectionChanged;
			break;
		case 7:
			m_singleLodCheck = (ToggleButton)target;
			break;
		case 8:
			((Button)target).Click += Cancel_Click;
			break;
		case 9:
			((Button)target).Click += Export_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
