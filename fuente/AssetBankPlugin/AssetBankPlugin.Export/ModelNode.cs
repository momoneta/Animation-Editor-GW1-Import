using System.Collections.Generic;

namespace AssetBankPlugin.Export;

public class ModelNode : CastNode
{
	public string Name => GetPropertyVal<string>("n");

	public List<float> Position => GetPropertyArray<float>("p");

	public List<float> Rotation => GetPropertyArray<float>("r");

	public List<float> Scale => GetPropertyArray<float>("s");

	public SkeletonNode Skeleton => ChildOfType<SkeletonNode>();

	public List<MeshNode> Meshes => ChildrenOfType<MeshNode>();

	public List<HairNode> Hairs => ChildrenOfType<HairNode>();

	public List<MaterialNode> Materials => ChildrenOfType<MaterialNode>();

	public List<BlendShapeNode> BlendShapes => ChildrenOfType<BlendShapeNode>();

	public ModelNode()
		: base(1818521453u)
	{
	}

	public void SetName(string name)
	{
		SetPropertyVal("n", "s", name);
	}

	public void SetPosition(float[] position)
	{
		SetPropertyArray("p", "3v", position);
	}

	public void SetRotation(float[] rotation)
	{
		SetPropertyArray("r", "4v", rotation);
	}

	public void SetScale(float[] scale)
	{
		SetPropertyArray("s", "3v", scale);
	}

	public SkeletonNode CreateSkeleton()
	{
		return (SkeletonNode)CreateChild(new SkeletonNode());
	}

	public MeshNode CreateMesh()
	{
		return (MeshNode)CreateChild(new MeshNode());
	}

	public HairNode CreateHair()
	{
		return (HairNode)CreateChild(new HairNode());
	}

	public MaterialNode CreateMaterial()
	{
		return (MaterialNode)CreateChild(new MaterialNode());
	}

	public BlendShapeNode CreateBlendShape()
	{
		return (BlendShapeNode)CreateChild(new BlendShapeNode());
	}
}
