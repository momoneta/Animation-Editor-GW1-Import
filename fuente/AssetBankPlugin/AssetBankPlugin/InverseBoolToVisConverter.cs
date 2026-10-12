using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AssetBankPlugin;

public class InverseBoolToVisConverter : IValueConverter
{
	public object Convert(object value, Type t, object p, CultureInfo c)
	{
		bool flag = false;
		int num;
		if (value is bool)
		{
			flag = (bool)value;
			num = 1;
		}
		else
		{
			num = 0;
		}
		return (((uint)num & (flag ? 1u : 0u)) != 0) ? Visibility.Collapsed : Visibility.Visible;
	}

	public object ConvertBack(object value, Type t, object p, CultureInfo c)
	{
		throw new NotImplementedException();
	}
}
