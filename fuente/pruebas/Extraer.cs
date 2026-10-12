// Saca recursos de un juego con la biblioteca de Frosty, sin abrir el editor.
// Hay que lanzarlo con la carpeta de Frosty como directorio actual (ahí están Profiles\ y Caches\).
//   extraer.exe <perfil> <carpeta del juego> listar [filtro]
//   extraer.exe <perfil> <carpeta del juego> res <nombre> <salida>
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using FrostySdk;
using FrostySdk.Interfaces;
using FrostySdk.Managers;
using FrostySdk.Managers.Entries;

public static class Extraer
{
	private class Registro : ILogger
	{
		public void Log(string text, params object[] vars) { Console.Error.WriteLine("  [frosty] " + string.Format(text, vars)); }
		public void LogWarning(string text, params object[] vars) { Console.Error.WriteLine("  [aviso] " + string.Format(text, vars)); }
		public void LogError(string text, params object[] vars) { Console.Error.WriteLine("  [error] " + string.Format(text, vars)); }
	}

	public static int Main(string[] args)
	{
		string frosty = Directory.GetCurrentDirectory();
		AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
		{
			string n = new AssemblyName(e.Name).Name + ".dll";
			foreach (string d in new[] { frosty, Path.Combine(frosty, "ThirdParty"), Path.Combine(frosty, "Plugins") })
			{
				string p = Path.Combine(d, n);
				if (File.Exists(p)) return Assembly.LoadFrom(p);
			}
			return null;
		};
		return Run(args);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static int Run(string[] args)
	{
		ProfilesLibrary.Initialize(new List<Profile>());
		if (!ProfilesLibrary.HasProfile(args[0]))
		{
			Console.Error.WriteLine("perfil desconocido: " + args[0]);
			return 2;
		}
		ProfilesLibrary.Initialize(args[0]);
		Registro registro = new Registro();
		FileSystemManager fs = new FileSystemManager(args[1]);
		foreach (FileSystemSource s in ProfilesLibrary.Sources) fs.AddSource(s.Path, s.SubDirs);
		fs.Initialize();
		ResourceManager rm = new ResourceManager(fs);
		rm.SetLogger(registro);
		rm.Initialize();
		AssetManager am = new AssetManager(fs, rm);
		am.SetLogger(registro);
		am.Initialize(additionalStartup: false);
		Console.Error.WriteLine("perfil " + ProfilesLibrary.ProfileName + " version " + ProfilesLibrary.DataVersion);
		switch (args[2])
		{
		case "listar":
			foreach (ResAssetEntry e in am.EnumerateRes())
			{
				if (args.Length > 3 && e.Name.IndexOf(args[3], StringComparison.OrdinalIgnoreCase) < 0) continue;
				Console.WriteLine(e.Name + "\t" + e.ResType.ToString("x8") + "\t" + e.OriginalSize);
			}
			break;
		case "res":
			ResAssetEntry entrada = am.GetResEntry(args[3]);
			if (entrada == null)
			{
				Console.Error.WriteLine("RES no encontrado: " + args[3]);
				return 3;
			}
			using (Stream s = am.GetRes(entrada))
			using (FileStream f = File.Create(args[4]))
			{
				s.CopyTo(f);
				Console.WriteLine(f.Length + " bytes, tipo " + entrada.ResType.ToString("x8"));
			}
			break;
		}
		return 0;
	}
}
