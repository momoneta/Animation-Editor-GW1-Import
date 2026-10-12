using System.IO;
using FrostySdk.IO;

namespace AssetBankPlugin.Ant;

public class DofTable
{
	public class BitsPerComponent
	{
		public ushort Value { get; set; }

		public ushort BitsW => (ushort)(Value & 0xF);

		public ushort BitsZ => (ushort)((Value >> 4) & 0xF);

		public ushort BitsY => (ushort)((Value >> 8) & 0xF);

		public ushort BitsX => (ushort)((Value >> 12) & 0xF);

		public int BitSum => BitsX + BitsY + BitsZ + BitsW;

		public BitsPerComponent(ushort value)
		{
			Value = value;
		}

		public ushort SafeBitsW(ushort catchAllBitCount)
		{
			if (BitsW != 15)
			{
				return BitsW;
			}
			return catchAllBitCount;
		}

		public ushort SafeBitsZ(ushort catchAllBitCount)
		{
			if (BitsZ != 15)
			{
				return BitsZ;
			}
			return catchAllBitCount;
		}

		public ushort SafeBitsY(ushort catchAllBitCount)
		{
			if (BitsY != 15)
			{
				return BitsY;
			}
			return catchAllBitCount;
		}

		public ushort SafeBitsX(ushort catchAllBitCount)
		{
			if (BitsX != 15)
			{
				return BitsX;
			}
			return catchAllBitCount;
		}

		public int SafeSum(ushort catchAllBitCount)
		{
			return SafeBitsX(catchAllBitCount) + SafeBitsY(catchAllBitCount) + SafeBitsZ(catchAllBitCount) + SafeBitsW(catchAllBitCount);
		}
	}

	public short[] DeltaBase = new short[4];

	public BitsPerComponent[] BitsPerSubBlock = new BitsPerComponent[0];

	public ushort SubBlockCount { get; set; }

	public DofTable(ushort p_SubBlockCount)
	{
		SubBlockCount = p_SubBlockCount;
	}

	public DofTable(NativeReader r, ushort subBlockCount)
		: this(subBlockCount)
	{
		Deserialize(r);
	}

	public void Deserialize(NativeReader r)
	{
		DeltaBase = new short[4]
		{
			r.ReadShort(),
			r.ReadShort(),
			r.ReadShort(),
			r.ReadShort()
		};
		BitsPerSubBlock = new BitsPerComponent[SubBlockCount];
		for (int i = 0; i < SubBlockCount; i++)
		{
			BitsPerSubBlock[i] = new BitsPerComponent(r.ReadUShort());
		}
	}

	public void Deserialize(byte[] data)
	{
		using NativeReader r = new NativeReader(new MemoryStream(data));
		Deserialize(r);
	}
}
