using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Frosty.Controls;

namespace AssetBankPlugin;

internal class EditAnimAssetsWindow : FrostyWindow
{
	private readonly List<AntStateAssetEditor.AnimAssetEntry> _entries;

	private readonly List<TextBox> _boxes = new List<TextBox>();

	public EditAnimAssetsWindow(List<AntStateAssetEditor.AnimAssetEntry> entries)
	{
		_entries = entries;
		base.Title = "  Edit Anim Assets";
		base.Width = 520.0;
		base.MinHeight = 150.0;
		base.SizeToContent = SizeToContent.Height;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(10.0)
		};
		foreach (AntStateAssetEditor.AnimAssetEntry entry in entries)
		{
			Grid grid = new Grid
			{
				Margin = new Thickness(0.0, 4.0, 0.0, 4.0)
			};
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(140.0)
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			TextBlock element = new TextBlock
			{
				Text = $"Track {entry.TrackIndex}  Anim {entry.AnimIndex}:",
				Foreground = new SolidColorBrush(Color.FromRgb(187, 187, 187)),
				VerticalAlignment = VerticalAlignment.Center,
				FontSize = 11.0
			};
			Grid.SetColumn(element, 0);
			grid.Children.Add(element);
			TextBox textBox = new TextBox
			{
				Text = entry.CurrentGuid,
				Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
				Foreground = new SolidColorBrush(Color.FromRgb(0, 206, 155)),
				FontSize = 11.0,
				BorderThickness = new Thickness(0.0, 0.0, 0.0, 1.0),
				BorderBrush = new SolidColorBrush(Color.FromRgb(68, 68, 68)),
				CaretBrush = Brushes.White,
				Padding = new Thickness(3.0, 2.0, 3.0, 2.0)
			};
			Grid.SetColumn(textBox, 1);
			grid.Children.Add(textBox);
			_boxes.Add(textBox);
			stackPanel.Children.Add(grid);
		}
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
		};
		Button button = new Button
		{
			Content = "Save",
			Width = 100.0,
			Height = 26.0,
			Margin = new Thickness(0.0, 0.0, 12.0, 0.0)
		};
		Button button2 = new Button
		{
			Content = "Cancel",
			Width = 80.0,
			Height = 26.0
		};
		button.Click += delegate
		{
			Commit();
		};
		button2.Click += delegate
		{
			base.DialogResult = false;
			Close();
		};
		stackPanel2.Children.Add(button);
		stackPanel2.Children.Add(button2);
		stackPanel.Children.Add(stackPanel2);
		base.Content = stackPanel;
	}

	private void Commit()
	{
		for (int i = 0; i < _entries.Count; i++)
		{
			string text = _boxes[i].Text.Trim();
			if (!(text == _entries[i].CurrentGuid))
			{
				ulong result2;
				if (Guid.TryParse(text, out var result))
				{
					_entries[i].NewKey = AntStateAssetEditor.GuidToKey(result);
					_entries[i].WasEdited = true;
				}
				else if (ulong.TryParse(text, NumberStyles.HexNumber, null, out result2))
				{
					_entries[i].NewKey = result2;
					_entries[i].WasEdited = true;
				}
			}
		}
		base.DialogResult = true;
		Close();
	}
}
