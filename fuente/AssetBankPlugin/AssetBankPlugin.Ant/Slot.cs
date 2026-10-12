namespace AssetBankPlugin.Ant;

public struct Slot
{
	public string Name;

	public BoneChannelType Type;

	public override string ToString()
	{
		return Name + ", " + Type;
	}
}
