using System.IO;
using System.Text;

namespace SELib;

internal class ExtendedBinaryWriter : BinaryWriter
{
	public ExtendedBinaryWriter(Stream stream)
		: base(stream)
	{
	}

	public void WriteNullTermString(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			value = string.Empty;
		}
		Write(Encoding.ASCII.GetBytes(value));
		Write((byte)0);
	}
}
