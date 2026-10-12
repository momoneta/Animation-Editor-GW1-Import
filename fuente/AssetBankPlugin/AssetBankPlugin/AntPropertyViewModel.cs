using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using Frosty.Core;

namespace AssetBankPlugin;

public class AntPropertyViewModel : INotifyPropertyChanged
{
	private string _editedValue;

	private ObservableCollection<AntPropertyViewModel> _children;

	public string Name { get; set; }

	public string TypeStr { get; set; }

	public string ValueStr { get; set; }

	public object ValueObj { get; set; }

	public bool IsEditable { get; set; }

	public bool IsStructureModified { get; set; }

	public AntPropertyViewModel Parent { get; }

	public Action ModifiedCallback { get; set; }

	public string EditedValue
	{
		get
		{
			return _editedValue ?? ValueStr;
		}
		set
		{
			if (!(_editedValue == value))
			{
				_editedValue = value;
				ModifiedCallback?.Invoke();
			}
		}
	}

	public bool WasEdited
	{
		get
		{
			if (IsEditable && _editedValue != null)
			{
				return _editedValue != ValueStr;
			}
			return false;
		}
	}

	public Visibility EditableVisibility
	{
		get
		{
			if (IsEditable)
			{
				return Visibility.Visible;
			}
			return Visibility.Collapsed;
		}
	}

	public Visibility ReadOnlyVisibility
	{
		get
		{
			if (!IsEditable)
			{
				return Visibility.Visible;
			}
			return Visibility.Collapsed;
		}
	}

	public bool IsArrayElement
	{
		get
		{
			if (Parent != null)
			{
				if (!(Parent.ValueObj is Array))
				{
					return Parent.ValueObj is IList;
				}
				return true;
			}
			return false;
		}
	}

	public Visibility ArrayElementVisibility
	{
		get
		{
			if (IsArrayElement)
			{
				return Visibility.Visible;
			}
			return Visibility.Collapsed;
		}
	}

	public ICommand InsertBeforeCommand => new RelayCommand(delegate
	{
		Parent?.InsertArrayElement(this, 0);
	});

	public ICommand InsertAfterCommand => new RelayCommand(delegate
	{
		Parent?.InsertArrayElement(this, 1);
	});

	public ICommand DeleteCommand => new RelayCommand(delegate
	{
		Parent?.DeleteArrayElement(this);
	});

	public ICommand CopyValueCommand => new RelayCommand(delegate
	{
		if (!string.IsNullOrEmpty(ValueStr))
		{
			try
			{
				Clipboard.SetText(ValueStr);
			}
			catch
			{
			}
		}
	});

	public ObservableCollection<AntPropertyViewModel> Children
	{
		get
		{
			if (_children == null)
			{
				_children = new ObservableCollection<AntPropertyViewModel>();
				if (ValueObj != null)
				{
					Type type = ValueObj.GetType();
					if (!type.IsPrimitive && !(type == typeof(string)) && !(type == typeof(Guid)) && !type.IsEnum)
					{
						if (ValueObj is Dictionary<string, object> dictionary)
						{
							foreach (KeyValuePair<string, object> item in dictionary)
							{
								if (!(item.Key == "__typeHash") && !(item.Key == "__guid") && !(item.Key == "__key") && !(item.Key == "__name"))
								{
									AntPropertyViewModel antPropertyViewModel = new AntPropertyViewModel(item.Key, item.Value, this)
									{
										IsEditable = true
									};
									antPropertyViewModel.ModifiedCallback = ModifiedCallback;
									_children.Add(antPropertyViewModel);
								}
							}
						}
						else if (ValueObj is IEnumerable enumerable)
						{
							int num = 0;
							foreach (object item2 in enumerable)
							{
								AntPropertyViewModel antPropertyViewModel2 = new AntPropertyViewModel($"[{num++}]", item2, this)
								{
									IsEditable = true
								};
								antPropertyViewModel2.ModifiedCallback = ModifiedCallback;
								_children.Add(antPropertyViewModel2);
							}
						}
						else
						{
							foreach (PropertyInfo allProperty in ReflectionHelper.GetAllProperties(type))
							{
								try
								{
									AntPropertyViewModel antPropertyViewModel3 = new AntPropertyViewModel(allProperty.Name, allProperty.GetValue(ValueObj), this)
									{
										IsEditable = true
									};
									antPropertyViewModel3.ModifiedCallback = ModifiedCallback;
									_children.Add(antPropertyViewModel3);
								}
								catch
								{
								}
							}
							foreach (FieldInfo allField in ReflectionHelper.GetAllFields(type))
							{
								if (!allField.Name.Contains("<") && !allField.Name.Contains("k__BackingField"))
								{
									try
									{
										AntPropertyViewModel antPropertyViewModel4 = new AntPropertyViewModel(allField.Name, allField.GetValue(ValueObj), this)
										{
											IsEditable = true
										};
										antPropertyViewModel4.ModifiedCallback = ModifiedCallback;
										_children.Add(antPropertyViewModel4);
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
			return _children;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public AntPropertyViewModel(string name, object val, AntPropertyViewModel parent = null)
	{
		Name = name;
		ValueObj = val;
		Parent = parent;
		TypeStr = ((val != null) ? GetFriendlyTypeName(val.GetType()) : "Null");
		ValueStr = GetString(val);
	}

	public void InsertArrayElement(AntPropertyViewModel child, int offset)
	{
		if (ValueObj == null)
		{
			return;
		}
		int num = Children.IndexOf(child);
		if (num < 0)
		{
			return;
		}
		int num2 = num + offset;
		object obj = null;
		if (child.ValueObj != null)
		{
			obj = AntStateAssetEditor.DeepCopyRawData(child.ValueObj);
		}
		else
		{
			Type type = typeof(object);
			if (ValueObj is Array array)
			{
				type = array.GetType().GetElementType();
			}
			else if (ValueObj.GetType().IsGenericType && ValueObj.GetType().GetGenericTypeDefinition() == typeof(List<>))
			{
				type = ValueObj.GetType().GetGenericArguments()[0];
			}
			obj = ((type == typeof(string)) ? "" : ((type == typeof(Guid)) ? ((object)Guid.Empty) : ((!type.IsValueType) ? new Dictionary<string, object>() : Activator.CreateInstance(type))));
		}
		if (ValueObj is Array array2)
		{
			Array array3 = Array.CreateInstance(array2.GetType().GetElementType(), array2.Length + 1);
			int i = 0;
			int num3 = 0;
			for (; i < array2.Length + 1; i++)
			{
				if (i == num2)
				{
					array3.SetValue(obj, i);
				}
				else
				{
					array3.SetValue(array2.GetValue(num3++), i);
				}
			}
			ValueObj = array3;
		}
		else if (ValueObj is IList list)
		{
			list.Insert(num2, obj);
		}
		IsStructureModified = true;
		RebuildArrayChildren();
		ModifiedCallback?.Invoke();
	}

	public void DeleteArrayElement(AntPropertyViewModel child)
	{
		if (ValueObj == null)
		{
			return;
		}
		int num = Children.IndexOf(child);
		if (num < 0)
		{
			return;
		}
		if (ValueObj is Array array)
		{
			Array array2 = Array.CreateInstance(array.GetType().GetElementType(), array.Length - 1);
			int i = 0;
			int num2 = 0;
			for (; i < array.Length; i++)
			{
				if (i != num)
				{
					array2.SetValue(array.GetValue(i), num2++);
				}
			}
			ValueObj = array2;
		}
		else if (ValueObj is IList list)
		{
			list.RemoveAt(num);
		}
		IsStructureModified = true;
		RebuildArrayChildren();
		ModifiedCallback?.Invoke();
	}

	private void RebuildArrayChildren()
	{
		if (_children == null)
		{
			return;
		}
		_children.Clear();
		if (ValueObj is IEnumerable enumerable)
		{
			int num = 0;
			foreach (object item in enumerable)
			{
				AntPropertyViewModel antPropertyViewModel = new AntPropertyViewModel($"[{num++}]", item, this)
				{
					IsEditable = IsEditable
				};
				antPropertyViewModel.ModifiedCallback = ModifiedCallback;
				_children.Add(antPropertyViewModel);
			}
		}
		ValueStr = GetString(ValueObj);
		_editedValue = ValueStr;
		OnPropertyChanged("Children");
		OnPropertyChanged("ValueStr");
		OnPropertyChanged("EditedValue");
	}

	protected void OnPropertyChanged(string propName)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
	}

	private string GetFriendlyTypeName(Type t)
	{
		if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
		{
			return "List of " + t.GetGenericArguments()[0].Name;
		}
		return t.Name;
	}

	public string GetString(object obj)
	{
		if (obj == null)
		{
			return "null";
		}
		Type type = obj.GetType();
		if (obj is string text)
		{
			if (text.Length == 16 && ulong.TryParse(text, NumberStyles.HexNumber, null, out var result))
			{
				return KeyToGuid(result).ToString();
			}
			return text;
		}
		if (obj is ulong key && (Name.Contains("Anim") || Name.Contains("Target") || Name.Contains("Key") || Name.Contains("Asset") || Name.Contains("TagCollectionSet")))
		{
			return KeyToGuid(key).ToString();
		}
		if (type.IsPrimitive || type == typeof(Guid) || type.IsEnum)
		{
			return obj.ToString();
		}
		if (obj is Dictionary<string, object>)
		{
			return "[Struct]";
		}
		if (obj is IEnumerable enumerable)
		{
			int num = 0;
			IEnumerator enumerator = enumerable.GetEnumerator();
			while (enumerator.MoveNext())
			{
				num++;
			}
			return $"[{num} Items]";
		}
		return "[" + type.Name + "]";
	}

	public static Guid KeyToGuid(ulong key)
	{
		byte[] array = new byte[16];
		BitConverter.GetBytes(key).CopyTo(array, 0);
		return new Guid(array);
	}
}
