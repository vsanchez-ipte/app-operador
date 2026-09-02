using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Infrastructure.Sqlite.Entidades;

namespace AppOperador.Infrastructure.Sqlite;

/// <summary>
/// Traducción entre la fila de SQLite y el modelo que consumen las pantallas.
/// </summary>
/// <remarks>
/// Vive aparte porque lo usan el repositorio y la cola, y conviene que las dos pantallas
/// describan una incidencia exactamente igual.
/// </remarks>
internal static class MapeoIncidencia
{
	/// <summary>
	/// Convierte una fila en el elemento que lista la pantalla de Cola.
	/// </summary>
	/// <param name="ultimoErrorMensaje">
	/// Lo que dijo Jacob en el último intento fallido, si se conoce.
	/// <para>
	/// <b>Viene de fuera porque no está en esta fila</b>: la incidencia guarda el código del
	/// rechazo, pero el mensaje vive en <c>intento_sincronizacion</c>, que es una tabla aparte.
	/// Quien la consulta es la cola, que sabe qué registros va a listar y puede pedirlos de una
	/// sola vez.
	/// </para>
	/// </param>
	public static RegistroCola ARegistroCola(
		IncidenciaLocal fila,
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
		ultimoErrorMensaje);

	/// <summary>
	/// Describe el contenido del registro: solo el tipo de incidencia.
	/// </summary>
	/// <remarks>
	/// <b>Solo el tipo, nada más.</b> La clase y la prioridad las antepone la vista al
	/// componer <c>clase / prioridad / descripción / KM</c>; repetirlas aquí las duplica
	/// en pantalla.
	///
	/// Un borrador puede no tener tipo todavía; en ese caso se dice explícitamente en vez
	/// de dejar el hueco vacío, que se leería como un error de carga.
	/// </remarks>
	private static string Describir(IncidenciaLocal fila) =>
		string.IsNullOrWhiteSpace(fila.TipoNombre) ? "Sin tipo" : fila.TipoNombre;
}
