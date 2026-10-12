using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AssetBankPlugin.Export;

public class CastProperty
{
	public string Name { get; set; }

	public CastProperty_t PropertyType { get; set; }

	public List<object> Values { get; set; } = new List<object>();

	public CastProperty(string name, string typeIdentifier)
	{
		Name = name;
		PropertyType = new CastProperty_t(typeIdentifier);
	}

	public CastProperty(BinaryReader reader)
	{
		Load(reader);
	}

	public void Load(BinaryReader reader)
	{
		byte[] bytes = reader.ReadBytes(2);
		ushort count = reader.ReadUInt16();
		uint num = reader.ReadUInt32();
		byte[] bytes2 = reader.ReadBytes(count);
		Name = Encoding.UTF8.GetString(bytes2);
		string identifier = Encoding.UTF8.GetString(bytes).TrimEnd(default(char));
		PropertyType = new CastProperty_t(identifier);
		if (PropertyType.Size == 0 && PropertyType.Format == "s")
		{
			List<byte> list = new List<byte>();
			byte item;
			while ((item = reader.ReadByte()) != 0)
			{
				list.Add(item);
			}
			Values = new List<object> { Encoding.UTF8.GetString(list.ToArray()) };
			return;
		}
		int num2 = (int)num * PropertyType.ArrayCount;
		Values = new List<object>(num2);
		for (int i = 0; i < num2; i++)
		{
			string identifier2 = PropertyType.Identifier;
			if (identifier2 == null)
			{
				continue;
			}
			int length = identifier2.Length;
			if (length != 1)
			{
				if (length != 2)
				{
					continue;
				}
				switch (identifier2[0])
				{
				case '2':
					if (!(identifier2 == "2v"))
					{
						continue;
					}
					break;
				case '3':
					if (!(identifier2 == "3v"))
					{
						continue;
					}
					break;
				case '4':
					if (!(identifier2 == "4v"))
					{
						continue;
					}
					break;
				default:
					continue;
				}
			}
			else
			{
				switch (identifier2[0])
				{
				case 'b':
					Values.Add(reader.ReadByte());
					continue;
				case 'h':
					Values.Add(reader.ReadUInt16());
					continue;
				case 'i':
					Values.Add(reader.ReadUInt32());
					continue;
				case 'l':
					Values.Add(reader.ReadUInt64());
					continue;
				case 'f':
					break;
				case 'd':
					Values.Add(reader.ReadDouble());
					continue;
				default:
					continue;
				}
			}
			Values.Add(reader.ReadSingle());
		}
	}

	public void Save(BinaryWriter writer)
	{
		byte[] array = new byte[2];
		byte[] bytes = Encoding.UTF8.GetBytes(PropertyType.Identifier);
		Array.Copy(bytes, array, Math.Min(bytes.Length, 2));
		byte[] bytes2 = Encoding.UTF8.GetBytes(Name);
		writer.Write(array);
		writer.Write((ushort)bytes2.Length);
		writer.Write((uint)(Values.Count / PropertyType.ArrayCount));
		writer.Write(bytes2);
		if (PropertyType.Size == 0 && PropertyType.Format == "s")
		{
			string s = ((Values.Count <= 0) ? "" : (Values[0]?.ToString() ?? ""));
			byte[] bytes3 = Encoding.UTF8.GetBytes(s);
			writer.Write(bytes3);
			writer.Write((byte)0);
			return;
		}
		foreach (object value in Values)
		{
			string identifier = PropertyType.Identifier;
			if (identifier == null)
			{
				continue;
			}
			int length = identifier.Length;
			if (length != 1)
			{
				if (length != 2)
				{
					continue;
				}
				switch (identifier[0])
				{
				case '2':
					if (!(identifier == "2v"))
					{
						continue;
					}
					break;
				case '3':
					if (!(identifier == "3v"))
					{
						continue;
					}
					break;
				case '4':
					if (!(identifier == "4v"))
					{
						continue;
					}
					break;
				default:
					continue;
				}
			}
			else
			{
				switch (identifier[0])
				{
				case 'b':
					writer.Write(Convert.ToByte(value));
					continue;
				case 'h':
					writer.Write(Convert.ToUInt16(value));
					continue;
				case 'i':
					writer.Write(Convert.ToUInt32(value));
					continue;
				case 'l':
					writer.Write(Convert.ToUInt64(value));
					continue;
				case 'f':
					break;
				case 'd':
					writer.Write(Convert.ToDouble(value));
					continue;
				default:
					continue;
				}
			}
			writer.Write(Convert.ToSingle(value));
		}
	}

	public uint Length()
	{
		uint num = 8u;
		num += (uint)Encoding.UTF8.GetByteCount(Name);
		if (PropertyType.Size == 0 && PropertyType.Format == "s")
		{
			string s = ((Values.Count <= 0) ? "" : (Values[0]?.ToString() ?? ""));
			return num + (uint)(Encoding.UTF8.GetByteCount(s) + 1);
		}
		return num + (uint)(PropertyType.Size * (Values.Count / PropertyType.ArrayCount));
	}
}
