using System.Collections.Generic;

namespace AssetBankPlugin.Export;

public class AnimationNode : CastNode
{
	public string Name => GetPropertyVal<string>("n");

	public SkeletonNode Skeleton => ChildOfType<SkeletonNode>();

	public List<CurveNode> Curves => ChildrenOfType<CurveNode>();

	public List<CurveModeOverrideNode> CurveModeOverrides => ChildrenOfType<CurveModeOverrideNode>();

	public List<NotificationTrackNode> Notifications => ChildrenOfType<NotificationTrackNode>();

	public float Framerate => GetPropertyVal("fr", 0f);

	public bool Looping => GetPropertyVal("lo", (byte)0) >= 1;

	public AnimationNode()
		: base(1835626081u)
	{
	}

	public void SetName(string name)
	{
		SetPropertyVal("n", "s", name);
	}

	public SkeletonNode CreateSkeleton()
	{
		return (SkeletonNode)CreateChild(new SkeletonNode());
	}

	public CurveNode CreateCurve()
	{
		return (CurveNode)CreateChild(new CurveNode());
	}

	public CurveModeOverrideNode CreateCurveModeOverride()
	{
		return (CurveModeOverrideNode)CreateChild(new CurveModeOverrideNode());
	}

	public NotificationTrackNode CreateNotification()
	{
		return (NotificationTrackNode)CreateChild(new NotificationTrackNode());
	}

	public void SetFramerate(float framerate)
	{
		SetPropertyVal("fr", "f", framerate);
	}

	public void SetLooping(bool enabled)
	{
		SetPropertyVal("lo", "b", enabled ? ((byte)1) : ((byte)0));
	}
}
