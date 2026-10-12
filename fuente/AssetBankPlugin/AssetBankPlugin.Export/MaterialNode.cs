namespace AssetBankPlugin.Export;

public class MaterialNode : CastNode
{
	public string Name => GetPropertyVal<string>("n");

	public string MaterialType => GetPropertyVal<string>("t");

	public MaterialNode()
		: base(1819566445u)
	{
	}

	public void SetName(string name)
	{
		SetPropertyVal("n", "s", name);
	}

	public void SetMaterialType(string type)
	{
		SetPropertyVal("t", "s", type);
	}

	public void SetSlot(string slot, ulong hash)
	{
		SetPropertyVal(slot, "l", hash);
	}

	public FileNode CreateFile()
	{
		return (FileNode)CreateChild(new FileNode());
	}
}
