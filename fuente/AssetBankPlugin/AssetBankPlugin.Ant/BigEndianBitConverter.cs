using FrostySdk.IO;

namespace AssetBankPlugin.Ant;

public sealed class BigEndianBitConverter : EndianBitConverter
{
	public override Endian Endianness => Endian.Big;

	public override bool IsLittleEndian()
	{
		return false;
	}

	protected override void CopyBytesImpl(long p_Value, int p_Bytes, byte[] p_Buffer, int p_Index)
	{
		int num = p_Index + p_Bytes - 1;
		for (int i = 0; i < p_Bytes; i++)
		{
			p_Buffer[num - i] = (byte)(p_Value & 0xFF);
			p_Value >>= 8;
		}
	}

	protected override long FromBytes(byte[] p_Buffer, int p_StartIndex, int p_BytesToConvert)
	{
		long num = 0L;
		for (int i = 0; i < p_BytesToConvert; i++)
		{
			num = (num << 8) | p_Buffer[p_StartIndex + i];
		}
		return num;
	}
}
