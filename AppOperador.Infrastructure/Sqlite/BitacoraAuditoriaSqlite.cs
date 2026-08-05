using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Infrastructure.Sqlite.Entidades;

namespace AppOperador.Infrastructure.Sqlite;

/// <summary>
/// Bitácora local de eventos, respaldada en SQLite.
/// </summary>
/// <remarks>
/// A diferencia del simulador en memoria, los eventos sobreviven al cierre de la app: es
/// lo que permite responder qué pasó en un turno anterior.
///
/// Se conserva un número acotado de eventos. Sin recorte, la tabla crece sin límite en un
/// dispositivo que puede pasar semanas sin mantenimiento.
/// </remarks>
public sealed class BitacoraAuditoriaSqlite : IAuditLog
{
	/// <summary>Cuántos eventos se conservan como máximo.</summary>
	/// <remarks>
	/// Valor provisional: la política de retención real es de JTT-292 y todavía no está
	/// cerrada. Se deja explícito para que sea fácil de cambiar cuando se defina.
	/// </remarks>
	public const int EventosMaximos = 200;

	private readonly BaseDatosLocal _baseDatos;
	private readonly IClock _reloj;

	public BitacoraAuditoriaSqlite(BaseDatosLocal baseDatos, IClock reloj)
	{
		_baseDatos = baseDatos;
		_reloj = reloj;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<EventoAuditoria>> ObtenerEventosAsync(CancellationToken cancelacion = default)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);

		var filas = await conexion.Table<EventoAuditoriaLocal>()
			.OrderByDescending(e => e.InstanteUtcTicks)
			.Take(EventosMaximos)
			.ToListAsync();

		return filas
			.Select(e => new EventoAuditoria(
				// Se guardó como ticks UTC: al reconstruir hay que volver a sellar el Kind,
				// porque un DateTime sin Kind se compara como hora local.
				new DateTime(e.InstanteUtcTicks, DateTimeKind.Utc),
				(NivelAuditoria)e.Nivel,
				e.Mensaje))
			.ToList();
	}

	/// <inheritdoc />
	public async Task RegistrarAsync(NivelAuditoria nivel, string mensaje, CancellationToken cancelacion = default)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);

		await conexion.InsertAsync(new EventoAuditoriaLocal
		{
			InstanteUtcTicks = _reloj.UtcAhora.Ticks,
			Nivel = (int)nivel,
			Mensaje = mensaje,
		});

		await RecortarAsync(conexion);
	}

	/// <summary>
	/// Borra los eventos más antiguos que exceden <see cref="EventosMaximos"/>.
	/// </summary>
	private static Task RecortarAsync(SQLite.SQLiteAsyncConnection conexion) =>
		conexion.ExecuteAsync(
			"""
			DELETE FROM evento_auditoria
			WHERE id NOT IN (
				SELECT id FROM evento_auditoria
				ORDER BY instante_utc_ticks DESC
				LIMIT ?
			);
			""",
			EventosMaximos);
}
