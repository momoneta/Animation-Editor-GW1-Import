namespace AssetBankPlugin.Export;

public class RootNode : CastNode
{
	public RootNode()
		: base(1953460082u)
	{
	}

	public ModelNode CreateModel()
	{
		return (ModelNode)CreateChild(new ModelNode());
	}

	public AnimationNode CreateAnimation()
	{
		return (AnimationNode)CreateChild(new AnimationNode());
	}

	public InstanceNode CreateInstance()
	{
		return (InstanceNode)CreateChild(new InstanceNode());
	}

	public MetadataNode CreateMetadata()
	{
		return (MetadataNode)CreateChild(new MetadataNode());
	}
}
