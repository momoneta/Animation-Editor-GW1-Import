using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AssetBankPlugin;

public static class ReflectionHelper
{
	public static IEnumerable<FieldInfo> GetAllFields(Type t)
	{
		if (t == null)
		{
			return Enumerable.Empty<FieldInfo>();
		}
		BindingFlags bindingAttr = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		return t.GetFields(bindingAttr).Concat(GetAllFields(t.BaseType));
	}

	public static IEnumerable<PropertyInfo> GetAllProperties(Type t)
	{
		if (t == null)
		{
			return Enumerable.Empty<PropertyInfo>();
		}
		BindingFlags bindingAttr = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		return t.GetProperties(bindingAttr).Concat(GetAllProperties(t.BaseType));
	}
}
