namespace AssetBankPlugin.Formats.GenericData;

public class ReflLayout
{
	public int mMinSlot;

	public int mMaxSlot;

	public uint mDataSize;

	public uint mAlignment;

	public uint mStringTableOffset;

	public uint mStringTableLength;

	public bool mReordered;

	public bool mNative;

	public uint mHash;

	public ReflEntry[] mEntries;

	public string mName;

	public string[] mFieldNames;

	public override string ToString()
	{
		return $"Class \"{mName}\", Size {mDataSize}";
	}
}
