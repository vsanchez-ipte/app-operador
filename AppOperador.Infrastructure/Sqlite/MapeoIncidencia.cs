using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Infrastructure.Sqlite.Entidades;

namespace AppOperador.Infrastructure.Sqlite;

internal static class MapeoIncidencia
{
	public static RegistroCola ARegistroCola(
		IncidenciaLocal fila,
		// El mensaje vive en la tabla de intentos; la cola lo consulta de una vez para toda la lista.
		string? ultimoErrorMensaje = null) => new(
		fila.ClaveLocal,
		ClaseRegistro.Incidencia,
		(SyncPriority)fila.Prioridad,
		Describir(fila),
		fila.Kilometro ?? string.Empty,
		(EstadoSincronizacion)fila.Estado,
		fila.FolioCentral,
		fila.SeveridadNombre,
		fila.UltimoErrorCodigo,
		ultimoErrorMensaje,
		fila.Intentos,
		// Último cambio de estado = último intento: la misma columna que decide la espera.
		new DateTime(fila.ActualizadoUtcTicks, DateTimeKind.Utc),
		// Hora de captura, la misma que viaja a Jacob.
		new DateTime(fila.CreadoUtcTicks, DateTimeKind.Utc));

	// Solo el tipo: la vista antepone clase y prioridad.
	private static string Describir(IncidenciaLocal fila) =>
		string.IsNullOrWhiteSpace(fila.TipoNombre) ? "Sin tipo" : fila.TipoNombre;
}
