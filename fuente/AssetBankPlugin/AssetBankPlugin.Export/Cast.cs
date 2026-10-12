using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AssetBankPlugin.Export;

public class Cast
{
	private static ulong _hashBase = 6002815992723690064uL;

	public List<CastNode> RootNodes { get; set; } = new List<CastNode>();

	public static ulong NextHash()
	{
		return _hashBase++;
	}

	public static string CastTypeForMaximum(IEnumerable<uint> values)
	{
		if (!values.Any())
		{
			return "b";
		}
		uint num = values.Max();
		if (num <= 255)
		{
			return "b";
		}
		if (num <= 65535)
		{
			return "h";
		}
		return "i";
	}

	public RootNode CreateRoot()
	{
		RootNode rootNode = new RootNode();
		RootNodes.Add(rootNode);
		return rootNode;
	}

	public static Cast Load(string path)
	{
		using FileStream stream = File.OpenRead(path);
		return Load(stream);
	}

	public static Cast Load(Stream stream)
	{
		using BinaryReader binaryReader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
		if (binaryReader.ReadUInt32() != 1953718627)
		{
			throw new InvalidDataException("Invalid cast file magic");
		}
		binaryReader.ReadUInt32();
		uint num = binaryReader.ReadUInt32();
		binaryReader.ReadUInt32();
		Cast cast = new Cast();
		for (int i = 0; i < num; i++)
		{
			cast.RootNodes.Add(CastNode.LoadNode(binaryReader));
		}
		return cast;
	}

	public void Save(string path)
	{
		using FileStream stream = File.Create(path);
		Save(stream);
	}

	public void Save(Stream stream)
	{
		using BinaryWriter binaryWriter = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
		binaryWriter.Write(1953718627u);
		binaryWriter.Write(1u);
		binaryWriter.Write((uint)RootNodes.Count);
		binaryWriter.Write(0u);
		foreach (CastNode rootNode in RootNodes)
		{
			rootNode.Save(binaryWriter);
		}
	}
}
