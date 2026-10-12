namespace AssetBankPlugin.Export;

public class MetadataNode : CastNode
{
	public string Author => GetPropertyVal<string>("a");

	public string Software => GetPropertyVal<string>("s");

	public string UpAxis => GetPropertyVal<string>("up");

	public string SceneRoot => GetPropertyVal<string>("sr");

	public MetadataNode()
		: base(1635018093u)
	{
	}

	public void SetAuthor(string author)
	{
		SetPropertyVal("a", "s", author);
	}

	public void SetSoftware(string software)
	{
		SetPropertyVal("s", "s", software);
	}

	public void SetUpAxis(string up)
	{
		SetPropertyVal("up", "s", up);
	}

	public void SetSceneRoot(string root)
	{
		SetPropertyVal("sr", "s", root);
	}
}
