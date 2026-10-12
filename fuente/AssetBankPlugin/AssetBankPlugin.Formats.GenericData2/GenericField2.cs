namespace AssetBankPlugin.Formats.GenericData2;

public class GenericField2
{
	public string Name;

	public string Type;

	public uint TypeHash;

	public int Offset;

	public object Data;

	public uint Size;

	public uint Alignment;

	public bool IsList;

	public int InlineCount;

	public uint ElementTypeHash;

	public uint ElementSize;

	public uint ElementAlignment;

	public bool IsNative;

	public bool IsArray
	{
		get
		{
			if (!IsList)
			{
				return InlineCount > 1;
			}
			return true;
		}
		set
		{
			IsList = value;
		}
	}

	public override string ToString()
	{
		string text = (IsList ? " (List)" : ((InlineCount > 1) ? $"[{InlineCount}]" : ""));
		return $"Field, \"{Name}\", {Type}{text}, Offset: {Offset}";
	}
}
