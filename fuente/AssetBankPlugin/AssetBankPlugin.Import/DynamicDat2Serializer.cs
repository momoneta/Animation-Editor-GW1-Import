using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using AssetBankPlugin.Ant;
using AssetBankPlugin.Formats;
using AssetBankPlugin.Formats.GenericData2;

namespace AssetBankPlugin.Import;

public static class DynamicDat2Serializer
{
	private class SerializationContext
	{
		public bool BigEndian { get; }

		public Dictionary<uint, GenericClass2> Classes { get; }

		public MemoryStream Stream { get; } = new MemoryStream();

		public List<int> LayoutDataOffsets { get; } = new List<int>();

		public List<PendingPointer> PendingPointers { get; } = new List<PendingPointer>();

		public Dictionary<object, int> SerializedAddresses { get; } = new Dictionary<object, int>();

		public Dictionary<object, int> ObjectRefCounts { get; } = new Dictionary<object, int>();

		public SerializationContext(bool bigEndian, Dictionary<uint, GenericClass2> classes)
		{
			BigEndian = bigEndian;
			Classes = classes;
		}

		public void RegisterReference(object target)
		{
			if (target != null)
			{
				if (!ObjectRefCounts.ContainsKey(target))
				{
					ObjectRefCounts[target] = 0;
				}
				ObjectRefCounts[target]++;
			}
		}
	}

	private class PendingPointer
	{
		public int SourceOffset { get; set; }

		public object Target { get; set; }

		public int Alignment { get; set; }

		public bool IsLayoutData { get; set; }

		public uint TypeHash { get; set; }
	}

	private class StringPayload
	{
		public string Value { get; }

		public StringPayload(string v)
		{
			Value = v;
		}
	}

	private class PrimitiveArrayPayload
	{
		public IEnumerable List { get; }

		public uint ElementTypeHash { get; }

		public uint ElementSize { get; }

		public uint ElementAlignment { get; }

		public PrimitiveArrayPayload(IEnumerable list, uint typeHash, uint size, uint align)
		{
			List = list;
			ElementTypeHash = typeHash;
			ElementSize = size;
			ElementAlignment = align;
		}
	}

	private class InlineStructArrayPayload
	{
		public IEnumerable List { get; }

		public uint ElementTypeHash { get; }

		public uint ElementSize { get; }

		public uint ElementAlignment { get; }

		public InlineStructArrayPayload(IEnumerable list, uint typeHash, uint size, uint align)
		{
			List = list;
			ElementTypeHash = typeHash;
			ElementSize = size;
			ElementAlignment = align;
		}
	}

	private class StringArrayPayload
	{
		public List<string> Strings { get; }

		public StringArrayPayload(List<string> strings)
		{
			Strings = strings;
		}
	}

	private class ReferenceObjectArrayPayload
	{
		public IEnumerable List { get; }

		public uint ElementTypeHash { get; }

		public ReferenceObjectArrayPayload(IEnumerable list, uint typeHash)
		{
			List = list;
			ElementTypeHash = typeHash;
		}
	}

	private const ulong kIID = 51274245uL;

	public static byte[] Serialize(AntAsset asset, Dictionary<uint, GenericClass2> classes, bool bigEndian)
	{
		SerializationContext serializationContext = new SerializationContext(bigEndian, classes);
		uint typeHashForAsset = GetTypeHashForAsset(asset, classes);
		byte[] buffer = new byte[12];
		serializationContext.Stream.Write(buffer, 0, 12);
		serializationContext.PendingPointers.Add(new PendingPointer
		{
			SourceOffset = -1,
			Target = asset,
			Alignment = 16,
			IsLayoutData = true,
			TypeHash = typeHashForAsset
		});
		int num = 0;
		while (num < serializationContext.PendingPointers.Count)
		{
			PendingPointer pendingPointer = serializationContext.PendingPointers[num];
			num++;
			object target = pendingPointer.Target;
			if (serializationContext.SerializedAddresses.TryGetValue(target, out var value))
			{
				if (pendingPointer.SourceOffset != -1)
				{
					PatchRelativePointer(serializationContext.Stream, pendingPointer.SourceOffset, value, bigEndian);
				}
				continue;
			}
			int num2 = AlignUp((int)serializationContext.Stream.Length - 12, pendingPointer.Alignment) + 12;
			serializationContext.Stream.SetLength(num2);
			serializationContext.Stream.Position = num2;
			serializationContext.SerializedAddresses[target] = num2;
			if (pendingPointer.SourceOffset != -1)
			{
				PatchRelativePointer(serializationContext.Stream, pendingPointer.SourceOffset, num2, bigEndian);
			}
			if (pendingPointer.IsLayoutData)
			{
				serializationContext.LayoutDataOffsets.Add(num2);
				uint num3 = pendingPointer.TypeHash;
				if (num3 == 18 || num3 == 0)
				{
					if (target is Dictionary<string, object> dictionary && dictionary.TryGetValue("__typeHash", out var value2))
					{
						num3 = Convert.ToUInt32(value2);
					}
					else if (target is AntAsset asset2)
					{
						num3 = GetTypeHashForAsset(asset2, serializationContext.Classes);
					}
				}
				uint refCount = 0u;
				if (target != asset && serializationContext.ObjectRefCounts.TryGetValue(target, out var value3))
				{
					refCount = (uint)value3;
				}
				WriteLayoutDataHeader(serializationContext.Stream, num3, refCount, bigEndian);
				int startOffset = num2 + 32;
				SerializeObject(target, num3, startOffset, serializationContext);
			}
			else if (target is StringPayload stringPayload)
			{
				byte[] bytes = Encoding.UTF8.GetBytes(stringPayload.Value);
				serializationContext.Stream.Write(bytes, 0, bytes.Length);
				serializationContext.Stream.WriteByte(0);
			}
			else if (target is PrimitiveArrayPayload pap)
			{
				SerializePrimitiveArray(pap, serializationContext);
			}
			else if (target is InlineStructArrayPayload isap)
			{
				SerializeInlineStructArray(isap, serializationContext);
			}
			else if (target is StringArrayPayload sap)
			{
				SerializeStringArray(sap, serializationContext);
			}
			else if (target is ReferenceObjectArrayPayload roap)
			{
				SerializeReferenceObjectArray(roap, serializationContext);
			}
		}
		return BuildDAT2Section(serializationContext.Stream.ToArray(), serializationContext.LayoutDataOffsets, bigEndian);
	}

	private static void SerializeObject(object obj, uint typeHash, int startOffset, SerializationContext ctx)
	{
		if (!ctx.Classes.TryGetValue(typeHash, out var value))
		{
			throw new Exception($"Layout for type hash 0x{typeHash:X8} not found in this bank.");
		}
		byte[] array = new byte[value.Size];
		foreach (GenericField2 element in value.Elements)
		{
			object fieldValue = GetFieldValue(obj, element.Name);
			int offset = element.Offset;
			if (element.IsList)
			{
				int num = ((fieldValue != null) ? GetCountOfVal(fieldValue) : 0);
				if (num > 0)
				{
					WriteU32(array, offset, (uint)num, ctx.BigEndian);
					WriteU32(array, offset + 4, (uint)num, ctx.BigEndian);
					object target;
					if (element.ElementTypeHash == 17 || element.Type == "String[]")
					{
						List<string> list = new List<string>();
						foreach (object item in (IEnumerable)fieldValue)
						{
							list.Add(item?.ToString() ?? "");
						}
						target = new StringArrayPayload(list);
					}
					else if (PrimitiveTypeMap.IsPrimitive(element.ElementTypeHash) && element.ElementTypeHash != 18)
					{
						target = new PrimitiveArrayPayload((IEnumerable)fieldValue, element.ElementTypeHash, element.ElementSize, element.ElementAlignment);
					}
					else if (IsReferenceType(element, ctx.Classes))
					{
						target = new ReferenceObjectArrayPayload((IEnumerable)fieldValue, element.ElementTypeHash);
					}
					else
					{
						uint num2 = element.ElementSize;
						uint num3 = element.ElementAlignment;
						if (ctx.Classes.TryGetValue(element.ElementTypeHash, out var value2))
						{
							if (num2 == 0)
							{
								num2 = (uint)value2.Size;
							}
							if (num3 == 0)
							{
								num3 = (uint)value2.Alignment;
							}
						}
						if (num2 == 0)
						{
							num2 = 8u;
						}
						if (num3 == 0)
						{
							num3 = 4u;
						}
						target = new InlineStructArrayPayload((IEnumerable)fieldValue, element.ElementTypeHash, num2, num3);
					}
					ctx.PendingPointers.Add(new PendingPointer
					{
						SourceOffset = startOffset + offset + 8,
						Target = target,
						Alignment = (int)((element.ElementAlignment != 0) ? element.ElementAlignment : 8),
						IsLayoutData = false
					});
				}
				else
				{
					WriteU32(array, offset, 0u, ctx.BigEndian);
					WriteU32(array, offset + 4, 0u, ctx.BigEndian);
					WriteU64(array, offset + 8, 0uL, ctx.BigEndian);
				}
			}
			else if (element.Type == "String")
			{
				if (fieldValue != null && fieldValue.ToString().Length > 0)
				{
					string text = fieldValue.ToString();
					int v = text.Length + 1;
					WriteU32(array, offset, (uint)v, ctx.BigEndian);
					WriteU32(array, offset + 4, (uint)v, ctx.BigEndian);
					ctx.PendingPointers.Add(new PendingPointer
					{
						SourceOffset = startOffset + offset + 8,
						Target = new StringPayload(text),
						Alignment = 1,
						IsLayoutData = false
					});
				}
				else
				{
					WriteU32(array, offset, 0u, ctx.BigEndian);
					WriteU32(array, offset + 4, 0u, ctx.BigEndian);
					WriteU64(array, offset + 8, 0uL, ctx.BigEndian);
				}
			}
			else if (element.InlineCount > 1)
			{
				SerializeInlineArray(array, offset, fieldValue, element, startOffset + offset, ctx);
			}
			else
			{
				SerializePrimitiveOrStruct(array, offset, fieldValue, element, startOffset + offset, ctx);
			}
		}
		ctx.Stream.Position = startOffset;
		ctx.Stream.Write(array, 0, array.Length);
	}

	private static bool IsReferenceType(GenericField2 field, Dictionary<uint, GenericClass2> classes)
	{
		if (field.ElementTypeHash == 18)
		{
			return true;
		}
		if (field.ElementSize == 8 && field.ElementAlignment == 8 && !field.IsNative)
		{
			if (classes.TryGetValue(field.ElementTypeHash, out var value) && (value.Elements.Count > 1 || (value.Elements.Count == 1 && PrimitiveTypeMap.IsPrimitive(value.Elements[0].TypeHash))))
			{
				return false;
			}
			return true;
		}
		return false;
	}

	private static void SerializePrimitiveOrStruct(byte[] fieldData, int fieldOff, object val, GenericField2 field, int absoluteOffset, SerializationContext ctx)
	{
		if (PrimitiveTypeMap.IsPrimitive(field.TypeHash) && field.TypeHash != 18)
		{
			WritePrimitiveValue(fieldData, fieldOff, val, field.TypeHash, ctx.BigEndian);
		}
		else if (field.Type == "Guid")
		{
			byte[] array = SafeGuid(val).ToByteArray();
			if (ctx.BigEndian)
			{
				Array.Reverse(array, 0, 4);
				Array.Reverse(array, 4, 2);
				Array.Reverse(array, 6, 2);
			}
			Buffer.BlockCopy(array, 0, fieldData, fieldOff, 16);
		}
		else if (field.TypeHash == 18 || (field.Size == 8 && field.Alignment == 8 && !field.IsNative))
		{
			if (val != null)
			{
				ctx.RegisterReference(val);
				ctx.PendingPointers.Add(new PendingPointer
				{
					SourceOffset = absoluteOffset,
					Target = val,
					Alignment = 16,
					IsLayoutData = true,
					TypeHash = field.TypeHash
				});
			}
			else
			{
				WriteU64(fieldData, fieldOff, 0uL, ctx.BigEndian);
			}
		}
		else if (val != null)
		{
			SerializeObject(val, field.TypeHash, absoluteOffset, ctx);
		}
	}

	private static void SerializeInlineArray(byte[] fieldData, int fieldOff, object val, GenericField2 field, int absoluteOffset, SerializationContext ctx)
	{
		if (!(val is IEnumerable enumerable))
		{
			return;
		}
		List<object> list = new List<object>();
		foreach (object item in enumerable)
		{
			list.Add(item);
		}
		uint num = field.ElementSize;
		if (num == 0)
		{
			num = ((!ctx.Classes.TryGetValue(field.ElementTypeHash, out var value)) ? 8u : ((uint)value.Size));
		}
		int num2 = (int)field.ElementAlignment;
		if (num2 == 0)
		{
			num2 = 4;
		}
		num = (uint)((num + num2 - 1) & ~(num2 - 1));
		for (int i = 0; i < field.InlineCount && i < list.Count; i++)
		{
			int num3 = fieldOff + (int)(i * num);
			if (PrimitiveTypeMap.IsPrimitive(field.ElementTypeHash) && field.ElementTypeHash != 18)
			{
				WritePrimitiveValue(fieldData, num3, list[i], field.ElementTypeHash, ctx.BigEndian);
			}
			else if (field.ElementTypeHash == 18 || (field.ElementSize == 8 && field.ElementAlignment == 8 && !field.IsNative))
			{
				if (list[i] != null)
				{
					ctx.RegisterReference(list[i]);
					ctx.PendingPointers.Add(new PendingPointer
					{
						SourceOffset = absoluteOffset + num3,
						Target = list[i],
						Alignment = 16,
						IsLayoutData = true,
						TypeHash = field.ElementTypeHash
					});
				}
				else
				{
					WriteU64(fieldData, num3, 0uL, ctx.BigEndian);
				}
			}
			else
			{
				SerializeObject(list[i], field.ElementTypeHash, absoluteOffset + num3, ctx);
			}
		}
	}

	private static void SerializePrimitiveArray(PrimitiveArrayPayload pap, SerializationContext ctx)
	{
		List<object> list = new List<object>();
		foreach (object item in pap.List)
		{
			list.Add(item);
		}
		uint elementSize = pap.ElementSize;
		int num = (int)pap.ElementAlignment;
		if (num == 0)
		{
			num = 4;
		}
		elementSize = (uint)((elementSize + num - 1) & ~(num - 1));
		byte[] array = new byte[list.Count * elementSize];
		for (int i = 0; i < list.Count; i++)
		{
			WritePrimitiveValue(array, (int)(i * elementSize), list[i], pap.ElementTypeHash, ctx.BigEndian);
		}
		ctx.Stream.Write(array, 0, array.Length);
	}

	private static void SerializeInlineStructArray(InlineStructArrayPayload isap, SerializationContext ctx)
	{
		List<object> list = new List<object>();
		foreach (object item in isap.List)
		{
			list.Add(item);
		}
		uint elementSize = isap.ElementSize;
		int num = (int)isap.ElementAlignment;
		if (num == 0)
		{
			num = 4;
		}
		elementSize = (uint)((elementSize + num - 1) & ~(num - 1));
		int num2 = (int)ctx.Stream.Position;
		int num3 = list.Count * (int)elementSize;
		byte[] buffer = new byte[num3];
		ctx.Stream.Write(buffer, 0, num3);
		for (int i = 0; i < list.Count; i++)
		{
			object obj = list[i];
			if (obj != null)
			{
				int startOffset = num2 + i * (int)elementSize;
				SerializeObject(obj, isap.ElementTypeHash, startOffset, ctx);
			}
		}
	}

	private static void SerializeStringArray(StringArrayPayload sap, SerializationContext ctx)
	{
		List<string> strings = sap.Strings;
		long position = ctx.Stream.Position;
		for (int i = 0; i < strings.Count; i++)
		{
			string text = strings[i];
			int v = text.Length + 1;
			int num = (int)(position + i * 16);
			byte[] array = new byte[16];
			WriteU32(array, 0, (uint)v, ctx.BigEndian);
			WriteU32(array, 4, (uint)v, ctx.BigEndian);
			ctx.PendingPointers.Add(new PendingPointer
			{
				SourceOffset = num + 8,
				Target = new StringPayload(text),
				Alignment = 1,
				IsLayoutData = false
			});
			ctx.Stream.Write(array, 0, 16);
		}
	}

	private static void SerializeReferenceObjectArray(ReferenceObjectArrayPayload roap, SerializationContext ctx)
	{
		List<object> list = new List<object>();
		foreach (object item in roap.List)
		{
			list.Add(item);
		}
		long position = ctx.Stream.Position;
		for (int i = 0; i < list.Count; i++)
		{
			object obj = list[i];
			if (obj != null)
			{
				ctx.RegisterReference(obj);
				ctx.PendingPointers.Add(new PendingPointer
				{
					SourceOffset = (int)(position + i * 8),
					Target = obj,
					Alignment = 16,
					IsLayoutData = true,
					TypeHash = roap.ElementTypeHash
				});
			}
			WriteU64ToStream(ctx.Stream, 0uL, ctx.BigEndian);
		}
	}

	public static uint GetTypeHashForAsset(AntAsset asset, Dictionary<uint, GenericClass2> classes)
	{
		foreach (KeyValuePair<uint, GenericClass2> @class in classes)
		{
			if (@class.Value.Name == asset.AssetType)
			{
				return @class.Key;
			}
		}
		if (asset.AssetType == "ClipControllerAsset")
		{
			return 2108648108u;
		}
		if (asset.AssetType == "ActorControllerAsset")
		{
			return 3127372384u;
		}
		throw new Exception("Unknown asset layout type: " + asset.AssetType);
	}

	private static object GetFieldValue(object obj, string fieldName)
	{
		if (obj is AntAsset { RawData: not null } antAsset && antAsset.RawData.TryGetValue(fieldName, out var value))
		{
			return value;
		}
		if (obj is Dictionary<string, object> dictionary && dictionary.TryGetValue(fieldName, out var value2))
		{
			return value2;
		}
		PropertyInfo property = obj.GetType().GetProperty(fieldName);
		if (property != null)
		{
			return property.GetValue(obj);
		}
		FieldInfo field = obj.GetType().GetField(fieldName);
		if (field != null)
		{
			return field.GetValue(obj);
		}
		return null;
	}

	private static int GetCountOfVal(object val)
	{
		if (val is string text)
		{
			if (text.Length <= 0)
			{
				return 0;
			}
			return text.Length + 1;
		}
		if (val is ICollection collection)
		{
			return collection.Count;
		}
		if (val is IEnumerable enumerable)
		{
			int num = 0;
			{
				foreach (object item in enumerable)
				{
					_ = item;
					num++;
				}
				return num;
			}
		}
		return 0;
	}

	private static Guid SafeGuid(object obj)
	{
		if (obj == null)
		{
			return Guid.Empty;
		}
		if (obj is Guid)
		{
			return (Guid)obj;
		}
		if (obj is string input && Guid.TryParse(input, out var result))
		{
			return result;
		}
		return Guid.Empty;
	}

	private static void PatchRelativePointer(MemoryStream ms, int sourceOffset, int targetOffset, bool bigEndian)
	{
		long value = ((targetOffset - sourceOffset) & 0xFFFFFFFFFFFFFFFL) | 0x1000000000000000L;
		long position = ms.Position;
		ms.Position = sourceOffset;
		byte[] bytes = BitConverter.GetBytes(value);
		if (bigEndian)
		{
			Array.Reverse(bytes);
		}
		ms.Write(bytes, 0, 8);
		ms.Position = position;
	}

	private static void WriteLayoutDataHeader(MemoryStream ms, uint typeHash, uint refCount, bool bigEndian)
	{
		byte[] array = new byte[32];
		WriteU64(array, 0, 51274245uL, bigEndian);
		WriteU64(array, 8, 0uL, bigEndian);
		WriteU64(array, 16, typeHash, bigEndian);
		WriteU32(array, 24, refCount, bigEndian);
		WriteU16(array, 28, 32, bigEndian);
		WriteU16(array, 30, 0, bigEndian);
		ms.Write(array, 0, 32);
	}

	private static byte[] BuildDAT2Section(byte[] rawPayload, List<int> layoutDataOffsets, bool bigEndian)
	{
		int count = layoutDataOffsets.Count;
		int num = count * 8 + 2;
		int num2 = rawPayload.Length + num;
		byte[] array = new byte[num2];
		Buffer.BlockCopy(rawPayload, 0, array, 0, rawPayload.Length);
		array[0] = 71;
		array[1] = 68;
		array[2] = 46;
		array[3] = 68;
		array[4] = 65;
		array[5] = 84;
		array[6] = 50;
		array[7] = (byte)(bigEndian ? 98u : 108u);
		WriteU32(array, 8, (uint)num2, bigEndian);
		int num3 = rawPayload.Length;
		for (int i = 0; i < count; i++)
		{
			int num4 = layoutDataOffsets[i];
			int num5 = num3 + i * 8;
			long v = ((num4 - num5) & 0xFFFFFFFFFFFFFFFL) | 0x1000000000000000L;
			WriteU64(array, num5, (ulong)v, bigEndian);
		}
		WriteU16(array, num3 + count * 8, (ushort)count, bigEndian);
		return array;
	}

	private static int AlignUp(int v, int a)
	{
		if (a > 1)
		{
			return (v + a - 1) & ~(a - 1);
		}
		return v;
	}

	private static void WriteU64(byte[] b, int o, ulong v, bool bigEndian)
	{
		if (bigEndian)
		{
			v = Swap(v);
		}
		for (int i = 0; i < 8; i++)
		{
			b[o + i] = (byte)(v >> i * 8);
		}
	}

	private static void WriteU32(byte[] b, int o, uint v, bool bigEndian)
	{
		if (bigEndian)
		{
			v = Swap(v);
		}
		for (int i = 0; i < 4; i++)
		{
			b[o + i] = (byte)(v >> i * 8);
		}
	}

	private static void WriteU16(byte[] b, int o, ushort v, bool bigEndian)
	{
		if (bigEndian)
		{
			v = Swap(v);
		}
		b[o] = (byte)v;
		b[o + 1] = (byte)(v >> 8);
	}

	private static void WriteU64ToStream(MemoryStream ms, ulong v, bool bigEndian)
	{
		byte[] array = new byte[8];
		WriteU64(array, 0, v, bigEndian);
		ms.Write(array, 0, 8);
	}

	private static ulong Swap(ulong v)
	{
		return ((v & 0xFF) << 56) | (((v >> 8) & 0xFF) << 48) | (((v >> 16) & 0xFF) << 40) | (((v >> 24) & 0xFF) << 32) | (((v >> 32) & 0xFF) << 24) | (((v >> 40) & 0xFF) << 16) | (((v >> 48) & 0xFF) << 8) | ((v >> 56) & 0xFF);
	}

	private static uint Swap(uint v)
	{
		return ((v & 0xFF) << 24) | (((v >> 8) & 0xFF) << 16) | (((v >> 16) & 0xFF) << 8) | ((v >> 24) & 0xFF);
	}

	private static ushort Swap(ushort v)
	{
		return (ushort)(((v & 0xFF) << 8) | ((v >> 8) & 0xFF));
	}

	private static ulong GuidToKey(Guid g)
	{
		return BitConverter.ToUInt64(g.ToByteArray(), 0);
	}

	private static void WritePrimitiveValue(byte[] b, int o, object val, uint typeHash, bool bigEndian)
	{
		if (val == null)
		{
			return;
		}
		switch (typeHash)
		{
		case 1u:
			b[o] = (Convert.ToBoolean(val) ? ((byte)1) : ((byte)0));
			break;
		case 2u:
			b[o] = (byte)Convert.ToSByte(val);
			break;
		case 3u:
			b[o] = Convert.ToByte(val);
			break;
		case 4u:
			WriteU16(b, o, (ushort)Convert.ToInt16(val), bigEndian);
			break;
		case 5u:
			WriteU16(b, o, Convert.ToUInt16(val), bigEndian);
			break;
		case 6u:
			WriteU32(b, o, (uint)Convert.ToInt32(val), bigEndian);
			break;
		case 7u:
			WriteU32(b, o, Convert.ToUInt32(val), bigEndian);
			break;
		case 8u:
			WriteU64(b, o, (ulong)Convert.ToInt64(val), bigEndian);
			break;
		case 9u:
			WriteU64(b, o, Convert.ToUInt64(val), bigEndian);
			break;
		case 10u:
			WriteU32(b, o, BitConverter.ToUInt32(BitConverter.GetBytes(Convert.ToSingle(val)), 0), bigEndian);
			break;
		case 19u:
			WriteU64(b, o, BitConverter.ToUInt32(BitConverter.GetBytes(Convert.ToDouble(val)), 0), bigEndian);
			break;
		case 35u:
		{
			ulong result = 0uL;
			if (val is Guid g)
			{
				result = GuidToKey(g);
			}
			else if (val is string text)
			{
				ulong result3;
				if (Guid.TryParse(text, out var result2))
				{
					result = GuidToKey(result2);
				}
				else if (ulong.TryParse(text, NumberStyles.HexNumber, null, out result3))
				{
					result = result3;
				}
				else
				{
					ulong.TryParse(text, out result);
				}
			}
			else
			{
				result = Convert.ToUInt64(val);
			}
			WriteU64(b, o, result, bigEndian);
			break;
		}
		case 11u:
		case 12u:
		case 13u:
		case 14u:
		{
			float[] array2 = (float[])val;
			for (int j = 0; j < array2.Length; j++)
			{
				WriteU32(b, o + j * 4, BitConverter.ToUInt32(BitConverter.GetBytes(array2[j]), 0), bigEndian);
			}
			break;
		}
		case 15u:
		{
			float[] array = (float[])val;
			for (int i = 0; i < 16 && i < array.Length; i++)
			{
				WriteU32(b, o + i * 4, BitConverter.ToUInt32(BitConverter.GetBytes(array[i]), 0), bigEndian);
			}
			break;
		}
		}
	}
}
