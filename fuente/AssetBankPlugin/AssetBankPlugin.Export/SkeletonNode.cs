using System.Collections.Generic;

namespace AssetBankPlugin.Export;

public class SkeletonNode : CastNode
{
	public List<BoneNode> Bones => ChildrenOfType<BoneNode>();

	public List<IKHandleNode> IKHandles => ChildrenOfType<IKHandleNode>();

	public List<ConstraintNode> Constraints => ChildrenOfType<ConstraintNode>();

	public SkeletonNode()
		: base(1818585971u)
	{
	}

	public BoneNode CreateBone()
	{
		return (BoneNode)CreateChild(new BoneNode());
	}

	public IKHandleNode CreateIKHandle()
	{
		return (IKHandleNode)CreateChild(new IKHandleNode());
	}

	public ConstraintNode CreateConstraint()
	{
		return (ConstraintNode)CreateChild(new ConstraintNode());
	}
}
