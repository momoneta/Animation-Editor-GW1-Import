using System;
using System.IO;
using FrostySdk.IO;

namespace AssetBankPlugin.Ant;

public abstract class EndianBinaryReader : Stream, IDisposable
{
	protected bool m_Disposed;

	protected readonly byte[] m_Buffer = new byte[16];

	protected bool m_ShouldDispose;

	public override long Position
	{
		get
		{
			return BaseStream.Position;
		}
		set
		{
			throw new NotSupportedException();
		}
	}

	public override long Length => BaseStream.Length;

	public EndianBitConverter BitConverter { get; private set; }

	public Stream BaseStream { get; }

	public Endian Endianness
	{
		get
		{
			return BitConverter.Endianness;
		}
		set
		{
			if (value == Endian.Big)
			{
				BitConverter = EndianBitConverter.Big;
			}
			else
			{
				BitConverter = EndianBitConverter.Little;
			}
		}
	}

	public override bool CanRead => BaseStream.CanRead;

	public override bool CanSeek => BaseStream.CanSeek;

	public override bool CanWrite => BaseStream.CanWrite;

	protected EndianBinaryReader(EndianBitConverter p_BitConverter, Stream p_Stream, bool p_ShouldDispose)
	{
		if (!p_Stream.CanRead)
		{
			throw new ArgumentException("Stream isn't readable", "p_Stream");
		}
		m_ShouldDispose = p_ShouldDispose;
		BaseStream = p_Stream;
		BitConverter = p_BitConverter;
	}

	public override int ReadByte()
	{
		ReadInternal(m_Buffer, 0, 1);
		return m_Buffer[0];
	}

	public byte ReadUByte()
	{
		ReadInternal(m_Buffer, 0, 1);
		return m_Buffer[0];
	}

	public sbyte ReadSByte()
	{
		ReadInternal(m_Buffer, 0, 1);
		return (sbyte)m_Buffer[0];
	}

	public bool ReadBool()
	{
		ReadInternal(m_Buffer, 0, 1);
		return BitConverter.ToBoolean(m_Buffer, 0);
	}

	public short ReadInt16()
	{
		ReadInternal(m_Buffer, 0, 2);
		return BitConverter.ToInt16(m_Buffer, 0);
	}

	public int ReadInt32()
	{
		ReadInternal(m_Buffer, 0, 4);
		return BitConverter.ToInt32(m_Buffer, 0);
	}

	public long ReadInt64()
	{
		ReadInternal(m_Buffer, 0, 8);
		return BitConverter.ToInt64(m_Buffer, 0);
	}

	public ushort ReadUInt16()
	{
		ReadInternal(m_Buffer, 0, 2);
		return BitConverter.ToUInt16(m_Buffer, 0);
	}

	public uint ReadUInt32()
	{
		ReadInternal(m_Buffer, 0, 4);
		return BitConverter.ToUInt32(m_Buffer, 0);
	}

	public ulong ReadUInt64()
	{
		ReadInternal(m_Buffer, 0, 8);
		return BitConverter.ToUInt64(m_Buffer, 0);
	}

	public float ReadSingle()
	{
		ReadInternal(m_Buffer, 0, 4);
		return BitConverter.ToSingle(m_Buffer, 0);
	}

	public double ReadDouble()
	{
		ReadInternal(m_Buffer, 0, 8);
		return BitConverter.ToDouble(m_Buffer, 0);
	}

	public decimal ReadDecimal()
	{
		ReadInternal(m_Buffer, 0, 16);
		return BitConverter.ToDecimal(m_Buffer, 0);
	}

	public virtual int ReadBytes(byte[] p_Data, int p_Offset, int p_Count)
	{
		CheckDisposed();
		if (p_Offset < 0)
		{
			throw new ArgumentOutOfRangeException("p_Offset");
		}
		if (p_Count < 0)
		{
			throw new ArgumentOutOfRangeException("p_Count");
		}
		if (p_Count + p_Offset > p_Data.Length)
		{
			throw new ArgumentException("Not enough space in buffer for specified number of bytes starting at specified offset.");
		}
		return ReadInternal(p_Data, p_Offset, p_Count);
	}

	public virtual byte[] ReadBytes(int p_Count)
	{
		CheckDisposed();
		if (p_Count < 0)
		{
			throw new ArgumentOutOfRangeException("p_Count");
		}
		byte[] array = new byte[p_Count];
		int num = ReadInternal(array, 0, p_Count);
		if (num != p_Count)
		{
			throw new EndOfStreamException(string.Format("End of stream reached with {0} byte{1} left to read.", p_Count - num, (p_Count - num == 1) ? "s" : ""));
		}
		return array;
	}

	public new virtual void Dispose()
	{
		base.Dispose();
		CheckDisposed();
		m_Disposed = true;
		if (m_ShouldDispose)
		{
			BaseStream.Dispose();
		}
	}

	public override void Flush()
	{
		CheckDisposed();
		BaseStream.Flush();
	}

	public override long Seek(long p_Offset, SeekOrigin p_Origin)
	{
		CheckDisposed();
		return BaseStream.Seek(p_Offset, p_Origin);
	}

	public override int Read(byte[] p_Buffer, int p_Offset, int p_Count)
	{
		return ReadInternal(p_Buffer, p_Offset, p_Count);
	}

	public override void SetLength(long p_Value)
	{
		CheckDisposed();
		BaseStream.SetLength(p_Value);
	}

	public override void Write(byte[] p_Buffer, int p_Offset, int p_Count)
	{
		CheckDisposed();
		BaseStream.Write(p_Buffer, p_Offset, p_Count);
	}

	protected void CheckDisposed()
	{
		if (m_Disposed)
		{
			throw new ObjectDisposedException("EndianBinaryReader");
		}
	}

	protected virtual int ReadInternal(byte[] p_Buffer, int p_Offset, int p_Count)
	{
		CheckDisposed();
		return BaseStream.Read(p_Buffer, p_Offset, p_Count);
	}
}
