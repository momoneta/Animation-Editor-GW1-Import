using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using AssetBankPlugin.Export;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Windows;
using FrostySdk.Interfaces;
using FrostySdk.Resources;

namespace AssetBankPlugin;

public class AssetBankEditor : FrostyAssetEditor
{
	public static readonly DependencyProperty GridVisibleProperty;

	private TextBlock m_textureFormatText;

	private Texture m_textureAsset;

	private TextBox m_debugTextBox;

	private ComboBox m_mipsComboBox;

	private ComboBox m_sliceComboBox;

	private Border m_sliceToolBarItem;

	private bool m_textureIsSrgb;

	public bool GridVisible
	{
		get
		{
			return (bool)GetValue(GridVisibleProperty);
		}
		set
		{
			SetValue(GridVisibleProperty, value);
		}
	}

	static AssetBankEditor()
	{
		GridVisibleProperty = DependencyProperty.Register("GridVisible", typeof(bool), typeof(AssetBankEditor), new FrameworkPropertyMetadata(true));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(AssetBankEditor), new FrameworkPropertyMetadata(typeof(AssetBankEditor)));
	}

	public AssetBankEditor(ILogger inLogger)
		: base(inLogger)
	{
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		UpdateControls();
	}

	public override List<ToolbarItem> RegisterToolbarItems()
	{
		List<ToolbarItem> list = base.RegisterToolbarItems();
		list.Add(new ToolbarItem("Export", "Export Texture", "Images/Export.png", new RelayCommand(delegate
		{
			ExportButton_Click(this, new RoutedEventArgs());
		})));
		list.Add(new ToolbarItem("Import", "Import Texture", "Images/Import.png", new RelayCommand(delegate
		{
			ImportButton_Click(this, new RoutedEventArgs());
		})));
		return list;
	}

	private void ImportButton_Click(object sender, RoutedEventArgs e)
	{
		FrostyOpenFileDialog frostyOpenFileDialog = new FrostyOpenFileDialog("Import Texture", "PNG (*.png)|*.png|TGA (*.tga)|*.tga|HDR (*.hdr)|*.hdr|DDS (*.dds)|*.dds", "Texture");
		if (m_textureAsset.Type != TextureType.TT_2d)
		{
			frostyOpenFileDialog.Multiselect = true;
			frostyOpenFileDialog.Title = "Import Textures";
		}
		if (frostyOpenFileDialog.ShowDialog())
		{
			_ = base.AssetEntry;
			_ = (ulong)((dynamic)base.RootObject).Resource;
			string text = "";
			FrostyTaskWindow.Show("Importing Texture", "", delegate
			{
			});
			string text2 = "Texture " + frostyOpenFileDialog.FileName + " failed to import: " + text;
			if (0 == 0)
			{
				UpdateControls();
				InvokeOnAssetModified();
				text2 = "Texture " + frostyOpenFileDialog.FileName + " successfully imported";
			}
			logger.Log(text2);
		}
	}

	private void UpdateControls()
	{
		float num = (int)m_textureAsset.Width;
		float num2 = (int)m_textureAsset.Height;
		if (num > 2048f)
		{
			num = 2048f;
			num2 *= num / (float)(int)m_textureAsset.Width;
		}
		if (num2 > 2048f)
		{
			num2 = 2048f;
			num *= num2 / (float)(int)m_textureAsset.Height;
		}
		string text = m_textureAsset.PixelFormat;
		if (text.StartsWith("BC") && m_textureAsset.Flags.HasFlag(TextureFlags.SrgbGamma))
		{
			text = text.Replace("UNORM", "SRGB");
		}
		m_textureFormatText.Text = text;
		ushort num3 = m_textureAsset.Width;
		ushort num4 = m_textureAsset.Height;
		m_mipsComboBox.Items.Clear();
		for (int i = 0; i < m_textureAsset.MipCount; i++)
		{
			m_mipsComboBox.Items.Add($"{num3}x{num4}");
			num3 >>= 1;
			num4 >>= 1;
		}
		m_mipsComboBox.SelectedIndex = 0;
		if (m_textureAsset.Depth <= 1)
		{
			return;
		}
		m_sliceComboBox.ItemsSource = null;
		if (m_textureAsset.Type == TextureType.TT_Cube)
		{
			string[] itemsSource = new string[6] { "X+", "X-", "Y+", "Y-", "Z+", "Z-" };
			m_sliceComboBox.ItemsSource = itemsSource;
		}
		else
		{
			string[] array = new string[m_textureAsset.Depth];
			for (int j = 0; j < m_textureAsset.Depth; j++)
			{
				array[j] = j.ToString();
			}
			m_sliceComboBox.ItemsSource = array;
		}
		m_sliceComboBox.SelectedIndex = 0;
	}

	private void ExportButton_Click(object sender, RoutedEventArgs e)
	{
		FrostySaveFileDialog frostySaveFileDialog = new FrostySaveFileDialog("Export Texture", "PNG (*.png)|*.png|TGA (*.tga)|*.tga|HDR (*.hdr)|*.hdr|DDS (*.dds)|*.dds", "Texture", base.AssetEntry.Filename, overwritePrompt: false);
		if (false)
		{
			FrostyTaskWindow.Show("Exporting Animation", base.AssetEntry.Filename, delegate
			{
				(new string[1])[0] = "*.seanim";
				new AnimationExporterSEANIM();
			});
			logger.Log("Texture successfully exported to " + frostySaveFileDialog.FileName);
		}
	}
}
