using FrostySdk.IO;

namespace AssetBankPlugin.Ant;

public sealed class LittleEndianBitConverter : EndianBitConverter
{
	public override Endian Endianness => Endian.Little;

	public override bool IsLittleEndian()
	{
		return true;
	}

	protected override void CopyBytesImpl(long p_Value, int p_Bytes, byte[] p_Buffer, int p_Index)
	{
		for (int i = 0; i < p_Bytes; i++)
		{
			p_Buffer[i + p_Index] = (byte)(p_Value & 0xFF);
			p_Value >>= 8;
		}
	}

	protected override long FromBytes(byte[] p_Buffer, int p_StartIndex, int p_BytesToConvert)
	{
		long num = 0L;
		for (int i = 0; i < p_BytesToConvert; i++)
		{
			num = (num << 8) | p_Buffer[p_StartIndex + p_BytesToConvert - 1 - i];
		}
		return num;
	}
}
