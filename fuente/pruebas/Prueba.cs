// Banco de pruebas sin Frosty para el soporte de bancos antiguos (GD.DATA) de AssetBankPlugin.
//   prueba.exe exacto <banco>...      reserializa cada sección sin cambios y compara byte a byte
//   prueba.exe ida    <banco>...      descomprime cada animación, la recomprime, la inserta y compara
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using AssetBankPlugin.Ant;
using AssetBankPlugin.Ant.VBR;
using AssetBankPlugin.Formats;
using AssetBankPlugin.Formats.GenericData;
using AssetBankPlugin.Import;
using AssetBankPlugin.Import.Gw;
using FrostySdk.IO;

public static class Prueba
{
	// carpeta de Frosty contra la que se prueba (PRUEBA_FROSTY para la 1.0.6.3)
	private static readonly string CarpetaFrosty = Environment.GetEnvironmentVariable("PRUEBA_FROSTY") ?? @"C:\Frosty";

	private static readonly string[] Dirs = { CarpetaFrosty, Path.Combine(CarpetaFrosty, "ThirdParty"), Path.Combine(CarpetaFrosty, "Plugins") };

	public static int Main(string[] args)
	{
		// AssimpNet carga assimp.dll (nativo) por nombre: tiene que estar en la ruta de búsqueda
		Environment.SetEnvironmentVariable("PATH", CarpetaFrosty + ";" + Environment.GetEnvironmentVariable("PATH"));
		AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
		{
			string n = new AssemblyName(e.Name).Name + ".dll";
			string local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, n);
			if (File.Exists(local)) return Assembly.LoadFrom(local);
			foreach (string d in Dirs)
			{
				string p = Path.Combine(d, n);
				if (File.Exists(p)) return Assembly.LoadFrom(p);
			}
			return null;
		};
		return Run(args);
	}

	/// prueba.exe diferencia <original> <modificado>: para las animaciones de PRUEBA_SOLO, cuánto se
	/// mueve cada versión y cuánto se separa la modificada de la original en los primeros fotogramas.
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static int Diferencia(string original, string modificado)
	{
		FrostySdk.ProfilesLibrary.Initialize(new List<FrostySdk.Profile>());
		FrostySdk.ProfilesLibrary.Initialize(Environment.GetEnvironmentVariable("PRUEBA_PERFIL") ?? "PVZ.Main_Win64_Retail");
		Frosty.Core.App.Logger = new Registro();
		string solo = Environment.GetEnvironmentVariable("PRUEBA_SOLO") ?? "";
		Dictionary<string, AssetBankPlugin.Export.InternalAnimation>[] v = new Dictionary<string, AssetBankPlugin.Export.InternalAnimation>[2];
		for (int k = 0; k < 2; k++)
		{
			AntRefTable.Clear();
			using (NativeReader r = new NativeReader(new MemoryStream(File.ReadAllBytes(k == 0 ? original : modificado))))
			{
				new Bank(r, 0);
			}
			v[k] = new Dictionary<string, AssetBankPlugin.Export.InternalAnimation>();
			foreach (VbrAnimationAsset a in AntRefTable.Refs.Values.OfType<VbrAnimationAsset>().ToList())
			{
				if (a.Name.IndexOf(solo, StringComparison.OrdinalIgnoreCase) < 0) continue;
				a.Channels = a.GetChannels(a.ChannelToDofAsset);
				v[k][a.Name] = a.ConvertToInternal();
			}
		}
		foreach (string nombre in v[0].Keys)
		{
			AssetBankPlugin.Export.InternalAnimation o = v[0][nombre], n;
			if (!v[1].TryGetValue(nombre, out n) || o == null || n == null) continue;
			Func<Quaternion, Quaternion, double> dq = (x, y) => Math.Min(
				Math.Max(Math.Max(Math.Abs(x.X - y.X), Math.Abs(x.Y - y.Y)), Math.Max(Math.Abs(x.Z - y.Z), Math.Abs(x.W - y.W))),
				Math.Max(Math.Max(Math.Abs(x.X + y.X), Math.Abs(x.Y + y.Y)), Math.Max(Math.Abs(x.Z + y.Z), Math.Abs(x.W + y.W))));
			Func<AssetBankPlugin.Export.InternalAnimation, int, double> movimiento = (an, hasta) =>
			{
				double mx = 0;
				for (int f = 1; f < Math.Min(hasta, an.Frames.Count); f++)
					for (int i = 0; i < an.Frames[f].Rotations.Count; i++) mx = Math.Max(mx, dq(an.Frames[f].Rotations[i], an.Frames[0].Rotations[i]));
				return mx;
			};
			int comunes = Math.Min(o.Frames.Count, n.Frames.Count);
			double sep = 0;
			int huesosDistintos = 0;
			for (int i = 0; i < Math.Min(o.RotationChannels.Count, n.RotationChannels.Count); i++)
			{
				double e = 0;
				for (int f = 0; f < comunes; f++) e = Math.Max(e, dq(o.Frames[f].Rotations[i], n.Frames[f].Rotations[i]));
				if (e > 0.05) huesosDistintos++;
				sep = Math.Max(sep, e);
			}
			Console.WriteLine(nombre + ": original " + o.Frames.Count + " fotogramas, modificada " + n.Frames.Count
				+ " | movimiento (rot. max. respecto al fotograma 0): original " + movimiento(o, int.MaxValue).ToString("0.###")
				+ ", modificada en sus primeros " + comunes + " " + movimiento(n, comunes).ToString("0.###") + ", modificada entera " + movimiento(n, int.MaxValue).ToString("0.###")
				+ " | separacion entre ambas en los primeros " + comunes + ": " + sep.ToString("0.###") + " (" + huesosDistintos + " de " + o.RotationChannels.Count + " huesos distintos)");
		}
		return 0;
	}

	/// prueba.exe listar <banco> [filtro]: animaciones del banco con sus claves y los clips que las reproducen.
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static int Listar(string ruta, string filtro)
	{
		AntRefTable.Clear();
		using (NativeReader r = new NativeReader(new MemoryStream(File.ReadAllBytes(ruta))))
		{
			new Bank(r, 0);
		}
		Dictionary<Guid, List<ClipControllerAsset>> clips = new Dictionary<Guid, List<ClipControllerAsset>>();
		foreach (ClipControllerAsset c in AntRefTable.Refs.Values.OfType<ClipControllerAsset>())
		{
			List<Guid> ids = new List<Guid> { c.Anim };
			if (c.Anims != null) ids.AddRange(c.Anims);
			foreach (Guid id in ids.Where(x => x != Guid.Empty).Distinct())
			{
				List<ClipControllerAsset> l;
				if (!clips.TryGetValue(id, out l)) clips[id] = l = new List<ClipControllerAsset>();
				l.Add(c);
			}
		}
		foreach (VbrAnimationAsset a in AntRefTable.Refs.Values.OfType<VbrAnimationAsset>().OrderBy(x => x.Name))
		{
			List<ClipControllerAsset> l;
			clips.TryGetValue(a.ID, out l);
			string nombres = l == null ? "" : string.Join(", ", l.Select(c => c.Name + " (fps " + c.FPS.ToString("0.#") + ", ticks " + c.NumTicks.ToString("0.#") + ")"));
			if (filtro.Length > 0 && a.Name.IndexOf(filtro, StringComparison.OrdinalIgnoreCase) < 0 && nombres.IndexOf(filtro, StringComparison.OrdinalIgnoreCase) < 0) continue;
			Console.WriteLine(a.Name + "\t" + a.NumKeys + " claves\tq " + a.QuaternionCount + "+" + a.ConstQuaternionCount + " v " + a.Vector3Count + "+" + a.ConstVector3Count
				+ " f " + a.NumFloat + "+" + a.ConstFloatCount + "\tflags 0x" + a.Flags.ToString("X") + "\t" + a.Data.Length + " bytes\t" + nombres);
		}
		return 0;
	}

	/// Carga un banco de nivel (los que traen también los canales) y devuelve la animación pedida ya decodificada.
	private static AssetBankPlugin.Export.InternalAnimation Decodificar(string ruta, string nombre, out VbrAnimationAsset anim, out Bank banco, out byte[] bytes)
	{
		FrostySdk.ProfilesLibrary.Initialize(new List<FrostySdk.Profile>());
		FrostySdk.ProfilesLibrary.Initialize(Environment.GetEnvironmentVariable("PRUEBA_PERFIL") ?? "PVZ.Main_Win64_Retail");
		Frosty.Core.App.Logger = new Registro();
		AntRefTable.Clear();
		bytes = File.ReadAllBytes(ruta);
		using (NativeReader r = new NativeReader(new MemoryStream(bytes)))
		{
			banco = new Bank(r, 0);
		}
		anim = AntRefTable.Refs.Values.OfType<VbrAnimationAsset>().FirstOrDefault(a => a.Name == nombre)
			?? AntRefTable.Refs.Values.OfType<VbrAnimationAsset>().FirstOrDefault(a => string.Equals(a.Name, nombre + " Anim", StringComparison.OrdinalIgnoreCase));
		if (anim == null) return null;
		anim.Channels = anim.GetChannels(anim.ChannelToDofAsset);
		return anim.ConvertToInternal();
	}

	/// prueba.exe canales <banco> <animacion>: huesos de la animación, su valor inicial y cuánto se mueven.
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static int CanalesDe(string ruta, string nombre)
	{
		VbrAnimationAsset a;
		Bank banco;
		byte[] bytes;
		AssetBankPlugin.Export.InternalAnimation ia = Decodificar(ruta, nombre, out a, out banco, out bytes);
		if (ia == null) { Console.WriteLine("no existe o no se puede decodificar '" + nombre + "'"); return 1; }
		Console.WriteLine(a.Name + ": " + ia.Frames.Count + " fotogramas, fps " + a.FPS + ", " + ia.RotationChannels.Count + " rotaciones, "
			+ ia.PositionChannels.Count + " posiciones, " + ia.ScaleChannels.Count + " escalas");
		for (int i = 0; i < ia.RotationChannels.Count; i++)
		{
			Quaternion q0 = ia.Frames[0].Rotations[i];
			double mov = 0;
			foreach (AssetBankPlugin.Export.Frame f in ia.Frames) mov = Math.Max(mov, 1 - Math.Abs(Quaternion.Dot(q0, f.Rotations[i])));
			Console.WriteLine("  q " + ia.RotationChannels[i].PadRight(34) + " " + q0.X.ToString("0.000") + " " + q0.Y.ToString("0.000") + " " + q0.Z.ToString("0.000") + " " + q0.W.ToString("0.000") + "  mov " + mov.ToString("0.0000"));
		}
		for (int i = 0; i < ia.PositionChannels.Count; i++)
		{
			Vector3 v0 = ia.Frames[0].Positions[i];
			double mov = 0;
			foreach (AssetBankPlugin.Export.Frame f in ia.Frames) mov = Math.Max(mov, (f.Positions[i] - v0).Length());
			Console.WriteLine("  t " + ia.PositionChannels[i].PadRight(34) + " " + v0.X.ToString("0.000") + " " + v0.Y.ToString("0.000") + " " + v0.Z.ToString("0.000") + "  mov " + mov.ToString("0.0000"));
		}
		return 0;
	}

	/// prueba.exe gltf <banco> <animacion> <salida.gltf> [girar:<hueso>:<eje x|y|z>:<vueltas>] [subir:<hueso>:<eje>:<metros>]
	/// Escribe la animación como glTF (un nodo por hueso, valores locales tal cual), opcionalmente
	/// deformada para que el cambio se reconozca a simple vista dentro del juego.
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static int Gltf(string ruta, string nombre, string salida, string[] cambios)
	{
		VbrAnimationAsset a;
		Bank banco;
		byte[] bytes;
		AssetBankPlugin.Export.InternalAnimation ia = Decodificar(ruta, nombre, out a, out banco, out bytes);
		if (ia == null) { Console.WriteLine("no existe o no se puede decodificar '" + nombre + "'"); return 1; }
		int n = ia.Frames.Count;
		float fps = a.FPS > 0f ? a.FPS : 30f;
		foreach (string cambio in cambios)
		{
			string[] p = cambio.Split(':');
			Vector3 eje = p[2] == "x" ? Vector3.UnitX : (p[2] == "y" ? Vector3.UnitY : Vector3.UnitZ);
			float cantidad = float.Parse(p[3], System.Globalization.CultureInfo.InvariantCulture);
			if (p[0] == "girar")
			{
				int i = ia.RotationChannels.IndexOf(p[1]);
				if (i < 0) { Console.WriteLine("no hay rotacion para el hueso '" + p[1] + "'"); return 1; }
				for (int f = 0; f < n; f++)
				{
					// giro añadido en el espacio del padre: da vueltas completas a lo largo de la animación
					Quaternion giro = Quaternion.CreateFromAxisAngle(eje, (float)(2 * Math.PI * cantidad * f / (n - 1)));
					ia.Frames[f].Rotations[i] = Quaternion.Normalize(giro * ia.Frames[f].Rotations[i]);
				}
			}
			else if (p[0] == "subir")
			{
				int i = ia.PositionChannels.IndexOf(p[1]);
				if (i < 0) { Console.WriteLine("no hay posicion para el hueso '" + p[1] + "'"); return 1; }
				for (int f = 0; f < n; f++)
				{
					ia.Frames[f].Positions[i] += eje * (float)(cantidad * Math.Sin(Math.PI * f / (n - 1)));
				}
			}
		}
		List<string> huesos = ia.RotationChannels.Concat(ia.PositionChannels).Concat(ia.ScaleChannels).Distinct().ToList();
		MemoryStream bin = new MemoryStream();
		BinaryWriter w = new BinaryWriter(bin);
		System.Text.StringBuilder accesores = new System.Text.StringBuilder(), vistas = new System.Text.StringBuilder(), muestreadores = new System.Text.StringBuilder(), canales = new System.Text.StringBuilder(), nodos = new System.Text.StringBuilder();
		System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;
		int nAcc = 0, nMuestreador = 0;
		Func<int, string, string, int> accesor = (bytesVista, tipo, extra) =>
		{
			int inicio = (int)bin.Length - bytesVista;
			if (nAcc > 0) { accesores.Append(","); vistas.Append(","); }
			vistas.Append("{\"buffer\":0,\"byteOffset\":" + inicio + ",\"byteLength\":" + bytesVista + "}");
			accesores.Append("{\"bufferView\":" + nAcc + ",\"componentType\":5126,\"count\":" + n + ",\"type\":\"" + tipo + "\"" + extra + "}");
			return nAcc++;
		};
		for (int f = 0; f < n; f++) w.Write(f / fps);
		int tiempos = accesor(n * 4, "SCALAR", ",\"min\":[0],\"max\":[" + ((n - 1) / fps).ToString("R", inv) + "]");
		Action<int, string, int> canal = (nodo, camino, salidaAcc) =>
		{
			if (nMuestreador > 0) { muestreadores.Append(","); canales.Append(","); }
			muestreadores.Append("{\"input\":" + tiempos + ",\"output\":" + salidaAcc + ",\"interpolation\":\"LINEAR\"}");
			canales.Append("{\"sampler\":" + nMuestreador + ",\"target\":{\"node\":" + nodo + ",\"path\":\"" + camino + "\"}}");
			nMuestreador++;
		};
		for (int h = 0; h < huesos.Count; h++)
		{
			string hueso = huesos[h];
			int iq = ia.RotationChannels.IndexOf(hueso), it = ia.PositionChannels.IndexOf(hueso), ie = ia.ScaleChannels.IndexOf(hueso);
			if (h > 0) nodos.Append(",");
			nodos.Append("{\"name\":\"" + hueso + "\"");
			if (iq >= 0)
			{
				Quaternion q = ia.Frames[0].Rotations[iq];
				nodos.Append(",\"rotation\":[" + q.X.ToString("R", inv) + "," + q.Y.ToString("R", inv) + "," + q.Z.ToString("R", inv) + "," + q.W.ToString("R", inv) + "]");
				for (int f = 0; f < n; f++) { q = ia.Frames[f].Rotations[iq]; w.Write(q.X); w.Write(q.Y); w.Write(q.Z); w.Write(q.W); }
				canal(h, "rotation", accesor(n * 16, "VEC4", ""));
			}
			if (it >= 0)
			{
				Vector3 v = ia.Frames[0].Positions[it];
				nodos.Append(",\"translation\":[" + v.X.ToString("R", inv) + "," + v.Y.ToString("R", inv) + "," + v.Z.ToString("R", inv) + "]");
				for (int f = 0; f < n; f++) { v = ia.Frames[f].Positions[it]; w.Write(v.X); w.Write(v.Y); w.Write(v.Z); }
				canal(h, "translation", accesor(n * 12, "VEC3", ""));
			}
			if (ie >= 0)
			{
				Vector3 v = ia.Frames[0].Scales[ie];
				nodos.Append(",\"scale\":[" + v.X.ToString("R", inv) + "," + v.Y.ToString("R", inv) + "," + v.Z.ToString("R", inv) + "]");
				for (int f = 0; f < n; f++) { v = ia.Frames[f].Scales[ie]; w.Write(v.X); w.Write(v.Y); w.Write(v.Z); }
				canal(h, "scale", accesor(n * 12, "VEC3", ""));
			}
			nodos.Append("}");
		}
		w.Flush();
		string json = "{\"asset\":{\"version\":\"2.0\",\"generator\":\"prueba.exe\"},\"scene\":0,\"scenes\":[{\"nodes\":[" + string.Join(",", Enumerable.Range(0, huesos.Count)) + "]}],"
			+ "\"nodes\":[" + nodos + "],\"animations\":[{\"name\":\"" + nombre + "\",\"samplers\":[" + muestreadores + "],\"channels\":[" + canales + "]}],"
			+ "\"accessors\":[" + accesores + "],\"bufferViews\":[" + vistas + "],"
			+ "\"buffers\":[{\"byteLength\":" + bin.Length + ",\"uri\":\"data:application/octet-stream;base64," + Convert.ToBase64String(bin.ToArray()) + "\"}]}";
		File.WriteAllText(salida, json);
		Console.WriteLine(salida + ": " + huesos.Count + " huesos, " + n + " fotogramas a " + fps + " fps, " + new FileInfo(salida).Length + " bytes");
		return 0;
	}

	/// prueba.exe importar <banco> <animacion> <archivo fbx/gltf/dae> <banco de salida>
	/// Hace lo mismo que "Import Animation" del editor, leyendo el archivo con Assimp, y compara lo
	/// que queda en el banco con lo que traía el archivo.
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static int Importar(string ruta, string nombre, string archivo, string salida)
	{
		VbrAnimationAsset a;
		Bank banco;
		byte[] bytes;
		AssetBankPlugin.Export.InternalAnimation antes = Decodificar(ruta, nombre, out a, out banco, out bytes);
		if (antes == null) { Console.WriteLine("no existe o no se puede decodificar '" + nombre + "'"); return 1; }
		Registro.Detallado = Environment.GetEnvironmentVariable("PRUEBA_LOG") == "1";
		Assimp.Scene escena;
		using (Assimp.AssimpContext ctx = new Assimp.AssimpContext())
		{
			escena = ctx.ImportFile(archivo, Assimp.PostProcessSteps.None);
		}
		Console.WriteLine("  escena: " + escena.AnimationCount + " animaciones, " + escena.MeshCount + " mallas, raiz con " + (escena.RootNode == null ? 0 : escena.RootNode.ChildCount) + " hijos");
		if (!escena.HasAnimations) { Console.WriteLine("  el archivo no trae animaciones"); return 1; }
		Assimp.Animation an = escena.Animations[0];
		Console.WriteLine("  archivo: " + an.NodeAnimationChannelCount + " huesos animados, duracion " + (an.DurationInTicks / an.TicksPerSecond).ToString("0.###") + " s");
		byte[] nuevo = AnimationImporter.ImportLegacy(escena, bytes, a, banco.Classes, 0f);
		File.WriteAllBytes(salida, nuevo);
		Console.WriteLine("  banco: " + bytes.Length + " -> " + nuevo.Length + " bytes; '" + a.Name + "': " + antes.Frames.Count + " -> " + a.NumKeys + " claves");
		// releer desde cero y comparar con el archivo, hueso a hueso
		VbrAnimationAsset b;
		AssetBankPlugin.Export.InternalAnimation despues = Decodificar(salida, a.Name, out b, out banco, out bytes);
		if (despues == null) { Console.WriteLine("  NO SE PUEDE RELEER"); return 1; }
		double peorQ = 0, peorV = 0, cambio = 0, cambioPos = 0;
		int comparados = 0;
		// cuánto se separa el resultado de la animación original, hueso a hueso (posiciones incluidas:
		// un archivo en centímetros o con otros ejes se delata aquí)
		string peorHueso = "";
		for (int i = 0; i < Math.Min(antes.PositionChannels.Count, despues.PositionChannels.Count); i++)
			for (int f = 0; f < Math.Min(antes.Frames.Count, despues.Frames.Count); f++)
			{
				double d = (antes.Frames[f].Positions[i] - despues.Frames[f].Positions[i]).Length();
				if (d > cambioPos) { cambioPos = d; peorHueso = despues.PositionChannels[i]; }
			}
		foreach (Assimp.NodeAnimationChannel c in an.NodeAnimationChannels)
		{
			int iq = despues.RotationChannels.IndexOf(c.NodeName);
			if (iq < 0 || c.RotationKeyCount != despues.Frames.Count) continue;
			comparados++;
			for (int f = 0; f < despues.Frames.Count; f++)
			{
				Assimp.Quaternion k = c.RotationKeys[f].Value;
				Quaternion q = despues.Frames[f].Rotations[iq];
				peorQ = Math.Max(peorQ, 1 - Math.Abs(k.X * q.X + k.Y * q.Y + k.Z * q.Z + k.W * q.W));
				if (f < antes.Frames.Count) cambio = Math.Max(cambio, 1 - Math.Abs(Quaternion.Dot(antes.Frames[f].Rotations[iq], q)));
			}
			int it = despues.PositionChannels.IndexOf(c.NodeName);
			if (it < 0 || c.PositionKeyCount != despues.Frames.Count) continue;
			for (int f = 0; f < despues.Frames.Count; f++)
			{
				Assimp.Vector3D k = c.PositionKeys[f].Value;
				peorV = Math.Max(peorV, (despues.Frames[f].Positions[it] - new Vector3(k.X, k.Y, k.Z)).Length());
			}
		}
		Console.WriteLine("  releido: " + despues.Frames.Count + " fotogramas; " + comparados + " huesos comparados con el archivo, peor error rot " + peorQ.ToString("0.000000") + " pos " + peorV.ToString("0.000000")
			+ "; cambio maximo respecto a la animacion original: rot " + cambio.ToString("0.000") + ", pos " + cambioPos.ToString("0.0000") + (peorHueso.Length > 0 ? " (" + peorHueso + ")" : ""));
		// los huesos que más cambian, para confirmar que la edición hecha en el archivo es la que llega
		var porHueso = new List<KeyValuePair<string, double>>();
		for (int i = 0; i < Math.Min(antes.RotationChannels.Count, despues.RotationChannels.Count); i++)
		{
			double d = 0;
			for (int f = 0; f < Math.Min(antes.Frames.Count, despues.Frames.Count); f++) d = Math.Max(d, 1 - Math.Abs(Quaternion.Dot(antes.Frames[f].Rotations[i], despues.Frames[f].Rotations[i])));
			if (d > 0.001) porHueso.Add(new KeyValuePair<string, double>(despues.RotationChannels[i], d));
		}
		Console.WriteLine("  huesos cuya rotacion cambia: " + porHueso.Count + (porHueso.Count > 0 ? " -> " + string.Join(", ", porHueso.OrderByDescending(k => k.Value).Take(6).Select(k => k.Key + " " + k.Value.ToString("0.00"))) : ""));
		return peorQ < 0.001 && peorV < 0.01 ? 0 : 1;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static int Run(string[] args)
	{
		if (args[0] == "diferencia") return Diferencia(args[1], args[2]);
		if (args[0] == "listar") return Listar(args[1], args.Length > 2 ? args[2] : "");
		if (args[0] == "canales") return CanalesDe(args[1], args[2]);
		if (args[0] == "gltf") return Gltf(args[1], args[2], args[3], args.Skip(4).ToArray());
		if (args[0] == "importar") return Importar(args[1], args[2], args[3], args[4]);
		if (args[0] == "legacy")
		{
			// Reproduce lo que hace el plugin al importar en GW1: importa el .cast sobre la animación
			// indicada y comprueba si los clip controllers que la usan cambian de NumTicks.
			byte[] bancoBytes = File.ReadAllBytes(args[1]);
			string nombreAnim = args[2];
			string archivo = args[3];
			AntRefTable.Clear();
			Frosty.Core.App.Logger = new Registro();
			Bank banco;
			using (NativeReader lector = new NativeReader(new MemoryStream(bancoBytes)))
			{
				banco = new Bank(lector, 0);
			}
			VbrAnimationAsset destino = AntRefTable.Refs.Values.OfType<VbrAnimationAsset>()
				.FirstOrDefault(a => a.Name == nombreAnim);
			if (destino == null)
			{
				Console.WriteLine("  no existe '" + nombreAnim + "'");
				return 1;
			}
			destino.Channels = destino.GetChannels(destino.ChannelToDofAsset);
			AssetBankPlugin.Export.InternalAnimation ia = destino.ConvertToInternal();
			HashSet<string> omitir = new HashSet<string>(Environment.GetEnvironmentVariable("PRUEBA_OMITIR")?.Split(',') ?? new string[0]);
			Assimp.Scene escena = Escena(ia, 30f, omitir);
			Console.WriteLine("  importando '" + nombreAnim + "' (" + destino.NumKeys + " claves) con " + ia.Frames.Count + " fotogramas de la escena");
			byte[] salida = AnimationImporter.ImportLegacy(escena, bancoBytes, destino, banco.Classes, 0f);
			Console.WriteLine("  banco: " + bancoBytes.Length + " -> " + salida.Length + " bytes");
			Console.WriteLine("  destino ahora tiene " + destino.NumKeys + " claves");
			string[] despues = NumTicksDe(salida, banco.Classes, nombreAnim);
			Console.WriteLine("  clips de esta animación: " + (despues.Length == 0 ? "NINGUNO" : string.Join(", ", despues)));
			return 0;
		}
		if (args[0] == "clips")
		{
			byte[] bancoBytes = File.ReadAllBytes(args[1]);
			AntRefTable.Clear();
			Bank banco;
			using (NativeReader lector = new NativeReader(new MemoryStream(bancoBytes)))
			{
				banco = new Bank(lector, 0);
			}
			Console.WriteLine(Path.GetFileName(args[1]) + ": " + bancoBytes.Length + " bytes, " + banco.Classes.Count + " clases, antiguo=" + GwBankFile.IsLegacy(bancoBytes));
			return Clips(bancoBytes, banco, args[2]);
		}
		int fallos = 0;
		foreach (string ruta in args.Skip(1))
		{
			byte[] bytes = File.ReadAllBytes(ruta);
			AntRefTable.Clear();
			Bank bank;
			using (NativeReader r = new NativeReader(new MemoryStream(bytes)))
			{
				bank = new Bank(r, 0);
			}
			Console.WriteLine(Path.GetFileName(ruta) + ": " + bytes.Length + " bytes, " + bank.Classes.Count + " clases, antiguo=" + GwBankFile.IsLegacy(bytes));
			switch (args[0])
			{
			case "exacto":
				fallos += Exacto(bytes, bank);
				break;
			case "ida":
				fallos += IdaYVuelta(bytes, bank);
				break;
			case "escena":
				fallos += PorEscena(bytes, bank);
				break;
			case "clips":
				fallos += Clips(bytes, bank, args[2]);
				break;
			}
		}
		Console.WriteLine(fallos == 0 ? "TODO BIEN" : ("FALLOS: " + fallos));
		return fallos == 0 ? 0 : 1;
	}

	private static Guid GuidRaiz(byte[] bytes, GwBankFile.Section s, Dictionary<uint, GenericClass> clases, out GenericClass cls)
	{
		int b = s.Start + 16;
		uint tipo = (uint)GwBinary.ReadU64(bytes, b + 16, s.BigEndian);
		cls = null;
		if (!clases.TryGetValue(tipo, out cls)) return Guid.Empty;
		GenericField f = cls.Elements.FirstOrDefault(x => x.Name == "__guid");
		if (f.Name == null) return Guid.Empty;
		using (NativeReader r = new NativeReader(new MemoryStream(bytes)))
		{
			r.BaseStream.Position = b + GwBinary.ReadU16(bytes, b + 28, s.BigEndian) + f.Offset;
			return r.ReadGuid(s.BigEndian ? Endian.Big : Endian.Little);
		}
	}

	/// <summary>
	/// prueba.exe clips &lt;banco&gt; &lt;nombre de animacion&gt;: comprueba si RetimeClips encuentra los
	/// clip controllers que apuntan a esa animacion y les cambia NumTicks.
	/// </summary>
	private static int Clips(byte[] bytes, Bank bank, string wanted)
	{
		foreach (GwBankFile.Section s in GwBankFile.Sections(bytes))
		{
			if (s.Tag != "GD.DATA") continue;
			GenericClass cls;
			Guid id = GuidRaiz(bytes, s, bank.Classes, out cls);
			if (cls == null || cls.Name != "VbrAnimationAsset") continue;
			AntAsset asset;
			if (!AntRefTable.Refs.TryGetValue(id, out asset) || asset.Name != wanted) continue;
			VbrAnimationAsset anim = (VbrAnimationAsset)asset;
			Console.WriteLine("  '" + wanted + "': " + anim.NumKeys + " claves, seccion 0x" + s.Start.ToString("X"));
			byte[] copia = (byte[])bytes.Clone();
			string[] antes = NumTicksDe(copia, bank.Classes, wanted);
			GwClipTiming.Report informe = GwClipTiming.Retime(copia, bank.Classes, s.Start, anim.NumKeys + 7);
			int clips = informe.Clips, actors = informe.Actors;
			string[] despues = NumTicksDe(copia, bank.Classes, wanted);
			Console.WriteLine("    RetimeClips -> " + clips + " clip(s), " + actors + " actor(s)");
			Console.WriteLine("    NumTicks de los clips que la usan: " + string.Join(", ", antes));
			Console.WriteLine("    tras RetimeClips:                " + string.Join(", ", despues));
			return clips > 0 ? 0 : 1;
		}
		Console.WriteLine("  no se encuentra la animacion '" + wanted + "'");
		return 1;
	}

	/// <summary>NumTicks de todos los ClipControllerAsset que apuntan a la animacion indicada.</summary>
	private static string[] NumTicksDe(byte[] bytes, Dictionary<uint, GenericClass> clases, string wanted)
	{
		byte[] animGuid = null;
		foreach (GwBankFile.Section s in GwBankFile.Sections(bytes))
		{
			if (s.Tag != "GD.DATA") continue;
			GenericClass cls;
			Guid id = GuidRaiz(bytes, s, clases, out cls);
			if (cls == null || cls.Name != "VbrAnimationAsset") continue;
			AntAsset a;
			if (!AntRefTable.Refs.TryGetValue(id, out a) || a.Name != wanted) continue;
			GenericField f = cls.Elements.FirstOrDefault(x => x.Name == "__guid");
			if (f.Name == null) continue;
			int b0 = s.Start + 16 + GwBinary.ReadU16(bytes, s.Start + 16 + 28, s.BigEndian);
			animGuid = new byte[16];
			Buffer.BlockCopy(bytes, b0 + f.Offset, animGuid, 0, 16);
			break;
		}
		if (animGuid == null) return Array.Empty<string>();
		byte[] aguja = animGuid;
		List<string> list = new List<string>();
		foreach (GwBankFile.Section s in GwBankFile.Sections(bytes))
		{
			if (s.Tag != "GD.DATA") continue;
			int b = s.Start + 16;
			GenericClass cls;
			if (!clases.TryGetValue((uint)GwBinary.ReadU64(bytes, b + 16, s.BigEndian), out cls)) continue;
			if (cls.Name != "ClipControllerAsset") continue;
			GenericField fAnim = cls.Elements.FirstOrDefault(x => x.Name == "Anim");
			GenericField fTicks = cls.Elements.FirstOrDefault(x => x.Name == "NumTicks");
			GenericField fScale = cls.Elements.FirstOrDefault(x => x.Name == "FPSScale");
			if (fAnim.Name == null || fTicks.Name == null || fScale.Name == null) continue;
			int obj = b + GwBinary.ReadU16(bytes, b + 28, s.BigEndian);
			bool igual = true;
			for (int i = 0; i < 16; i++) if (bytes[obj + fAnim.Offset + i] != aguja[i]) { igual = false; break; }
			if (!igual) continue;
			int ticks = (int)ReadFloatBE(bytes, obj + fTicks.Offset);
			float escala = ReadFloatBE(bytes, obj + fScale.Offset);
			list.Add(ticks + "/" + escala.ToString("0.###"));
		}
		return list.ToArray();
	}

	private static float ReadFloatBE(byte[] b, int o)
	{
		return BitConverter.ToSingle(BitConverter.GetBytes(GwBinary.ReadU32(b, o, true)), 0);
	}

	private static int Exacto(byte[] bytes, Bank bank)
	{
		int ok = 0, mal = 0, saltadas = 0;
		Dictionary<string, int> porTipo = new Dictionary<string, int>();
		foreach (GwBankFile.Section s in GwBankFile.Sections(bytes))
		{
			if (s.Tag != "GD.DATA") continue;
			GenericClass cls;
			Guid id = GuidRaiz(bytes, s, bank.Classes, out cls);
			AntAsset asset;
			// el serializador solo se usa con animaciones; el resto de tipos no se toca al importar
			if (cls == null || cls.Name != "VbrAnimationAsset" || !AntRefTable.Refs.TryGetValue(id, out asset)) { saltadas++; continue; }
			byte[] nueva;
			try
			{
				nueva = GwSectionWriter.Write(bytes, s, bank.Classes, null).Section;
			}
			catch (NotSupportedException)
			{
				saltadas++;
				continue;
			}
			bool igual = nueva.Length == s.Size;
			int dif = -1;
			for (int i = 0; igual && i < nueva.Length; i++)
			{
				if (nueva[i] != bytes[s.Start + i]) { igual = false; dif = i; }
			}
			if (igual) ok++;
			else
			{
				mal++;
				int n;
				porTipo.TryGetValue(cls.Name, out n);
				porTipo[cls.Name] = n + 1;
				if (mal <= 5) Console.WriteLine("  DISTINTA " + cls.Name + " '" + asset.Name + "' en 0x" + s.Start.ToString("X") + ": tam " + s.Size + " -> " + nueva.Length + ", primer byte distinto " + dif);
			}
		}
		Console.WriteLine("  animaciones identicas " + ok + ", distintas " + mal + ", otras secciones " + saltadas);
		foreach (KeyValuePair<string, int> kv in porTipo) Console.WriteLine("    " + kv.Key + ": " + kv.Value);
		return mal;
	}

	/// Reparte los datos decodificados de una animación en canales completos, en el orden del códec.
	private static void Canales(VbrAnimationAsset a, out float[][][] quats, out float[][][] vecs, out float[][] floats)
	{
		List<Vector4> dec = a.Decompress();
		int tq = a.QuaternionCount + a.ConstQuaternionCount, tv = a.Vector3Count + a.ConstVector3Count, tf = a.NumFloat + a.ConstFloatCount;
		int total = tq + tv + tf, n = a.NumKeys;
		bool[] cte = new bool[total];
		if (a.ConstChanMap != null && a.ConstChanMap.Length > 0)
		{
			int c = 0;
			bool estado = false;
			foreach (byte tramo in a.ConstChanMap)
			{
				for (int i = 0; i < tramo && c < total; i++) cte[c++] = estado;
				estado = !estado;
			}
		}
		int porFotograma = a.QuaternionCount + a.Vector3Count + a.NumFloat;
		quats = new float[tq][][];
		vecs = new float[tv][][];
		floats = new float[tf][];
		int ia = 0, ip = 0;
		float qd = a.QuatMax - a.QuatMin, vd = a.Vec3Max - a.Vec3Min, fd = a.FloatMax - a.FloatMin;
		// primero se recorren los constantes para avanzar el índice de paleta en el orden del códec
		int[] paleta = new int[total];
		for (int c = 0; c < total; c++)
		{
			if (!cte[c]) continue;
			paleta[c] = ip;
			ip += c < tq ? 4 : (c < tq + tv ? 3 : 1);
		}
		for (int c = 0; c < total; c++)
		{
			int k = cte[c] ? -1 : ia++;
			if (c < tq)
			{
				quats[c] = new float[n][];
				for (int f = 0; f < n; f++)
				{
					if (cte[c])
					{
						quats[c][f] = new float[4];
						for (int j = 0; j < 4; j++) quats[c][f][j] = a.ConstantPalette[a.PaletteIndexes[paleta[c] + j]] * qd + a.QuatMin;
					}
					else
					{
						Vector4 v = dec[f * porFotograma + k];
						quats[c][f] = new[] { v.X, v.Y, v.Z, v.W };
					}
				}
			}
			else if (c < tq + tv)
			{
				int i = c - tq;
				vecs[i] = new float[n][];
				for (int f = 0; f < n; f++)
				{
					if (cte[c])
					{
						vecs[i][f] = new float[3];
						for (int j = 0; j < 3; j++) vecs[i][f][j] = a.ConstantPalette[a.PaletteIndexes[paleta[c] + j]] * vd + a.Vec3Min;
					}
					else
					{
						Vector4 v = dec[f * porFotograma + k];
						vecs[i][f] = new[] { v.X, v.Y, v.Z };
					}
				}
			}
			else
			{
				int i = c - tq - tv;
				floats[i] = new float[n];
				for (int f = 0; f < n; f++)
				{
					floats[i][f] = cte[c] ? a.ConstantPalette[a.PaletteIndexes[paleta[c]]] * fd + a.FloatMin : dec[f * porFotograma + k].X;
				}
			}
		}
	}

	private class Registro : FrostySdk.Interfaces.ILogger
	{
		public static bool Detallado;
		public void Log(string text, params object[] vars) { if (Detallado) Console.WriteLine("    [log] " + text); }
		public void LogWarning(string text, params object[] vars) { Console.WriteLine("    [aviso] " + text); }
		public void LogError(string text, params object[] vars) { Console.WriteLine("    [error] " + text); }
	}

	/// Construye una escena de Assimp con las curvas de una animación ya decodificada, como si viniera de un FBX.
	private static Assimp.Scene Escena(AssetBankPlugin.Export.InternalAnimation ia, float fps, HashSet<string> omitir)
	{
		Assimp.Scene escena = new Assimp.Scene();
		Assimp.Animation anim = new Assimp.Animation { Name = ia.Name ?? "anim", TicksPerSecond = fps, DurationInTicks = ia.Frames.Count - 1 };
		Dictionary<string, Assimp.NodeAnimationChannel> porHueso = new Dictionary<string, Assimp.NodeAnimationChannel>();
		Func<string, Assimp.NodeAnimationChannel> canal = nombre =>
		{
			Assimp.NodeAnimationChannel c;
			if (!porHueso.TryGetValue(nombre, out c))
			{
				c = new Assimp.NodeAnimationChannel { NodeName = nombre };
				porHueso[nombre] = c;
				anim.NodeAnimationChannels.Add(c);
			}
			return c;
		};
		// PRUEBA_ESTIRAR=n repite cada fotograma n veces: la animación importada dura n veces más
		int estirar = int.Parse(Environment.GetEnvironmentVariable("PRUEBA_ESTIRAR") ?? "1");
		anim.DurationInTicks = ia.Frames.Count * estirar - 1;
		for (int f = 0; f < ia.Frames.Count * estirar; f++)
		{
			AssetBankPlugin.Export.Frame fr = ia.Frames[f / estirar];
			for (int i = 0; i < ia.RotationChannels.Count && i < fr.Rotations.Count; i++)
			{
				if (omitir.Contains(ia.RotationChannels[i])) continue;
				Quaternion q = fr.Rotations[i];
				canal(ia.RotationChannels[i]).RotationKeys.Add(new Assimp.QuaternionKey(f, new Assimp.Quaternion(q.W, q.X, q.Y, q.Z)));
			}
			for (int i = 0; i < ia.PositionChannels.Count && i < fr.Positions.Count; i++)
			{
				if (omitir.Contains(ia.PositionChannels[i])) continue;
				Vector3 v = fr.Positions[i];
				canal(ia.PositionChannels[i]).PositionKeys.Add(new Assimp.VectorKey(f, new Assimp.Vector3D(v.X, v.Y, v.Z)));
			}
			for (int i = 0; i < ia.ScaleChannels.Count && i < fr.Scales.Count; i++)
			{
				if (omitir.Contains(ia.ScaleChannels[i])) continue;
				Vector3 v = fr.Scales[i];
				canal(ia.ScaleChannels[i]).ScalingKeys.Add(new Assimp.VectorKey(f, new Assimp.Vector3D(v.X, v.Y, v.Z)));
			}
		}
		escena.Animations.Add(anim);
		return escena;
	}

	private static double Comparar(AssetBankPlugin.Export.InternalAnimation a, AssetBankPlugin.Export.InternalAnimation b, out double pos)
	{
		double rot = 0;
		pos = 0;
		if (a.Frames.Count != b.Frames.Count || a.RotationChannels.Count != b.RotationChannels.Count || a.PositionChannels.Count != b.PositionChannels.Count) return double.MaxValue;
		for (int f = 0; f < a.Frames.Count; f++)
		{
			for (int i = 0; i < a.Frames[f].Rotations.Count; i++)
			{
				Quaternion x = a.Frames[f].Rotations[i], y = b.Frames[f].Rotations[i];
				double d1 = Math.Max(Math.Max(Math.Abs(x.X - y.X), Math.Abs(x.Y - y.Y)), Math.Max(Math.Abs(x.Z - y.Z), Math.Abs(x.W - y.W)));
				double d2 = Math.Max(Math.Max(Math.Abs(x.X + y.X), Math.Abs(x.Y + y.Y)), Math.Max(Math.Abs(x.Z + y.Z), Math.Abs(x.W + y.W)));
				rot = Math.Max(rot, Math.Min(d1, d2));
			}
			for (int i = 0; i < a.Frames[f].Positions.Count; i++) pos = Math.Max(pos, (a.Frames[f].Positions[i] - b.Frames[f].Positions[i]).Length());
			for (int i = 0; i < a.Frames[f].Scales.Count && i < b.Frames[f].Scales.Count; i++) pos = Math.Max(pos, (a.Frames[f].Scales[i] - b.Frames[f].Scales[i]).Length());
		}
		return rot;
	}

	/// Camino completo del botón "Import Animation": escena con huesos por nombre -> muestreo -> compresión -> banco.
	/// Necesita un banco que contenga también los canales (los de nivel) y PRUEBA_PERFIL con el perfil de Frosty.
	private static int PorEscena(byte[] bytes, Bank bank)
	{
		string perfil = Environment.GetEnvironmentVariable("PRUEBA_PERFIL") ?? "PVZ.Main_Win64_Retail";
		FrostySdk.ProfilesLibrary.Initialize(new List<FrostySdk.Profile>());
		FrostySdk.ProfilesLibrary.Initialize(perfil);
		Frosty.Core.App.Logger = new Registro();
		Registro.Detallado = Environment.GetEnvironmentVariable("PRUEBA_LOG") == "1";
		string solo = Environment.GetEnvironmentVariable("PRUEBA_SOLO");
		int max = int.Parse(Environment.GetEnvironmentVariable("PRUEBA_MAX") ?? "100000");
		int n = 0, mal = 0, sinCanales = 0;
		double peorQ = 0, peorV = 0;
		byte[] actual = bytes;
		Dictionary<Guid, AssetBankPlugin.Export.InternalAnimation> antes = new Dictionary<Guid, AssetBankPlugin.Export.InternalAnimation>();
		HashSet<Guid> parciales = new HashSet<Guid>();
		foreach (VbrAnimationAsset a in AntRefTable.Refs.Values.OfType<VbrAnimationAsset>().ToList())
		{
			if (!string.IsNullOrEmpty(solo) && a.Name.IndexOf(solo, StringComparison.OrdinalIgnoreCase) < 0) continue;
			if (n >= max) break;
			a.Channels = a.GetChannels(a.ChannelToDofAsset);
			if (a.OrderedChannels == null || a.OrderedChannels.Count == 0) { sinCanales++; continue; }
			AssetBankPlugin.Export.InternalAnimation ia = a.ConvertToInternal();
			if (ia == null || ia.Frames.Count == 0) { sinCanales++; continue; }
			antes[a.ID] = ia;
			// una de cada tres se importa sin la mitad de los huesos: deben conservar la pose original
			HashSet<string> omitir = new HashSet<string>();
			if (n % 3 == 2 || Environment.GetEnvironmentVariable("PRUEBA_PARCIAL") == "1")
			{
				for (int i = 0; i < ia.RotationChannels.Count; i += 2) omitir.Add(ia.RotationChannels[i]);
				parciales.Add(a.ID);
			}
			try
			{
				actual = AnimationImporter.ImportLegacy(Escena(ia, a.FPS > 0f ? a.FPS : 30f, omitir), actual, a, bank.Classes, 0f);
				n++;
			}
			catch (Exception e)
			{
				mal++;
				Console.WriteLine("  fallo al importar '" + a.Name + "': " + e);
			}
		}
		AntRefTable.Clear();
		using (NativeReader r = new NativeReader(new MemoryStream(actual)))
		{
			new Bank(r, 0);
		}
		int parcialesMal = 0;
		foreach (VbrAnimationAsset a in AntRefTable.Refs.Values.OfType<VbrAnimationAsset>().ToList())
		{
			AssetBankPlugin.Export.InternalAnimation original;
			if (!antes.TryGetValue(a.ID, out original)) continue;
			a.Channels = a.GetChannels(a.ChannelToDofAsset);
			AssetBankPlugin.Export.InternalAnimation nueva = a.ConvertToInternal();
			double ev = 0, eq = double.MaxValue;
			if (nueva != null) eq = Comparar(original, nueva, out ev);
			bool parcial = parciales.Contains(a.ID);
			// en las parciales los huesos omitidos quedan congelados en el fotograma 0: solo se exige que se pueda leer
			if (!parcial)
			{
				if (eq > 0.02 || ev > 0.02)
				{
					mal++;
					if (mal <= 8) Console.WriteLine("  ERROR ALTO '" + a.Name + "': rot " + eq.ToString("0.#####") + " pos " + ev.ToString("0.#####") + " claves " + a.NumKeys);
				}
				peorQ = Math.Max(peorQ, eq);
				peorV = Math.Max(peorV, ev);
			}
			else if (nueva == null || nueva.Frames.Count != original.Frames.Count)
			{
				parcialesMal++;
				mal++;
			}
			else
			{
				// los huesos omitidos (índices pares) deben tener en todos los fotogramas la rotación original del fotograma 0
				double e = 0;
				for (int f = 0; f < nueva.Frames.Count; f++)
					for (int i = 0; i < nueva.RotationChannels.Count; i += 2)
					{
						Quaternion x = nueva.Frames[f].Rotations[i], y = original.Frames[0].Rotations[i];
						double d1 = Math.Max(Math.Max(Math.Abs(x.X - y.X), Math.Abs(x.Y - y.Y)), Math.Max(Math.Abs(x.Z - y.Z), Math.Abs(x.W - y.W)));
						double d2 = Math.Max(Math.Max(Math.Abs(x.X + y.X), Math.Abs(x.Y + y.Y)), Math.Max(Math.Abs(x.Z + y.Z), Math.Abs(x.W + y.W)));
						if (Math.Min(d1, d2) > 0.02 && f == 0 && Registro.Detallado) Console.WriteLine("    hueso omitido desviado: " + nueva.RotationChannels[i] + " (" + i + ")");
						e = Math.Max(e, Math.Min(d1, d2));
					}
				if (e > 0.02)
				{
					parcialesMal++;
					mal++;
					if (parcialesMal <= 5) Console.WriteLine("  PARCIAL MAL '" + a.Name + "': los huesos omitidos se desvian " + e.ToString("0.#####"));
				}
			}
		}
		Console.WriteLine("  importadas por escena " + n + " (parciales " + parciales.Count + ", mal " + parcialesMal + "), sin canales " + sinCanales + ", fallos " + mal + ", peor error rot " + peorQ.ToString("0.######") + " pos " + peorV.ToString("0.######"));
		string salida = Environment.GetEnvironmentVariable("PRUEBA_SALIDA");
		if (!string.IsNullOrEmpty(salida)) File.WriteAllBytes(salida, actual);
		return mal;
	}

	private static int IdaYVuelta(byte[] bytes, Bank bank)
	{
		int n = 0, mal = 0, constantes = 0, constantesIguales = 0;
		double peorQ = 0, peorV = 0;
		long antes = 0, despues = 0;
		byte[] actual = bytes;
		List<VbrAnimationAsset> originales = AntRefTable.Refs.Values.OfType<VbrAnimationAsset>().ToList();
		Dictionary<Guid, float[][][]> q0 = new Dictionary<Guid, float[][][]>();
		Dictionary<Guid, float[][][]> v0 = new Dictionary<Guid, float[][][]>();
		string solo = Environment.GetEnvironmentVariable("PRUEBA_SOLO");
		foreach (VbrAnimationAsset a in originales)
		{
			if (!string.IsNullOrEmpty(solo) && a.Name.IndexOf(solo, StringComparison.OrdinalIgnoreCase) < 0) continue;
			float[][][] quats, vecs;
			float[][] floats;
			try
			{
				Canales(a, out quats, out vecs, out floats);
			}
			catch (Exception e)
			{
				Console.WriteLine("  no se pudo decodificar '" + a.Name + "': " + e.Message);
				mal++;
				continue;
			}
			q0[a.ID] = quats;
			v0[a.ID] = vecs;
			if (a.Name == Environment.GetEnvironmentVariable("PRUEBA_DETALLE"))
			{
				Console.WriteLine("  ORIGINAL " + a.Name + ": aQ " + a.QuaternionCount + " cQ " + a.ConstQuaternionCount + " aV " + a.Vector3Count + " cV " + a.ConstVector3Count
					+ " aF " + a.NumFloat + " cF " + a.ConstFloatCount + " claves " + a.NumKeys + " cps " + a.ConstPaletteSize + " voff " + a.VectorOffsetSize + " foff " + a.FloatOffsetSize
					+ " bloques " + string.Join(",", a.FrameBlockSizes) + " data " + a.Data.Length
					+ " flags " + a.Flags + " quat [" + a.QuatMin + ", " + a.QuatMax + "] vec [" + a.Vec3Min + ", " + a.Vec3Max + "] traj [" + a.TrajMin + ", " + a.TrajMax + "] dct " + a.Dct
					+ " paleta " + a.ConstantPalette.Length + " mapa " + BitConverter.ToString(a.ConstChanMap ?? new byte[0]));
			}
			int inicio = GwBankFile.FindSection(actual, bank.Classes, a.ID);
			if (inicio < 0) { Console.WriteLine("  sin seccion: " + a.Name); mal++; continue; }
			int tamAntes = (int)GwBinary.ReadU32(actual, inicio + 8, true);
			byte[] seccionAntes = new byte[tamAntes];
			Buffer.BlockCopy(actual, inicio, seccionAntes, 0, tamAntes);
			bool soloConstantes = a.QuaternionCount + a.Vector3Count + a.NumFloat == 0;
			try
			{
				actual = AnimationImporter.ImportLegacy(quats, vecs, floats, a.NumKeys, actual, a, bank.Classes);
				// sin canales animados no hay pérdida: la sección debe salir idéntica a la original
				if (soloConstantes)
				{
					constantes++;
					bool igual = GwBinary.ReadU32(actual, inicio + 8, true) == tamAntes;
					for (int i = 0; igual && i < tamAntes; i++) igual = actual[inicio + i] == seccionAntes[i];
					if (igual) constantesIguales++;
					else if (constantes - constantesIguales <= 3) Console.WriteLine("  constante distinta: " + a.Name);
				}
			}
			catch (Exception e)
			{
				Console.WriteLine("  fallo al importar '" + a.Name + "': " + e);
				mal++;
				continue;
			}
			n++;
		}
		// releer el banco resultante desde cero y comparar
		AntRefTable.Clear();
		Bank nuevo;
		using (NativeReader r = new NativeReader(new MemoryStream(actual)))
		{
			nuevo = new Bank(r, 0);
		}
		foreach (VbrAnimationAsset a in AntRefTable.Refs.Values.OfType<VbrAnimationAsset>())
		{
			float[][][] quats, vecs, oq, ov;
			float[][] floats;
			if (!q0.TryGetValue(a.ID, out oq)) continue;
			ov = v0[a.ID];
			try
			{
				Canales(a, out quats, out vecs, out floats);
			}
			catch (Exception e)
			{
				mal++;
				Console.WriteLine("  NO SE PUEDE RELEER '" + a.Name + "': " + e.Message + " | aQ " + a.QuaternionCount + " cQ " + a.ConstQuaternionCount + " aV " + a.Vector3Count + " cV " + a.ConstVector3Count
					+ " aF " + a.NumFloat + " cF " + a.ConstFloatCount + " claves " + a.NumKeys + " flags " + a.Flags + " paleta " + a.ConstantPalette.Length + " cps " + a.ConstPaletteSize + " mapa " + a.ConstChanMapSize
					+ " voff " + a.VectorOffsetSize + " foff " + a.FloatOffsetSize + " bloques " + string.Join(",", a.FrameBlockSizes) + " data " + a.Data.Length);
				continue;
			}
			double eq = 0, ev = 0;
			if (quats.Length != oq.Length || vecs.Length != ov.Length || (oq.Length > 0 && quats[0].Length != oq[0].Length))
			{
				Console.WriteLine("  forma distinta en '" + a.Name + "'");
				mal++;
				continue;
			}
			for (int c = 0; c < quats.Length; c++)
				for (int f = 0; f < quats[c].Length; f++)
				{
					// q y -q son la misma rotación
					double d1 = 0, d2 = 0;
					for (int j = 0; j < 4; j++)
					{
						d1 = Math.Max(d1, Math.Abs(quats[c][f][j] - oq[c][f][j]));
						d2 = Math.Max(d2, Math.Abs(quats[c][f][j] + oq[c][f][j]));
					}
					eq = Math.Max(eq, Math.Min(d1, d2));
				}
			for (int c = 0; c < vecs.Length; c++)
				for (int f = 0; f < vecs[c].Length; f++)
					for (int j = 0; j < 3; j++)
						ev = Math.Max(ev, Math.Abs(vecs[c][f][j] - ov[c][f][j]));
			if (a.Name == Environment.GetEnvironmentVariable("PRUEBA_DETALLE"))
			{
				Console.WriteLine("  DETALLE " + a.Name + ": aQ " + a.QuaternionCount + " cQ " + a.ConstQuaternionCount + " aV " + a.Vector3Count + " cV " + a.ConstVector3Count
					+ " flags " + a.Flags + " quat [" + a.QuatMin + ", " + a.QuatMax + "] vec [" + a.Vec3Min + ", " + a.Vec3Max + "] traj [" + a.TrajMin + ", " + a.TrajMax + "] dct " + a.Dct
					+ " paleta " + a.ConstantPalette.Length + " mapa " + BitConverter.ToString(a.ConstChanMap ?? new byte[0]));
				for (int c = 0; c < Math.Min(quats.Length, 12); c++)
					Console.WriteLine("    q" + c + " antes " + string.Join(" ", oq[c][0].Select(x => x.ToString("0.0000"))) + " | despues " + string.Join(" ", quats[c][0].Select(x => x.ToString("0.0000"))));
				for (int c = 0; c < Math.Min(vecs.Length, 6); c++)
					Console.WriteLine("    v" + c + " antes " + string.Join(" ", ov[c][0].Select(x => x.ToString("0.0000"))) + " | despues " + string.Join(" ", vecs[c][0].Select(x => x.ToString("0.0000"))));
			}
			if (eq > 0.02 || ev > 0.02)
			{
				mal++;
				if (mal <= 8) Console.WriteLine("  ERROR ALTO '" + a.Name + "': rot " + eq.ToString("0.#####") + " pos " + ev.ToString("0.#####") + " claves " + a.NumKeys);
			}
			peorQ = Math.Max(peorQ, eq);
			peorV = Math.Max(peorV, ev);
		}
		foreach (GwBankFile.Section s in GwBankFile.Sections(actual)) if (s.Tag == "GD.DATA") despues += s.Size;
		Console.WriteLine("  solo constantes: " + constantes + ", de ellas identicas al original byte a byte: " + constantesIguales);
		Console.WriteLine("  reimportadas " + n + ", fallos " + mal + ", peor error rot " + peorQ.ToString("0.######") + " pos " + peorV.ToString("0.######"));
		Console.WriteLine("  banco " + bytes.Length + " -> " + actual.Length + " bytes; presupuesto " + GwBinary.ReadU32(bytes, 20, true) + " -> " + GwBinary.ReadU32(actual, 20, true));
		string salida = Environment.GetEnvironmentVariable("PRUEBA_SALIDA");
		if (!string.IsNullOrEmpty(salida)) File.WriteAllBytes(salida, actual);
		return mal;
	}
}
