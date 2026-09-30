// Solo existe en paquetes compilados con HabilitarExportacionBaseDatos: uno normal no puede dejar la base en claro.
#if EXPORTAR_BASE_DATOS

using System.Text;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using Microsoft.Maui.Storage;
using SQLite;

namespace AppOperador.Infrastructure.Sqlite;

// Copiar el archivo cifrado no sirve: la clave no sale del KeyStore. Se convierte desde dentro con sqlcipher_export.
public sealed class ExportadorBaseDatosSqlite : IExportadorBaseDatos
{
	private const string BaseAdjunta = "legible";

	private const string FirmaSqlite = "SQLite format 3\0";

	private const string PrefijoTemporal = "appoperador-export-";

	// Más que cualquier exportación en curso, menos que para olvidarse semanas.
	private static readonly TimeSpan VidaDeUnTemporal = TimeSpan.FromHours(1);

	private readonly BaseDatosLocal _baseDatos;
	private readonly IClock _reloj;
	private readonly string _directorioTemporal;

	public ExportadorBaseDatosSqlite(
		BaseDatosLocal baseDatos,
		IClock reloj,
		string? directorioTemporal = null)
	{
		_baseDatos = baseDatos;
		_reloj = reloj;
		_directorioTemporal = directorioTemporal ?? FileSystem.CacheDirectory;
	}

	public async Task<ExportacionBaseDatos> ExportarAsync(
		string etiquetaAmbiente,
		CancellationToken cancelacion = default)
	{
		cancelacion.ThrowIfCancellationRequested();

		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);

		var versionOrigen = await conexion.ExecuteScalarAsync<int>("PRAGMA user_version;");
		var estructuraOrigen = await LeerEstructuraAsync(conexion);

		BarrerTemporalesViejos();

		var nombre = ComponerNombre(etiquetaAmbiente, versionOrigen);
		var destino = Path.Combine(_directorioTemporal, PrefijoTemporal + Guid.NewGuid().ToString("N") + ".db3");

		Directory.CreateDirectory(_directorioTemporal);

		// sqlcipher_export vuelca sobre lo que encuentre; un archivo previo mezclaría la copia.
		File.Delete(destino);

		try
		{
			await ConvertirAsync(conexion, destino, versionOrigen);
			await ComprobarAsync(destino, versionOrigen, estructuraOrigen);
		}
		catch (ExportacionBaseDatosException)
		{
			Borrar(destino);
			throw;
		}
		catch (Exception error)
		{
			Borrar(destino);
			throw new ExportacionBaseDatosException(
				"No se pudo crear la copia legible de la base de datos.", error);
		}

		return new ExportacionBaseDatos(
			destino,
			nombre,
			versionOrigen,
			[.. estructuraOrigen.Where(e => e.StartsWith("table:", StringComparison.Ordinal))
				.Select(e => e["table:".Length..])],
			new FileInfo(destino).Length);
	}

	// KEY '' deja la copia en claro; user_version se copia a mano; el DETACH siempre, o estorba a la siguiente.
	private static async Task ConvertirAsync(
		SQLiteAsyncConnection conexion,
		string destino,
		int versionOrigen)
	{
		await conexion.ExecuteAsync(
			$"ATTACH DATABASE '{Escapar(destino)}' AS {BaseAdjunta} KEY '';");

		try
		{
			await conexion.ExecuteScalarAsync<string>($"SELECT sqlcipher_export('{BaseAdjunta}');");
			await conexion.ExecuteAsync($"PRAGMA {BaseAdjunta}.user_version = {versionOrigen};");
		}
		finally
		{
			await conexion.ExecuteAsync($"DETACH DATABASE {BaseAdjunta};");
		}
	}

	// Una copia corrupta es peor que ninguna: se revisa firma, integridad, versión y estructura.
	private static async Task ComprobarAsync(
		string destino,
		int versionOrigen,
		IReadOnlyList<string> estructuraOrigen)
	{
		if (!File.Exists(destino))
		{
			throw new ExportacionBaseDatosException("La copia no llegó a crearse.");
		}

		if (!await TieneFirmaSqliteAsync(destino))
		{
			throw new ExportacionBaseDatosException(
				"La copia no es un archivo SQLite estándar; pudo quedar cifrada.");
		}

		// Sin clave a propósito: si solo abriera con ella, la exportación no sirvió.
		var copia = new SQLiteAsyncConnection(
			new SQLiteConnectionString(destino, SQLiteOpenFlags.ReadOnly, storeDateTimeAsTicks: true));

		try
		{
			var integridad = await copia.ExecuteScalarAsync<string>("PRAGMA quick_check;");
			if (!string.Equals(integridad, "ok", StringComparison.OrdinalIgnoreCase))
			{
				throw new ExportacionBaseDatosException($"La copia no pasó la revisión de integridad: {integridad}.");
			}

			var versionCopia = await copia.ExecuteScalarAsync<int>("PRAGMA user_version;");
			if (versionCopia != versionOrigen)
			{
				throw new ExportacionBaseDatosException(
					$"La copia quedó en la versión de esquema {versionCopia} y el origen es {versionOrigen}.");
			}

			var estructuraCopia = await LeerEstructuraAsync(copia);
			if (!estructuraCopia.SequenceEqual(estructuraOrigen, StringComparer.Ordinal))
			{
				var faltantes = estructuraOrigen.Except(estructuraCopia, StringComparer.Ordinal).ToList();
				throw new ExportacionBaseDatosException(faltantes.Count > 0
					? $"A la copia le faltan objetos del esquema: {string.Join(", ", faltantes)}."
					: "La copia no tiene el mismo esquema que el origen.");
			}
		}
		finally
		{
			await copia.CloseAsync();
		}
	}

	// Sin lo que empieza con sqlite_: el motor lo administra y no tiene por qué coincidir.
	private static async Task<IReadOnlyList<string>> LeerEstructuraAsync(SQLiteAsyncConnection conexion) =>
		await conexion.QueryScalarsAsync<string>(
			"""
			SELECT type || ':' || name FROM sqlite_master
			WHERE type IN ('table', 'index') AND name NOT LIKE 'sqlite_%'
			ORDER BY type, name;
			""");

	private static async Task<bool> TieneFirmaSqliteAsync(string ruta)
	{
		var esperada = Encoding.ASCII.GetBytes(FirmaSqlite);
		var leidos = new byte[esperada.Length];

		await using (var archivo = File.OpenRead(ruta))
		{
			if (await archivo.ReadAsync(leidos) != leidos.Length)
			{
				return false;
			}
		}

		return leidos.SequenceEqual(esperada);
	}

	// Hora local, que es la que el usuario busca. Sin operador ni unidad: el nombre se ve en carpetas compartidas.
	private string ComponerNombre(string etiquetaAmbiente, int versionEsquema)
	{
		var ambiente = new string([.. etiquetaAmbiente.Where(char.IsLetterOrDigit)]);
		if (ambiente.Length == 0)
		{
			ambiente = "Local";
		}

		var cuando = _reloj.UtcAhora.ToLocalTime().ToString("yyyyMMdd-HHmmss");
		return $"AppOperador-{ambiente}-{cuando}-esquema{versionEsquema}.db3";
	}

	// Son archivos sin cifrar; limpiar es accesorio y no debe impedir la exportación.
	private void BarrerTemporalesViejos()
	{
		try
		{
			if (!Directory.Exists(_directorioTemporal))
			{
				return;
			}

			var limite = DateTime.UtcNow - VidaDeUnTemporal;

			foreach (var viejo in Directory.EnumerateFiles(_directorioTemporal, PrefijoTemporal + "*.db3"))
			{
				if (File.GetLastWriteTimeUtc(viejo) < limite)
				{
					Borrar(viejo);
				}
			}
		}
		catch (IOException)
		{
		}
		catch (UnauthorizedAccessException)
		{
		}
	}

	private static void Borrar(string ruta)
	{
		try
		{
			if (File.Exists(ruta))
			{
				File.Delete(ruta);
			}
		}
		catch (IOException)
		{
		}
		catch (UnauthorizedAccessException)
		{
		}
	}

	// ATTACH no admite parámetros: la ruta va en el texto.
	private static string Escapar(string valor) => valor.Replace("'", "''");
}

#endif
