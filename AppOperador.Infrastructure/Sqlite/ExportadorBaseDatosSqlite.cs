// Solo se compila en paquetes armados expresamente con la exportación de diagnóstico
// (propiedad HabilitarExportacionBaseDatos, explicada en AppOperador.Mobile.csproj). Con el
// interruptor apagado esta clase no llega siquiera al ensamblado, así que un paquete normal
// no lleva dentro código capaz de dejar la base en claro.
#if EXPORTAR_BASE_DATOS

using System.Text;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using Microsoft.Maui.Storage;
using SQLite;

namespace AppOperador.Infrastructure.Sqlite;

/// <summary>
/// Convierte la base SQLCipher del dispositivo en una copia SQLite legible sin clave.
/// </summary>
/// <remarks>
/// <para>
/// <b>Copiar el archivo cifrado no es exportar.</b> SQLCipher cifra el archivo completo,
/// cabecera incluida, y la clave vive en el almacén seguro respaldado por el KeyStore, que no
/// la entrega. Un <c>.db3</c> sacado con el explorador de archivos no lo abre nada. La única
/// vía es convertirlo desde dentro del proceso de la app, que es lo que hace esta clase.
/// </para>
/// <para>
/// La conversión la hace <c>sqlcipher_export</c> contra una base adjunta con clave vacía —la
/// forma que documenta SQLCipher para esto—, la misma llamada que
/// <see cref="BaseDatosLocal"/> usa en el sentido contrario al migrar una base en claro. Va
/// sobre la conexión que ya tiene la app abierta: se ejecuta dentro de su propia transacción,
/// así que la copia no puede salir mezclada con una escritura a medias.
/// </para>
/// <para>
/// Recorrer las tablas a mano habría exigido actualizar esta clase cada vez que se agregue
/// una, y una tabla olvidada no se nota al revisar la copia.
/// </para>
/// </remarks>
public sealed class ExportadorBaseDatosSqlite : IExportadorBaseDatos
{
	/// <summary>Nombre con el que se adjunta la base destino durante la conversión.</summary>
	private const string BaseAdjunta = "legible";

	/// <summary>Los 16 bytes con que arranca todo archivo SQLite estándar.</summary>
	private const string FirmaSqlite = "SQLite format 3\0";

	private const string PrefijoTemporal = "appoperador-export-";

	/// <summary>
	/// Antigüedad a partir de la cual un temporal se considera huérfano.
	/// </summary>
	/// <remarks>
	/// Suficientemente larga para no tocar jamás una exportación en curso, que se mide en
	/// segundos, y suficientemente corta para que un temporal olvidado no se quede semanas.
	/// </remarks>
	private static readonly TimeSpan VidaDeUnTemporal = TimeSpan.FromHours(1);

	private readonly BaseDatosLocal _baseDatos;
	private readonly IClock _reloj;
	private readonly string _directorioTemporal;

	/// <param name="baseDatos">
	/// La misma instancia que usa el resto de la app. Se pide el tipo concreto, como hacen los
	/// repositorios, porque hace falta su conexión ya abierta con la clave puesta.
	/// </param>
	/// <param name="reloj">Para fechar el nombre del archivo.</param>
	/// <param name="directorioTemporal">
	/// Dónde se deja la copia mientras la persona elige destino. Por omisión la caché de la
	/// app, que el sistema puede vaciar y que no se respalda. Las pruebas pasan uno propio
	/// porque fuera del dispositivo <c>FileSystem</c> no existe.
	/// </param>
	public ExportadorBaseDatosSqlite(
		BaseDatosLocal baseDatos,
		IClock reloj,
		string? directorioTemporal = null)
	{
		_baseDatos = baseDatos;
		_reloj = reloj;
		_directorioTemporal = directorioTemporal ?? FileSystem.CacheDirectory;
	}

	/// <inheritdoc />
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

		// sqlcipher_export vuelca sobre lo que encuentre: si quedara un archivo previo, la
		// copia saldría mezclada con él en vez de reemplazarlo.
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

	/// <summary>
	/// Vuelca esquema y filas hacia una base sin clave.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>KEY ''</c> es lo que le dice a SQLCipher que la base adjunta va en claro. Sin esa
	/// cláusula heredaría la clave de la conexión principal y saldría cifrada otra vez, que es
	/// justo lo que no sirve.
	/// </para>
	/// <para>
	/// <b><c>user_version</c> se copia a mano.</b> <c>sqlcipher_export</c> traslada esquema y
	/// datos pero no los pragmas del archivo, así que la copia nacería en la versión 0 y
	/// aparentaría ser de un esquema anterior al revisarla.
	/// </para>
	/// <para>
	/// El <c>DETACH</c> va en <c>finally</c>: una base adjunta que se queda pegada a la
	/// conexión compartida mantiene abierto el archivo temporal y estorba a la exportación
	/// siguiente, que ya no podría ni borrarlo.
	/// </para>
	/// </remarks>
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

	/// <summary>
	/// Comprueba la copia antes de ofrecerla.
	/// </summary>
	/// <remarks>
	/// Entregar un archivo corrupto o incompleto es peor que no entregar ninguno: se abriría
	/// semanas después, en otra máquina, y las conclusiones que salieran de él serían falsas
	/// sin que nada lo delatara. Se comprueban las cuatro cosas que pueden salir mal en
	/// silencio: que no quedara cifrado, que no se corrompiera, que conserve la versión de
	/// esquema y que no falte ninguna tabla ni índice.
	/// </remarks>
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

		// Sin clave a propósito: si se dejara leer solo con ella, la exportación no habría
		// servido para nada.
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

	/// <summary>
	/// Tablas e índices declarados, en orden estable.
	/// </summary>
	/// <remarks>
	/// Se excluye lo que empieza por <c>sqlite_</c> porque el motor lo administra solo: las
	/// tablas internas de secuencias no tienen por qué coincidir entre origen y copia.
	/// </remarks>
	private static async Task<IReadOnlyList<string>> LeerEstructuraAsync(SQLiteAsyncConnection conexion) =>
		await conexion.QueryScalarsAsync<string>(
			"""
			SELECT type || ':' || name FROM sqlite_master
			WHERE type IN ('table', 'index') AND name NOT LIKE 'sqlite_%'
			ORDER BY type, name;
			""");

	/// <summary>Lee los primeros bytes y los compara con la firma pública de SQLite.</summary>
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

	/// <summary>
	/// Nombre propuesto: ambiente, fecha, hora y versión de esquema.
	/// </summary>
	/// <remarks>
	/// La hora va en local y no en UTC porque quien exporta va a buscar el archivo por la hora
	/// que marcaba su teléfono. No entra ni el operador ni la unidad: el nombre se ve en
	/// carpetas compartidas y en el título de las herramientas de escritorio.
	/// </remarks>
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

	/// <summary>
	/// Borra copias que quedaron de exportaciones anteriores.
	/// </summary>
	/// <remarks>
	/// Son archivos sin cifrar. Aunque estén en la caché privada de la app, no hay razón para
	/// que sobrevivan a la sesión en que se crearon. Se limita a los de esta función y a los ya
	/// vencidos, para no tocar nunca una exportación en curso; los fallos se ignoran porque
	/// limpiar es accesorio y no debe impedir la exportación que se está pidiendo.
	/// </remarks>
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

	/// <summary>
	/// Deja una ruta lista para ir dentro de un literal SQL.
	/// </summary>
	/// <remarks>
	/// <c>ATTACH</c> no admite parámetros, así que la ruta va en el texto de la sentencia. Las
	/// rutas de la caché de Android no llevan comillas, pero el nombre del directorio depende
	/// del sistema y no se deja abierta la puerta.
	/// </remarks>
	private static string Escapar(string valor) => valor.Replace("'", "''");
}

#endif
