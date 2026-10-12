using System.IO;

namespace SELib;

internal class ExtendedBinaryReader : BinaryReader
{
	public ExtendedBinaryReader(Stream stream)
		: base(stream)
	{
	}

	public string ReadNullTermString()
	{
		string text = "";
		char c;
		while ((c = ReadChar()) != 0)
		{
			text += c;
		}
		return text;
	}
}
