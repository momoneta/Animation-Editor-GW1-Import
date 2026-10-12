using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AssetBankPlugin.Export;

public class CastNode
{
	public uint Identifier { get; set; }

	public ulong Hash { get; set; }

	public CastNode ParentNode { get; set; }

	public List<CastNode> ChildNodes { get; set; } = new List<CastNode>();

	public Dictionary<string, CastProperty> Properties { get; set; } = new Dictionary<string, CastProperty>();

	public CastNode(uint identifier)
	{
		Identifier = identifier;
		Hash = Cast.NextHash();
	}

	public T ChildOfType<T>() where T : CastNode
	{
		return ChildNodes.OfType<T>().FirstOrDefault();
	}

	public List<T> ChildrenOfType<T>() where T : CastNode
	{
		return ChildNodes.OfType<T>().ToList();
	}

	public CastNode ChildByHash(ulong hash)
	{
		return ChildNodes.FirstOrDefault((CastNode x) => x.Hash == hash);
	}

	public CastProperty CreateProperty(string name, string type)
	{
		CastProperty castProperty = new CastProperty(name, type);
		Properties[name] = castProperty;
		return castProperty;
	}

	public void SetPropertyVal<T>(string name, string type, T val)
	{
		CastProperty castProperty = CreateProperty(name, type);
		castProperty.Values.Clear();
		castProperty.Values.Add(val);
	}

	public void SetPropertyArray<T>(string name, string type, IEnumerable<T> values)
	{
		CastProperty castProperty = CreateProperty(name, type);
		castProperty.Values.Clear();
		foreach (T value in values)
		{
			castProperty.Values.Add(value);
		}
	}

	public T GetPropertyVal<T>(string name, T defaultVal = default(T))
	{
		if (Properties.TryGetValue(name, out var value) && value.Values.Count > 0)
		{
			try
			{
				return (T)Convert.ChangeType(value.Values[0], typeof(T));
			}
			catch
			{
				if (value.Values[0] is T result)
				{
					return result;
				}
			}
		}
		return defaultVal;
	}

	public List<T> GetPropertyArray<T>(string name)
	{
		if (Properties.TryGetValue(name, out var value))
		{
			return value.Values.Select((object v) => (T)Convert.ChangeType(v, typeof(T))).ToList();
		}
		return new List<T>();
	}

	public CastNode CreateChild(CastNode child)
	{
		child.ParentNode = this;
		ChildNodes.Add(child);
		return child;
	}

	public static CastNode LoadNode(BinaryReader reader)
	{
		uint identifier = reader.ReadUInt32();
		reader.ReadUInt32();
		ulong hash = reader.ReadUInt64();
		uint num = reader.ReadUInt32();
		uint num2 = reader.ReadUInt32();
		CastNode castNode = CreateNode(identifier);
		castNode.Hash = hash;
		for (uint num3 = 0u; num3 < num; num3++)
		{
			CastProperty castProperty = new CastProperty(reader);
			castNode.Properties[castProperty.Name] = castProperty;
		}
		for (uint num4 = 0u; num4 < num2; num4++)
		{
			CastNode castNode2 = LoadNode(reader);
			castNode2.ParentNode = castNode;
			castNode.ChildNodes.Add(castNode2);
		}
		return castNode;
	}

	public void Save(BinaryWriter writer)
	{
		writer.Write(Identifier);
		writer.Write(Length());
		writer.Write(Hash);
		writer.Write((uint)Properties.Count);
		writer.Write((uint)ChildNodes.Count);
		foreach (CastProperty value in Properties.Values)
		{
			value.Save(writer);
		}
		foreach (CastNode childNode in ChildNodes)
		{
			childNode.Save(writer);
		}
	}

	public uint Length()
	{
		uint num = 24u;
		foreach (CastProperty value in Properties.Values)
		{
			num += value.Length();
		}
		foreach (CastNode childNode in ChildNodes)
		{
			num += childNode.Length();
		}
		return num;
	}

	private static CastNode CreateNode(uint identifier)
	{
		return identifier switch
		{
			1953460082u => new RootNode(), 
			1818521453u => new ModelNode(), 
			1752393069u => new MeshNode(), 
			1919508840u => new HairNode(), 
			1752394850u => new BlendShapeNode(), 
			1818585971u => new SkeletonNode(), 
			1835626081u => new AnimationNode(), 
			1987212643u => new CurveNode(), 
			1448037699u => new CurveModeOverrideNode(), 
			1718187118u => new NotificationTrackNode(), 
			1701736290u => new BoneNode(), 
			1684564841u => new IKHandleNode(), 
			1953721955u => new ConstraintNode(), 
			1819566445u => new MaterialNode(), 
			1701603686u => new FileNode(), 
			1919709027u => new ColorNode(), 
			1953721961u => new InstanceNode(), 
			1635018093u => new MetadataNode(), 
			_ => new CastNode(identifier), 
		};
	}
}
