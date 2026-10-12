using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Frosty.Core;
using FrostySdk.Managers.Entries;

namespace AssetBankPlugin;

public class BankPickerDialog : AssetPickerDialog
{
	private static readonly HashSet<EbxAssetEntry> _selectedCache = new HashSet<EbxAssetEntry>();

	private List<EbxAssetEntry> _allEntries;

	private ObservableCollection<BankNode> _rootNodes = new ObservableCollection<BankNode>();

	public static void UpdateSelection(EbxAssetEntry entry, bool selected)
	{
		if (selected)
		{
			_selectedCache.Add(entry);
		}
		else
		{
			_selectedCache.Remove(entry);
		}
	}

	public static IEnumerable<EbxAssetEntry> PeekSelection()
	{
		return _selectedCache;
	}

	public BankPickerDialog()
	{
		base.Title = "  Select Reference Banks";
		base.Width = 580.0;
		base.Height = 660.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		_allEntries = (from x in App.AssetManager.EnumerateEbx("AntStateAsset")
			orderby x.Path, x.Filename
			select x).ToList();
		BuildTree("");
		ConfigureFolderTree("AntStateAsset", "Search by name or path…", _rootNodes, "#1C3852", "Load Selected", "Check banks then press Load Selected");
		OnTreeSearch = delegate(string q)
		{
			BuildTree(q ?? "");
			NotifySelectionChanged();
		};
	}

	private void BuildTree(string query)
	{
		_rootNodes.Clear();
		bool flag = !string.IsNullOrWhiteSpace(query);
		Dictionary<string, BankNode> dictionary = new Dictionary<string, BankNode>(StringComparer.OrdinalIgnoreCase);
		foreach (EbxAssetEntry allEntry in _allEntries)
		{
			if (flag && allEntry.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 && allEntry.Path.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
			{
				continue;
			}
			string[] array = allEntry.Path.Split(new char[1] { '/' }, StringSplitOptions.RemoveEmptyEntries);
			BankNode bankNode = null;
			string text = "";
			for (int i = 0; i < array.Length; i++)
			{
				text = text + array[i] + "/";
				if (!dictionary.TryGetValue(text, out var value))
				{
					value = new BankNode
					{
						Name = array[i],
						IsFolder = true,
						IsExpanded = flag
					};
					if (bankNode == null)
					{
						_rootNodes.Add(value);
					}
					else
					{
						bankNode.Children.Add(value);
					}
					dictionary[text] = value;
				}
				bankNode = value;
			}
			BankNode item = new BankNode
			{
				Name = allEntry.Filename,
				Entry = allEntry,
				IsFolder = false,
				IsSelected = _selectedCache.Contains(allEntry)
			};
			if (bankNode == null)
			{
				_rootNodes.Add(item);
			}
			else
			{
				bankNode.Children.Add(item);
			}
		}
	}
}
