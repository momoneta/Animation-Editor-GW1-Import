using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetBankPlugin.Formats.GenericData;

namespace AssetBankPlugin.Import.Gw;

public sealed class GwSectionWriter
{
	public sealed class Result
	{
		public byte[] Section;

		public List<string> UnusedUpdates = new List<string>();

		public List<string> Written = new List<string>();
	}

	private readonly byte[] _bank;

	private readonly GwBankFile.Section _section;

	private readonly Dictionary<uint, GenericClass> _classes;

	private readonly IDictionary<string, object> _updates;

	private readonly bool _big;

	private readonly MemoryStream _out = new MemoryStream();

	private readonly List<int> _pointers = new List<int>();

	private readonly HashSet<string> _used = new HashSet<string>();

	private readonly List<string> _written = new List<string>();

	private GwSectionWriter(byte[] bank, GwBankFile.Section section, Dictionary<uint, GenericClass> classes, IDictionary<string, object> updates)
	{
		_bank = bank;
		_section = section;
		_classes = classes;
		_updates = updates ?? new Dictionary<string, object>();
		_big = section.BigEndian;
	}

	public static Result Write(byte[] bank, GwBankFile.Section section, Dictionary<uint, GenericClass> classes, IDictionary<string, object> updates)
	{
		if (section.Tag != "GD.DATA")
		{
			throw new ArgumentException("Only GD.DATA sections can be written, not " + section.Tag + ".");
		}
		return new GwSectionWriter(bank, section, classes, updates).Build();
	}

	private Result Build()
	{
		WriteObject(_section.DataStart);
		Pad(4);
		int num = (int)_out.Length;
		_pointers.Sort();
		byte[] array = new byte[16 + num + 4 + _pointers.Count * 4];
		Buffer.BlockCopy(_bank, _section.Start, array, 0, 8);
		GwBinary.WriteU32(array, 8, (uint)array.Length, _big);
		GwBinary.WriteU32(array, 12, (uint)(16 + num), _big);
		Buffer.BlockCopy(_out.GetBuffer(), 0, array, 16, num);
		int num2 = 16 + num;
		GwBinary.WriteU32(array, num2, (uint)_pointers.Count, _big);
		for (int i = 0; i < _pointers.Count; i++)
		{
			GwBinary.WriteU32(array, num2 + 4 + i * 4, (uint)_pointers[i], _big);
		}
		Result result = new Result
		{
			Section = array,
			Written = _written
		};
		foreach (string key in _updates.Keys)
		{
			if (!_used.Contains(key) && !key.StartsWith("__"))
			{
				result.UnusedUpdates.Add(key);
			}
		}
		return result;
	}

	private int WriteObject(int origObject)
	{
		uint num = GwBankFile.ObjectType(_bank, origObject, _big);
		if (!_classes.TryGetValue(num, out var value))
		{
			throw new InvalidOperationException($"Type 0x{num:X8} is not described in the GD.REFL of this bank.");
		}
		int num2 = GwBinary.ReadU16(_bank, origObject + 28, _big);
		int num3 = origObject + num2;
		CheckSupported(value, num3);
		Pad(8);
		int result = (int)_out.Position;
		_out.Write(_bank, origObject, num2);
		int num4 = (int)_out.Position;
		byte[] array = new byte[value.Size];
		Buffer.BlockCopy(_bank, num3, array, 0, value.Size);
		foreach (GenericField element in value.Elements)
		{
			if (!element.IsArray && element.Type != "String" && element.Type != "DataRef" && TryUpdate(element, out var value2))
			{
				EncodeScalar(array, element.Offset, element, value2);
				Mark(element.Name);
			}
		}
		_out.Write(array, 0, array.Length);
		foreach (GenericField item in value.Elements.OrderByDescending((GenericField f) => f.Offset))
		{
			if (item.IsArray || item.Type == "String")
			{
				WriteArray(item, num3, num4);
			}
		}
		GenericField genericField = value.Elements.FirstOrDefault((GenericField f) => f.Name == "__base" && !f.IsArray);
		if (genericField.Name != null)
		{
			long num5 = (long)GwBinary.ReadU64(_bank, num3 + genericField.Offset, _big);
			if (num5 != 0L)
			{
				int target = WriteObject(_section.DataStart + (int)num5);
				PatchPointer(num4 + genericField.Offset, target);
			}
		}
		return result;
	}

	private void WriteArray(GenericField field, int origFields, int newFields)
	{
		bool flag = field.Type == "String";
		int num = origFields + field.Offset;
		int num2 = (int)GwBinary.ReadU32(_bank, num + 4, _big);
		long num3 = (long)GwBinary.ReadU64(_bank, num + 8, _big);
		int num4 = (flag ? 1 : ElementStride(field));
		byte[] array = null;
		if (!flag && TryUpdate(field, out var value))
		{
			array = EncodeArray(field, value);
			Mark(field.Name);
		}
		if (array == null)
		{
			array = new byte[num2 * num4];
			if (array.Length != 0)
			{
				Buffer.BlockCopy(_bank, _section.DataStart + (int)num3, array, 0, array.Length);
			}
		}
		int num5 = array.Length / num4;
		int num6 = newFields + field.Offset;
		if (num5 == 0)
		{
			Patch(num6, new byte[16]);
			return;
		}
		Pad(flag ? 1 : Math.Min(num4, 8));
		int target = (int)_out.Position;
		_out.Write(array, 0, array.Length);
		byte[] array2 = new byte[8];
		GwBinary.WriteU32(array2, 0, (uint)num5, _big);
		GwBinary.WriteU32(array2, 4, (uint)num5, _big);
		Patch(num6, array2);
		PatchPointer(num6 + 8, target);
	}

	private void CheckSupported(GenericClass cls, int origFields)
	{
		foreach (GenericField element in cls.Elements)
		{
			if (element.Type == "DataRef" && !element.IsArray && element.Name != "__base" && GwBinary.ReadU64(_bank, origFields + element.Offset, _big) != 0L)
			{
				throw new NotSupportedException("Field '" + element.Name + "' of '" + cls.Name + "' points to another object of the section; this is not supported.");
			}
			if (PrimitiveSize(element.Type) == 0 && element.Type != "String" && element.Type != "DataRef" && ((!element.IsArray || GwBinary.ReadU32(_bank, origFields + element.Offset + 4, _big) != 0) ? 1 : 0) > (false ? 1 : 0) && HasPointers(element.TypeHash, 0))
			{
				throw new NotSupportedException("Field '" + element.Name + "' (" + element.Type + ") of '" + cls.Name + "' holds pointers; this is not supported.");
			}
		}
	}

	private bool HasPointers(uint type, int depth)
	{
		if (depth > 16 || !_classes.TryGetValue(type, out var value))
		{
			return true;
		}
		foreach (GenericField element in value.Elements)
		{
			if (element.IsArray || element.Type == "String" || element.Type == "DataRef")
			{
				return true;
			}
			if (PrimitiveSize(element.Type) == 0 && HasPointers(element.TypeHash, depth + 1))
			{
				return true;
			}
		}
		return false;
	}

	private bool TryUpdate(GenericField field, out object value)
	{
		value = null;
		if (!field.Name.StartsWith("__") && _updates.TryGetValue(field.Name, out value))
		{
			return value != null;
		}
		return false;
	}

	private void Mark(string name)
	{
		_used.Add(name);
		if (!_written.Contains(name))
		{
			_written.Add(name);
		}
	}

	public static int PrimitiveSize(string type)
	{
		switch (type)
		{
		case "Bool":
		case "Int8":
		case "UInt8":
			return 1;
		case "Int16":
		case "UInt16":
			return 2;
		case "Float":
		case "Int32":
		case "UInt32":
			return 4;
		case "Int64":
		case "UInt64":
		case "Double":
			return 8;
		case "Guid":
			return 16;
		default:
			return 0;
		}
	}

	private int ElementStride(GenericField field)
	{
		if (field.Type == "DataRef")
		{
			return 16;
		}
		int num = PrimitiveSize(field.Type);
		if (num > 0)
		{
			return num;
		}
		uint num2 = field.Size;
		if (field.Alignment != 0 && num2 % field.Alignment != 0)
		{
			num2 += field.Alignment - num2 % field.Alignment;
		}
		return (int)Math.Max(1u, num2);
	}

	private void EncodeScalar(byte[] b, int o, GenericField field, object value)
	{
		switch (field.Type)
		{
		case "Bool":
			b[o] = (Convert.ToBoolean(value) ? ((byte)1) : ((byte)0));
			return;
		case "Int8":
		case "UInt8":
			b[o] = (byte)ToInt64(value);
			return;
		case "Int16":
		case "UInt16":
			GwBinary.WriteU16(b, o, (ushort)ToInt64(value), _big);
			return;
		case "Int32":
		case "UInt32":
			GwBinary.WriteU32(b, o, (uint)ToInt64(value), _big);
			return;
		case "Int64":
		case "UInt64":
			GwBinary.WriteU64(b, o, (ulong)ToInt64(value), _big);
			return;
		case "Float":
			GwBinary.WriteF32(b, o, Convert.ToSingle(value), _big);
			return;
		case "Double":
			GwBinary.WriteF64(b, o, Convert.ToDouble(value), _big);
			return;
		case "Guid":
			GwBinary.WriteGuid(b, o, (Guid)value, _big);
			return;
		}
		throw new NotSupportedException("Cannot write a value into field '" + field.Name + "' (" + field.Type + ").");
	}

	private byte[] EncodeArray(GenericField field, object value)
	{
		if (!(value is IEnumerable source) || value is string)
		{
			throw new ArgumentException("Field '" + field.Name + "' is an array but the new value is " + value.GetType().Name + ".");
		}
		List<object> list = source.Cast<object>().ToList();
		if (field.Type == "DataRef")
		{
			byte[] array = new byte[list.Count * 16];
			for (int i = 0; i < list.Count; i++)
			{
				GwBinary.WriteGuid(array, i * 16, (Guid)list[i], _big);
			}
			return array;
		}
		int num = PrimitiveSize(field.Type);
		if (num == 0)
		{
			throw new NotSupportedException("Cannot write new values into array '" + field.Name + "' (" + field.Type + "[]).");
		}
		byte[] array2 = new byte[list.Count * num];
		for (int j = 0; j < list.Count; j++)
		{
			EncodeScalar(array2, j * num, field, list[j]);
		}
		return array2;
	}

	private static long ToInt64(object value)
	{
		if (value is ulong)
		{
			return (long)(ulong)value;
		}
		return Convert.ToInt64(value);
	}

	private void Pad(int alignment)
	{
		_out.Position = _out.Length;
		while (_out.Position % alignment != 0L)
		{
			_out.WriteByte(0);
		}
	}

	private void Patch(int position, byte[] bytes)
	{
		long position2 = _out.Position;
		_out.Position = position;
		_out.Write(bytes, 0, bytes.Length);
		_out.Position = position2;
	}

	private void PatchPointer(int position, int target)
	{
		byte[] array = new byte[8];
		GwBinary.WriteU64(array, 0, (ulong)target, _big);
		Patch(position, array);
		_pointers.Add(position);
	}
}
