using AssetBankPlugin.Enums;

namespace AssetBankPlugin.Formats.GenericData;

public class ReflEntry
{
	public uint mLayoutHash;

	public uint mElementSize;

	public uint mOffset;

	public uint mName;

	public ushort mCount;

	public EFlags mFlags;

	public ushort mElementAlign;

	public short mRLE;

	public long mLayout;
}
