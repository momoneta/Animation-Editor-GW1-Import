using System;
using System.IO;
using FrostySdk.IO;

namespace AssetBankPlugin.Ant;

public class BitReader : NativeReader
{
	private readonly int _bitsPerSlice;

	private readonly int _bytesPerSlice;

	private readonly bool _shouldDispose;

	protected byte[] BitBuffer;

	protected int CurrentBitsLeft;

	public Endian Endianness { get; set; }

	protected bool Disposed { get; set; }

	public override long Position => (base.BaseStream.Position << 3) - (CurrentBitsLeft >> 3);

	public override long Length => base.BaseStream.Length << 3;

	private bool HasReadHigh { get; set; }

	private bool HasReadLow { get; set; }

	public BitReader(Stream stream, int bitCountPerSlice = 8, Endian endianness = Endian.Big, bool shouldDispose = false)
		: base(stream)
	{
		if (!stream.CanRead)
		{
			throw new ArgumentException("Stream isn't readable", "stream");
		}
		if ((bitCountPerSlice & 7) != 0)
		{
			throw new ArgumentException("BitReader does not support bit slices that are not 8 bit aligned");
		}
		_bitsPerSlice = bitCountPerSlice;
		_bytesPerSlice = bitCountPerSlice >> 3;
		BitBuffer = new byte[_bytesPerSlice];
		Endianness = endianness;
		_shouldDispose = shouldDispose;
	}

	public ulong ReadUIntLow(int p_BitCount)
	{
		ulong num = 0uL;
		for (int i = 0; i < p_BitCount; i++)
		{
			num = (num << 1) | (ulong)(ReadLowBit() ? 1 : 0);
		}
		return num;
	}

	public long ReadIntLow(int p_BitCount)
	{
		ulong num = ReadUIntLow(p_BitCount);
		if ((num & (ulong)(1L << p_BitCount - 1)) != 0L)
		{
			return (long)num | (-1L << p_BitCount);
		}
		return (long)num;
	}

	public bool ReadLowBit()
	{
		if (CurrentBitsLeft == 0)
		{
			UpdateBits();
		}
		if (HasReadHigh)
		{
			throw new Exception("Trying to read low bit after reading high bit. Will result in data loss. pLz fix!");
		}
		HasReadLow = true;
		int num = 1 << ((CurrentBitsLeft - _bitsPerSlice) & 7);
		bool result = (BitBuffer[CurrentBitsLeft - 1 >> 3] & num) != 0;
		CurrentBitsLeft--;
		return result;
	}

	public ulong ReadUIntHigh(int p_BitCount)
	{
		ulong num = 0uL;
		for (int i = 0; i < p_BitCount; i++)
		{
			num = (num << 1) | (ulong)(ReadHighBit() ? 1 : 0);
		}
		return num;
	}

	public long ReadIntHigh(int p_BitCount)
	{
		ulong num = ReadUIntHigh(p_BitCount);
		if ((num & (ulong)(1L << p_BitCount - 1)) != 0L)
		{
			return (long)num | (-1L << p_BitCount);
		}
		return (long)num;
	}

	public bool ReadHighBit()
	{
		if (CurrentBitsLeft == 0)
		{
			UpdateBits();
		}
		if (HasReadLow)
		{
			throw new Exception("Trying to read high bit after reading low bit. Will result in data loss. pLz fix!");
		}
		HasReadHigh = true;
		int num = CurrentBitsLeft - 1 >> 3;
		int num2 = _bytesPerSlice - num - 1;
		int num3 = 1 << CurrentBitsLeft - (num << 3) - 1;
		bool result = (BitBuffer[num2] & num3) != 0;
		CurrentBitsLeft--;
		return result;
	}

	private void UpdateBits()
	{
		if (Disposed)
		{
			throw new ObjectDisposedException("BitReader");
		}
		if (ReadInternal(BitBuffer, 0, _bytesPerSlice) == 0)
		{
			throw new Exception("End of stream bitreader");
		}
		if (_bytesPerSlice > 1 && Endianness != Endian.Big)
		{
			int num = 0;
			int num2 = _bytesPerSlice - 1;
			while (num < num2)
			{
				byte b = BitBuffer[num];
				BitBuffer[num] = BitBuffer[num2];
				BitBuffer[num2] = b;
				num++;
				num2--;
			}
		}
		CurrentBitsLeft = _bitsPerSlice;
	}

	public new void Dispose()
	{
		if (!Disposed)
		{
			base.Dispose();
			Disposed = true;
			if (_shouldDispose)
			{
				base.BaseStream?.Dispose();
			}
		}
	}

	public void Flush()
	{
		if (Disposed)
		{
			throw new ObjectDisposedException("BitReader");
		}
		base.BaseStream.Flush();
	}

	public override int Read(byte[] p_Buffer, int p_Offset, int p_Count)
	{
		throw new NotImplementedException();
	}

	public void SetLength(long p_Value)
	{
		if (Disposed)
		{
			throw new ObjectDisposedException("BitReader");
		}
		base.BaseStream.SetLength(p_Value);
	}

	protected int ReadInternal(byte[] p_Buffer, int p_Offset, int p_Count)
	{
		return base.BaseStream.Read(p_Buffer, p_Offset, p_Count);
	}
}
