using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using AssetBankPlugin.Ant;
using Frosty.Controls;

namespace AssetBankPlugin.Windows;

public class TreePreviewer : FrostyWindow, IComponentConnector
{
	private enum ViewMode
	{
		Local,
		Global
	}

	private sealed class GraphNode
	{
		public AntAsset Asset;

		public int Depth;

		public double X;

		public double Y;

		public Border Visual;

		public const double W = 220.0;

		public const double H = 58.0;
	}

	private sealed class GraphEdge
	{
		public GraphNode From;

		public GraphNode To;

		public string FieldName;

		public Path Visual;
	}

	private readonly AntAsset _root;

	private readonly Guid _rootVmId;

	private readonly Dictionary<Guid, AntAsset> _allById;

	private readonly List<AntAsset> _allAssets;

	private Dictionary<AntAsset, List<(AntAsset referer, string field)>> _reverseByAsset;

	private readonly List<GraphNode> _nodes = new List<GraphNode>();

	private readonly List<GraphEdge> _edges = new List<GraphEdge>();

	private readonly HashSet<(GraphNode, GraphNode, string)> _edgeSet = new HashSet<(GraphNode, GraphNode, string)>();

	private readonly Dictionary<Guid, GraphNode> _nodeByCanonId = new Dictionary<Guid, GraphNode>();

	private readonly ScaleTransform _scaleT = new ScaleTransform(1.0, 1.0);

	private readonly TranslateTransform _translateT = new TranslateTransform(0.0, 0.0);

	private GraphNode _draggingNode;

	private Point _dragStartMouse;

	private Point _dragStartNode;

	private bool _isPanning;

	private Point _panStart;

	private ViewMode _currentMode = ViewMode.Global;

	private int _forwardDepth = 3;

	private int _backwardDepth = 3;

	public Action<AntAsset> AssetDoubleClicked;

	private const double ColSpacing = 300.0;

	private const double RowSpacing = 90.0;

	private const double OriginX = 60.0;

	private const double OriginY = 60.0;

	private const int LocalFwdDepth = 0;

	private const int LocalBwdDepth = 1;

	private const int GlobalNodes = 200;

	private static readonly HashSet<string> s_skipKeys = new HashSet<string> { "__name", "__guid", "__key", "__base", "__typeHash" };

	internal TextBlock PART_RootLabel;

	internal TextBlock PART_TypeLabel;

	internal Border PART_DepthPanel;

	internal TextBox PART_ForwardDepth;

	internal TextBox PART_BackwardDepth;

	internal Button PART_ApplyDepthBtn;

	internal RadioButton PART_LocalBtn;

	internal RadioButton PART_GlobalBtn;

	internal Button PART_OrganiseBtn;

	internal Button PART_FitBtn;

	internal Border PART_CanvasHost;

	internal Canvas PART_Canvas;

	internal Border PART_LoadingOverlay;

	internal TextBlock PART_LoadingText;

	private bool _contentLoaded;

	public TreePreviewer(AntAsset rootAsset, Guid rootVmId, IEnumerable<AntAssetViewModel> allVms)
	{
		if (rootAsset == null)
		{
			throw new ArgumentNullException("rootAsset");
		}
		InitializeComponent();
		_root = rootAsset;
		_rootVmId = rootVmId;
		_allById = new Dictionary<Guid, AntAsset>();
		HashSet<AntAsset> hashSet = new HashSet<AntAsset>();
		foreach (AntAssetViewModel allVm in allVms)
		{
			if (allVm.AssetInstance is AntAsset antAsset)
			{
				hashSet.Add(antAsset);
				if (allVm.Id != Guid.Empty && !_allById.ContainsKey(allVm.Id))
				{
					_allById[allVm.Id] = antAsset;
				}
				if (antAsset.ID != Guid.Empty && !_allById.ContainsKey(antAsset.ID))
				{
					_allById[antAsset.ID] = antAsset;
				}
				Guid guid = RawKeyGuid(antAsset);
				if (guid != Guid.Empty && !_allById.ContainsKey(guid))
				{
					_allById[guid] = antAsset;
				}
			}
		}
		_allAssets = hashSet.ToList();
		PART_RootLabel.Text = rootAsset.Name ?? "Unknown";
		PART_TypeLabel.Text = rootAsset.AssetType ?? string.Empty;
		base.Title = "Ant Asset Tree  -  " + (rootAsset.Name ?? "Unknown");
		TransformGroup renderTransform = new TransformGroup
		{
			Children = 
			{
				(Transform)_scaleT,
				(Transform)_translateT
			}
		};
		PART_Canvas.RenderTransform = renderTransform;
		PART_CanvasHost.MouseRightButtonDown += CanvasHost_RightDown;
		PART_CanvasHost.MouseMove += CanvasHost_MouseMove;
		PART_CanvasHost.MouseRightButtonUp += CanvasHost_RightUp;
		PART_CanvasHost.MouseWheel += CanvasHost_Wheel;
		PART_FitBtn.Click += delegate
		{
			FitView();
		};
		PART_OrganiseBtn.Click += delegate
		{
			Organise();
		};
		PART_LocalBtn.Checked += delegate
		{
			SwitchMode(ViewMode.Local);
		};
		PART_GlobalBtn.Checked += delegate
		{
			SwitchMode(ViewMode.Global);
		};
		PART_ApplyDepthBtn.Click += delegate
		{
			ApplyDepthSettings();
		};
		PART_ForwardDepth.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				ApplyDepthSettings();
			}
		};
		PART_BackwardDepth.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				ApplyDepthSettings();
			}
		};
		base.Loaded += OnLoaded;
	}

	private async void OnLoaded(object sender, RoutedEventArgs e)
	{
		base.Loaded -= OnLoaded;
		ShowLoading("Building index...");
		await Task.Run(delegate
		{
			BuildReverseIndex();
		});
		await RebuildAsync();
	}

	private void ShowLoading(string message)
	{
		if (message == null)
		{
			PART_LoadingOverlay.Visibility = Visibility.Collapsed;
			return;
		}
		PART_LoadingText.Text = message;
		PART_LoadingOverlay.Visibility = Visibility.Visible;
	}

	private void BuildReverseIndex()
	{
		_reverseByAsset = new Dictionary<AntAsset, List<(AntAsset, string)>>();
		foreach (AntAsset allAsset in _allAssets)
		{
			if (allAsset.RawData == null)
			{
				continue;
			}
			List<(string, Guid)> list = new List<(string, Guid)>();
			CollectRefs(allAsset.RawData, list, topLevel: true);
			foreach (var (item, key) in list)
			{
				if (_allById.TryGetValue(key, out var value) && value != allAsset)
				{
					if (!_reverseByAsset.TryGetValue(value, out List<(AntAsset, string)> value2))
					{
						List<(AntAsset, string)> list2 = (_reverseByAsset[value] = new List<(AntAsset, string)>());
						value2 = list2;
					}
					value2.Add((allAsset, item));
				}
			}
		}
	}

	private void SwitchMode(ViewMode mode)
	{
		if (_currentMode != mode)
		{
			_currentMode = mode;
			PART_DepthPanel.Visibility = ((mode != ViewMode.Global) ? Visibility.Collapsed : Visibility.Visible);
			Rebuild();
		}
	}

	private void ApplyDepthSettings()
	{
		if (!int.TryParse(PART_ForwardDepth.Text.Trim(), out var result) || result < 0)
		{
			result = 3;
		}
		if (!int.TryParse(PART_BackwardDepth.Text.Trim(), out var result2) || result2 < 0)
		{
			result2 = 3;
		}
		_forwardDepth = Math.Min(result, 20);
		_backwardDepth = Math.Min(result2, 20);
		PART_ForwardDepth.Text = _forwardDepth.ToString();
		PART_BackwardDepth.Text = _backwardDepth.ToString();
		Rebuild();
	}

	private void Rebuild()
	{
		RebuildAsync();
	}

	private async Task RebuildAsync()
	{
		ShowLoading("Building graph...");
		PART_Canvas.Children.Clear();
		_nodes.Clear();
		_edges.Clear();
		_edgeSet.Clear();
		_nodeByCanonId.Clear();
		int fwdDepth = ((_currentMode != ViewMode.Local) ? _forwardDepth : 0);
		int bwdDepth = ((_currentMode == ViewMode.Local) ? 1 : _backwardDepth);
		int nodeLimit = ((_currentMode == ViewMode.Local) ? int.MaxValue : 200);
		await Task.Run(delegate
		{
			BuildBidirectionalGraph(fwdDepth, bwdDepth, nodeLimit);
		});
		LayoutNodes();
		RenderGraph();
		FitView();
		ShowLoading(null);
	}

	private void BuildBidirectionalGraph(int fwdDepth, int bwdDepth, int nodeLimit)
	{
		GraphNode item = RegisterNode(_root, _rootVmId, 0);
		Queue<(GraphNode, int)> queue = new Queue<(GraphNode, int)>();
		queue.Enqueue((item, 0));
		while (queue.Count > 0 && _nodes.Count < nodeLimit)
		{
			var (graphNode, num) = queue.Dequeue();
			if (num >= bwdDepth || graphNode.Asset.RawData == null)
			{
				continue;
			}
			List<(string, Guid)> list = new List<(string, Guid)>();
			CollectRefs(graphNode.Asset.RawData, list, topLevel: true);
			foreach (var (field, guid) in list)
			{
				if (_allById.TryGetValue(guid, out var value) && value != graphNode.Asset)
				{
					GraphNode graphNode2 = FindNode(guid);
					if (graphNode2 == null)
					{
						graphNode2 = RegisterNode(value, guid, num + 1);
						queue.Enqueue((graphNode2, num + 1));
					}
					AddEdge(graphNode, graphNode2, field);
				}
			}
		}
		Queue<(GraphNode, int)> queue2 = new Queue<(GraphNode, int)>();
		queue2.Enqueue((item, 0));
		while (queue2.Count > 0 && _nodes.Count < nodeLimit)
		{
			var (graphNode3, num2) = queue2.Dequeue();
			if (num2 <= -fwdDepth || !_reverseByAsset.TryGetValue(graphNode3.Asset, out List<(AntAsset, string)> value2))
			{
				continue;
			}
			foreach (var (antAsset, field2) in value2)
			{
				if (_nodes.Count >= nodeLimit)
				{
					break;
				}
				GraphNode graphNode4 = FindNode(antAsset.ID) ?? FindNode(RawKeyGuid(antAsset));
				if (graphNode4 != null)
				{
					AddEdge(graphNode4, graphNode3, field2);
					continue;
				}
				GraphNode graphNode5 = RegisterNode(antAsset, antAsset.ID, num2 - 1);
				AddEdge(graphNode5, graphNode3, field2);
				queue2.Enqueue((graphNode5, num2 - 1));
			}
		}
	}

	private GraphNode RegisterNode(AntAsset asset, Guid primaryId, int depth)
	{
		GraphNode graphNode = new GraphNode
		{
			Asset = asset,
			Depth = depth
		};
		_nodes.Add(graphNode);
		if (primaryId != Guid.Empty)
		{
			_nodeByCanonId[primaryId] = graphNode;
		}
		if (asset.ID != Guid.Empty)
		{
			_nodeByCanonId[asset.ID] = graphNode;
		}
		Guid guid = RawKeyGuid(asset);
		if (guid != Guid.Empty)
		{
			_nodeByCanonId[guid] = graphNode;
		}
		return graphNode;
	}

	private GraphNode FindNode(Guid id)
	{
		if (!(id != Guid.Empty) || !_nodeByCanonId.TryGetValue(id, out var value))
		{
			return null;
		}
		return value;
	}

	private void AddEdge(GraphNode from, GraphNode to, string field)
	{
		if (from != null && to != null && from != to && _edgeSet.Add((from, to, field)))
		{
			_edges.Add(new GraphEdge
			{
				From = from,
				To = to,
				FieldName = field
			});
		}
	}

	private static Guid RawKeyGuid(AntAsset asset)
	{
		if (asset.RawData == null)
		{
			return Guid.Empty;
		}
		if (asset.RawData.TryGetValue("__key", out var value) || asset.RawData.TryGetValue("__guid", out value))
		{
			return TryExtractGuid(value);
		}
		return Guid.Empty;
	}

	private static Guid TryExtractGuid(object val)
	{
		if (val == null)
		{
			return Guid.Empty;
		}
		if (val is Guid)
		{
			return (Guid)val;
		}
		if (val is string text)
		{
			if (text.Length > 0 && text.Length <= 16 && ulong.TryParse(text, NumberStyles.HexNumber, null, out var result) && result != 0L)
			{
				byte[] array = new byte[16];
				BitConverter.GetBytes(result).CopyTo(array, 0);
				return new Guid(array);
			}
			if (Guid.TryParse(text, out var result2))
			{
				return result2;
			}
			return Guid.Empty;
		}
		if (val is ulong num && num != 0L)
		{
			byte[] array2 = new byte[16];
			BitConverter.GetBytes(num).CopyTo(array2, 0);
			return new Guid(array2);
		}
		if (val is long num2 && num2 > 0)
		{
			byte[] array3 = new byte[16];
			BitConverter.GetBytes(num2).CopyTo(array3, 0);
			return new Guid(array3);
		}
		return Guid.Empty;
	}

	private static void CollectRefs(Dictionary<string, object> data, List<(string, Guid)> refs, bool topLevel, string prefix = "")
	{
		if (data == null)
		{
			return;
		}
		foreach (KeyValuePair<string, object> datum in data)
		{
			string key = datum.Key;
			if (s_skipKeys.Contains(key))
			{
				continue;
			}
			object value = datum.Value;
			if (value != null)
			{
				string text = ((prefix.Length == 0) ? key : (prefix + "." + key));
				Guid guid = TryExtractGuid(value);
				if (guid != Guid.Empty)
				{
					refs.Add((text, guid));
				}
				else if (value is Dictionary<string, object> data2)
				{
					CollectRefs(data2, refs, topLevel: false, text);
				}
				else if (value is object[] arr)
				{
					CollectRefsArray(arr, refs, text);
				}
			}
		}
	}

	private static void CollectRefsArray(object[] arr, List<(string, Guid)> refs, string prefix)
	{
		for (int i = 0; i < arr.Length; i++)
		{
			object obj = arr[i];
			if (obj != null)
			{
				string text = prefix + "[" + i + "]";
				Guid guid = TryExtractGuid(obj);
				if (guid != Guid.Empty)
				{
					refs.Add((text, guid));
				}
				else if (obj is Dictionary<string, object> data)
				{
					CollectRefs(data, refs, topLevel: false, text);
				}
				else if (obj is object[] arr2)
				{
					CollectRefsArray(arr2, refs, text);
				}
			}
		}
	}

	private void LayoutNodes()
	{
		if (_nodes.Count == 0)
		{
			return;
		}
		Dictionary<GraphNode, List<GraphNode>> dictionary = new Dictionary<GraphNode, List<GraphNode>>();
		Dictionary<GraphNode, List<GraphNode>> dictionary2 = new Dictionary<GraphNode, List<GraphNode>>();
		foreach (GraphEdge edge in _edges)
		{
			if (!dictionary.TryGetValue(edge.To, out var value))
			{
				List<GraphNode> list = (dictionary[edge.To] = new List<GraphNode>());
				value = list;
			}
			value.Add(edge.From);
			if (!dictionary2.TryGetValue(edge.From, out var value2))
			{
				List<GraphNode> list = (dictionary2[edge.From] = new List<GraphNode>());
				value2 = list;
			}
			value2.Add(edge.To);
		}
		int num = _nodes.Min((GraphNode n) => n.Depth);
		int num2 = _nodes.Max((GraphNode n) => n.Depth);
		foreach (GraphNode node in _nodes)
		{
			node.X = 60.0 + (double)(node.Depth - num) * 300.0;
		}
		Dictionary<int, List<GraphNode>> dictionary3 = (from n in _nodes
			group n by n.Depth).ToDictionary((IGrouping<int, GraphNode> g) => g.Key, (IGrouping<int, GraphNode> g) => g.ToList());
		if (dictionary3.TryGetValue(0, out var value3))
		{
			PlaceColumn(value3, null);
		}
		for (int num3 = 1; num3 <= num2; num3++)
		{
			if (dictionary3.TryGetValue(num3, out var value4))
			{
				PlaceColumn(value4, dictionary);
			}
		}
		for (int num4 = -1; num4 >= num; num4--)
		{
			if (dictionary3.TryGetValue(num4, out var value5))
			{
				PlaceColumn(value5, dictionary2);
			}
		}
	}

	private void PlaceColumn(List<GraphNode> col, Dictionary<GraphNode, List<GraphNode>> towardRoot)
	{
		int count = col.Count;
		double[] desired = new double[count];
		for (int i = 0; i < count; i++)
		{
			if (towardRoot != null && towardRoot.TryGetValue(col[i], out var value) && value.Count > 0)
			{
				double num = 0.0;
				foreach (GraphNode item in value)
				{
					num += item.Y;
				}
				desired[i] = num / (double)value.Count;
			}
			else
			{
				desired[i] = 0.0;
			}
		}
		List<int> list = (from num8 in Enumerable.Range(0, count)
			orderby desired[num8]
			select num8).ToList();
		double num2 = double.NegativeInfinity;
		foreach (int item2 in list)
		{
			double num3 = desired[item2];
			if (num3 < num2 + 90.0)
			{
				num3 = num2 + 90.0;
			}
			col[item2].Y = num3;
			num2 = num3;
		}
		double num4 = 0.0;
		double num5 = 0.0;
		for (int num6 = 0; num6 < count; num6++)
		{
			num4 += desired[num6];
			num5 += col[num6].Y;
		}
		if (count <= 0)
		{
			return;
		}
		double num7 = (num4 - num5) / (double)count;
		foreach (GraphNode item3 in col)
		{
			item3.Y += num7;
		}
	}

	private void RenderGraph()
	{
		PART_Canvas.Children.Clear();
		foreach (GraphEdge edge in _edges)
		{
			edge.Visual = BuildEdgePath(edge);
			PART_Canvas.Children.Add(edge.Visual);
		}
		foreach (GraphNode node in _nodes)
		{
			node.Visual = BuildNodeBorder(node);
			Canvas.SetLeft(node.Visual, node.X);
			Canvas.SetTop(node.Visual, node.Y);
			PART_Canvas.Children.Add(node.Visual);
		}
	}

	private Border BuildNodeBorder(GraphNode node)
	{
		bool flag = node.Asset == _root;
		SolidColorBrush background = new SolidColorBrush(flag ? Color.FromRgb(15, 58, 86) : Color.FromRgb(42, 42, 45));
		SolidColorBrush borderBrush = new SolidColorBrush(flag ? Color.FromRgb(0, 122, 204) : Color.FromRgb(63, 63, 70));
		TextBlock textBlock = new TextBlock
		{
			Text = (node.Asset.AssetType ?? "Unknown"),
			Foreground = new SolidColorBrush(flag ? Color.FromRgb(128, 200, byte.MaxValue) : Color.FromRgb(128, 128, 128)),
			FontSize = 10.0,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(8.0, 0.0, 8.0, 0.0),
			TextTrimming = TextTrimming.CharacterEllipsis
		};
		TextOptions.SetTextFormattingMode(textBlock, TextFormattingMode.Display);
		TextBlock element = new TextBlock
		{
			Text = (node.Asset.Name ?? "Unknown"),
			Foreground = Brushes.White,
			FontSize = 12.0,
			FontWeight = FontWeights.SemiBold,
			Margin = new Thickness(8.0, 6.0, 8.0, 6.0),
			TextTrimming = TextTrimming.CharacterEllipsis
		};
		TextOptions.SetTextFormattingMode(element, TextFormattingMode.Display);
		StackPanel stackPanel = new StackPanel();
		stackPanel.Children.Add(new Border
		{
			Background = background,
			Height = 22.0,
			CornerRadius = new CornerRadius(3.0, 3.0, 0.0, 0.0),
			Child = textBlock
		});
		stackPanel.Children.Add(element);
		TextBlock textBlock2 = new TextBlock
		{
			Text = (node.Asset.AssetType ?? "?") + "  |  " + node.Asset.ID.ToString(),
			Foreground = Brushes.White,
			FontSize = 11.0
		};
		TextOptions.SetTextFormattingMode(textBlock2, TextFormattingMode.Display);
		Border border = new Border();
		border.Width = 220.0;
		border.Background = new SolidColorBrush(Color.FromRgb(37, 37, 38));
		border.BorderBrush = borderBrush;
		border.BorderThickness = new Thickness(1.0);
		border.CornerRadius = new CornerRadius(4.0);
		border.Child = stackPanel;
		border.Cursor = Cursors.SizeAll;
		border.ToolTip = new ToolTip
		{
			Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
			BorderBrush = new SolidColorBrush(Color.FromRgb(63, 63, 70)),
			BorderThickness = new Thickness(1.0),
			Padding = new Thickness(7.0, 4.0, 7.0, 4.0),
			HasDropShadow = true,
			Content = textBlock2
		};
		border.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			if (e.ClickCount >= 2)
			{
				AssetDoubleClicked?.Invoke(node.Asset);
				e.Handled = true;
			}
			else
			{
				NodeDragStart(node, e);
			}
		};
		border.MouseMove += delegate(object s, MouseEventArgs e)
		{
			NodeDragMove(node, e);
		};
		border.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			NodeDragEnd(node, e);
		};
		MenuItem menuItem = new MenuItem
		{
			Header = "Copy GUID"
		};
		menuItem.Click += delegate
		{
			try
			{
				Clipboard.SetText(node.Asset.ID.ToString());
			}
			catch
			{
			}
		};
		ContextMenu contextMenu = new ContextMenu
		{
			Items = { (object)menuItem }
		};
		if (node.Asset.RawData != null && (node.Asset.RawData.TryGetValue("__key", out var value) || node.Asset.RawData.TryGetValue("__guid", out value)) && value != null)
		{
			string rawStr = value.ToString();
			if (rawStr != "0000000000000000" && rawStr != string.Empty)
			{
				MenuItem menuItem2 = new MenuItem
				{
					Header = "Copy Key  (" + rawStr + ")"
				};
				menuItem2.Click += delegate
				{
					try
					{
						Clipboard.SetText(rawStr);
					}
					catch
					{
					}
				};
				contextMenu.Items.Add(menuItem2);
			}
		}
		border.ContextMenu = contextMenu;
		return border;
	}

	private Path BuildEdgePath(GraphEdge edge)
	{
		Path path = new Path
		{
			Stroke = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
			StrokeThickness = 1.5,
			Opacity = 0.7,
			IsHitTestVisible = false
		};
		RefreshEdgePath(edge.From, edge.To, path);
		return path;
	}

	private static void RefreshEdgePath(GraphNode from, GraphNode to, Path path)
	{
		double num = from.X + 220.0;
		double y = from.Y + 29.0;
		double x = to.X;
		double y2 = to.Y + 29.0;
		double num2 = Math.Max(60.0, Math.Abs(x - num) * 0.45);
		PathFigure pathFigure = new PathFigure
		{
			StartPoint = new Point(num, y),
			IsFilled = false
		};
		pathFigure.Segments.Add(new BezierSegment(new Point(num + num2, y), new Point(x - num2, y2), new Point(x, y2), isStroked: true));
		path.Data = new PathGeometry(new PathFigure[1] { pathFigure });
	}

	private void UpdateEdgesForNode(GraphNode node)
	{
		foreach (GraphEdge edge in _edges)
		{
			if (edge.From == node || edge.To == node)
			{
				RefreshEdgePath(edge.From, edge.To, edge.Visual);
			}
		}
	}

	private void NodeDragStart(GraphNode node, MouseButtonEventArgs e)
	{
		_draggingNode = node;
		_dragStartMouse = e.GetPosition(PART_CanvasHost);
		_dragStartNode = new Point(node.X, node.Y);
		node.Visual.CaptureMouse();
		e.Handled = true;
	}

	private void NodeDragMove(GraphNode node, MouseEventArgs e)
	{
		if (_draggingNode == node && node.Visual.IsMouseCaptured)
		{
			Point position = e.GetPosition(PART_CanvasHost);
			node.X = _dragStartNode.X + (position.X - _dragStartMouse.X) / _scaleT.ScaleX;
			node.Y = _dragStartNode.Y + (position.Y - _dragStartMouse.Y) / _scaleT.ScaleY;
			Canvas.SetLeft(node.Visual, node.X);
			Canvas.SetTop(node.Visual, node.Y);
			UpdateEdgesForNode(node);
		}
	}

	private void NodeDragEnd(GraphNode node, MouseButtonEventArgs e)
	{
		if (_draggingNode == node)
		{
			_draggingNode = null;
			node.Visual.ReleaseMouseCapture();
		}
	}

	private void CanvasHost_RightDown(object sender, MouseButtonEventArgs e)
	{
		_isPanning = true;
		_panStart = e.GetPosition(PART_CanvasHost);
		PART_CanvasHost.CaptureMouse();
		e.Handled = true;
	}

	private void CanvasHost_MouseMove(object sender, MouseEventArgs e)
	{
		if (_isPanning)
		{
			Point position = e.GetPosition(PART_CanvasHost);
			_translateT.X += position.X - _panStart.X;
			_translateT.Y += position.Y - _panStart.Y;
			_panStart = position;
		}
	}

	private void CanvasHost_RightUp(object sender, MouseButtonEventArgs e)
	{
		_isPanning = false;
		PART_CanvasHost.ReleaseMouseCapture();
	}

	private void CanvasHost_Wheel(object sender, MouseWheelEventArgs e)
	{
		double num = ((e.Delta > 0) ? 1.1 : 0.90909090909090906);
		double num2 = Math.Max(0.15, Math.Min(4.0, _scaleT.ScaleX * num));
		double num3 = num2 / _scaleT.ScaleX;
		Point position = e.GetPosition(PART_CanvasHost);
		_translateT.X = position.X - (position.X - _translateT.X) * num3;
		_translateT.Y = position.Y - (position.Y - _translateT.Y) * num3;
		_scaleT.ScaleX = num2;
		_scaleT.ScaleY = num2;
	}

	private void Organise()
	{
		LayoutNodes();
		foreach (GraphNode node in _nodes)
		{
			if (node.Visual != null)
			{
				Canvas.SetLeft(node.Visual, node.X);
				Canvas.SetTop(node.Visual, node.Y);
			}
		}
		foreach (GraphEdge edge in _edges)
		{
			if (edge.Visual != null)
			{
				RefreshEdgePath(edge.From, edge.To, edge.Visual);
			}
		}
		FitView();
	}

	private void FitView()
	{
		if (_nodes.Count != 0)
		{
			double num = _nodes.Min((GraphNode n) => n.X);
			double num2 = _nodes.Min((GraphNode n) => n.Y);
			double num3 = _nodes.Max((GraphNode n) => n.X) + 220.0;
			double num4 = _nodes.Max((GraphNode n) => n.Y) + 58.0;
			double num5 = num3 - num + 80.0;
			double num6 = num4 - num2 + 80.0;
			double actualWidth = PART_CanvasHost.ActualWidth;
			double actualHeight = PART_CanvasHost.ActualHeight;
			if (!(actualWidth < 1.0) && !(actualHeight < 1.0))
			{
				double num7 = Math.Min(1.0, Math.Min(actualWidth / num5, actualHeight / num6));
				_scaleT.ScaleX = num7;
				_scaleT.ScaleY = num7;
				_translateT.X = (actualWidth - num5 * num7) / 2.0 - (num - 40.0) * num7;
				_translateT.Y = (actualHeight - num6 * num7) / 2.0 - (num2 - 40.0) * num7;
			}
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "9.0.14.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/AssetBankPlugin;component/windows/treepreviewer.xaml", UriKind.Relative);
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
			PART_RootLabel = (TextBlock)target;
			break;
		case 2:
			PART_TypeLabel = (TextBlock)target;
			break;
		case 3:
			PART_DepthPanel = (Border)target;
			break;
		case 4:
			PART_ForwardDepth = (TextBox)target;
			break;
		case 5:
			PART_BackwardDepth = (TextBox)target;
			break;
		case 6:
			PART_ApplyDepthBtn = (Button)target;
			break;
		case 7:
			PART_LocalBtn = (RadioButton)target;
			break;
		case 8:
			PART_GlobalBtn = (RadioButton)target;
			break;
		case 9:
			PART_OrganiseBtn = (Button)target;
			break;
		case 10:
			PART_FitBtn = (Button)target;
			break;
		case 11:
			PART_CanvasHost = (Border)target;
			break;
		case 12:
			PART_Canvas = (Canvas)target;
			break;
		case 13:
			PART_LoadingOverlay = (Border)target;
			break;
		case 14:
			PART_LoadingText = (TextBlock)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
