using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;
using AppOperador.Infrastructure.Sqlite.Entidades;

namespace AppOperador.Infrastructure.Sqlite;

/// <summary>
/// Sesión persistida en SQLite, para reanudarla tras cerrar la app (JTT-1383).
/// </summary>
/// <remarks>
/// Guarda una sola fila y la reemplaza entera en cada validación. No hay historial: lo que
/// importa es la última validación en línea, y conservar las anteriores solo daría material
/// para intentar reanudar una vencida.
/// </remarks>
public sealed class AlmacenSesionOfflineSqlite : IOfflineSessionStore
{
	private const char Separador = ',';

	private readonly BaseDatosLocal _baseDatos;

	public AlmacenSesionOfflineSqlite(BaseDatosLocal baseDatos) => _baseDatos = baseDatos;

	/// <inheritdoc />
	public async Task GuardarAsync(SesionOfflinePersistida sesion, CancellationToken cancelacion = default)
	{
		ArgumentNullException.ThrowIfNull(sesion);

		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);

		await conexion.InsertOrReplaceAsync(new SesionLocal
		{
			Id = SesionLocal.IdUnico,
			SessionId = sesion.SessionId,
			Operador = sesion.Operador,
			Rol = sesion.Rol,
			UnidadId = sesion.Unidad.Id,
			UnidadClave = sesion.Unidad.Clave,
			UnidadDescripcion = sesion.Unidad.Descripcion,
			Permisos = string.Join(Separador, sesion.Permisos),
			ValidadoUtcTicks = sesion.Vigencia.LastValidatedAtUtc.Ticks,
			OfflineHastaUtcTicks = sesion.Vigencia.OfflineUntilUtc.Ticks,
			MonotonicoAlValidarTicks = sesion.MonotonicoAlValidar.Ticks,
			VersionAplicacion = sesion.Instalacion.VersionAplicacion,
			VersionCatalogos = sesion.Instalacion.VersionCatalogos.ToString("yyyy-MM-dd"),
		});
	}

	/// <inheritdoc />
	public async Task<SesionOfflinePersistida?> ObtenerAsync(CancellationToken cancelacion = default)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);

		var fila = await conexion.Table<SesionLocal>()
			.Where(s => s.Id == SesionLocal.IdUnico)
			.FirstOrDefaultAsync();

		return fila is null ? null : Convertir(fila);
	}

	/// <inheritdoc />
	public async Task LimpiarAsync(CancellationToken cancelacion = default)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		await conexion.DeleteAsync<SesionLocal>(SesionLocal.IdUnico);
	}

	/// <summary>
	/// Rehidrata la fila, o devuelve <see langword="null"/> si no es aprovechable.
	/// </summary>
	/// <remarks>
	/// Una fila corrupta o con fechas imposibles se trata como «no hay sesión guardada»: la
	/// consecuencia es pedir autenticación, que es la salida segura. Recuperar a medias una
	/// sesión ilegible sería peor que no tenerla.
	/// </remarks>
	private static SesionOfflinePersistida? Convertir(SesionLocal fila)
	{
		if (fila.OfflineHastaUtcTicks < fila.ValidadoUtcTicks)
		{
			return null;
		}

		VigenciaOffline vigencia;
		try
		{
			vigencia = VigenciaOffline.DelServidor(
				new DateTime(fila.ValidadoUtcTicks, DateTimeKind.Utc),
				new DateTime(fila.OfflineHastaUtcTicks, DateTimeKind.Utc));
		}
		catch (ArgumentException)
		{
			return null;
		}

		if (!DateOnly.TryParse(fila.VersionCatalogos, out var catalogos))
		{
			catalogos = DateOnly.FromDateTime(vigencia.LastValidatedAtUtc);
		}

		return new SesionOfflinePersistida(
			SessionId: fila.SessionId,
			Operador: fila.Operador,
			Rol: fila.Rol,
			Unidad: new UnidadVehicular(fila.UnidadId, fila.UnidadClave, fila.UnidadDescripcion),
			Permisos: PermisosOperador.DelServidor(
				fila.Permisos.Split(Separador, StringSplitOptions.RemoveEmptyEntries)),
			Vigencia: vigencia,
			MonotonicoAlValidar: TimeSpan.FromTicks(fila.MonotonicoAlValidarTicks),
			Instalacion: new DatosDeInstalacion(fila.VersionAplicacion, catalogos));
	}
}
