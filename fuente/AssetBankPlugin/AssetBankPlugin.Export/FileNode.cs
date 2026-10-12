namespace AssetBankPlugin.Export;

public class FileNode : CastNode
{
	public string Path => GetPropertyVal<string>("p");

	public FileNode()
		: base(1701603686u)
	{
	}

	public void SetPath(string path)
	{
		SetPropertyVal("p", "s", path);
	}
}
