namespace AssetBankPlugin.Export;

public class CastProperty_t
{
	public int Size { get; set; }

	public string Format { get; set; }

	public string Identifier { get; set; }

	public int ArrayCount { get; set; }

	public CastProperty_t(string identifier)
	{
		Identifier = identifier;
		switch (identifier)
		{
		case "b":
			Size = 1;
			Format = "B";
			ArrayCount = 1;
			break;
		case "h":
			Size = 2;
			Format = "H";
			ArrayCount = 1;
			break;
		case "i":
			Size = 4;
			Format = "I";
			ArrayCount = 1;
			break;
		case "l":
			Size = 8;
			Format = "Q";
			ArrayCount = 1;
			break;
		case "f":
			Size = 4;
			Format = "f";
			ArrayCount = 1;
			break;
		case "d":
			Size = 8;
			Format = "d";
			ArrayCount = 1;
			break;
		case "s":
			Size = 0;
			Format = "s";
			ArrayCount = 1;
			break;
		case "2v":
			Size = 8;
			Format = "2f";
			ArrayCount = 2;
			break;
		case "3v":
			Size = 12;
			Format = "3f";
			ArrayCount = 3;
			break;
		case "4v":
			Size = 16;
			Format = "4f";
			ArrayCount = 4;
			break;
		default:
			Size = 0;
			Format = "";
			ArrayCount = 1;
			break;
		}
	}
}
