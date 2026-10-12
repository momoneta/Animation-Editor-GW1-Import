namespace AssetBankPlugin.Export;

public abstract class IAnimationExporter
{
	public abstract void Export(InternalAnimation animation, InternalSkeleton skeleton, string path);
}
