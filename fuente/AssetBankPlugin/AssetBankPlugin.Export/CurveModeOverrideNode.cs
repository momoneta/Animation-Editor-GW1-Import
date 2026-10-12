namespace AssetBankPlugin.Export;

public class CurveModeOverrideNode : CastNode
{
	public string NodeName => GetPropertyVal<string>("nn");

	public string Mode => GetPropertyVal<string>("m");

	public bool OverrideTranslationCurves => GetPropertyVal("ot", (byte)0) >= 1;

	public bool OverrideRotationCurves => GetPropertyVal("or", (byte)0) >= 1;

	public bool OverrideScaleCurves => GetPropertyVal("os", (byte)0) >= 1;

	public CurveModeOverrideNode()
		: base(1448037699u)
	{
	}

	public void SetNodeName(string name)
	{
		SetPropertyVal("nn", "s", name);
	}

	public void SetMode(string mode)
	{
		SetPropertyVal("m", "s", mode);
	}

	public void SetOverrideTranslationCurves(bool enabled)
	{
		SetPropertyVal("ot", "b", enabled ? ((byte)1) : ((byte)0));
	}

	public void SetOverrideRotationCurves(bool enabled)
	{
		SetPropertyVal("or", "b", enabled ? ((byte)1) : ((byte)0));
	}

	public void SetOverrideScaleCurves(bool enabled)
	{
		SetPropertyVal("os", "b", enabled ? ((byte)1) : ((byte)0));
	}
}
