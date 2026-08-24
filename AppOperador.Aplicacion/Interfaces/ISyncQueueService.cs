using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Cola local de registros pendientes de enviar a Jacob CCO.
/// </summary>
/// <remarks>
/// <para>
/// El nombre está fijado en inglés por el documento de arquitectura. La prioridad la determina la
/// regla de dominio <c>ReglaPrioridadSincronizacion</c>.
/// </para>
/// <para>
/// <b>Esta interfaz solo persiste; no orquesta.</b> Hasta JTT-1401 tenía un
/// <c>SincronizarAsync</c> cuya implementación en SQLite decidía la compuerta de conectividad,
/// recorría los registros, aplicaba las transiciones de estado y escribía la bitácora. Eso es
/// orquestación de aplicación, y vivía en la capa de persistencia: al añadirle la espera
/// creciente y las familias de error habría quedado sepultada ahí. Ahora la orquestación es
/// <c>SincronizarIncidencias</c>, y la cola se limita a leer y escribir.
/// </para>
/// <para>
/// <b>La cola es de quien tiene la sesión abierta</b> (JTT-1390 CA 7). Los registros de otros
/// operadores siguen guardados y conservan su identidad, pero no se listan, no se cuentan y no se
/// envían con el token de alguien más.
/// </para>
/// </remarks>
public interface ISyncQueueService
{
	/// <summary>Registros en cola, del más reciente al más antiguo.</summary>
	Task<IReadOnlyList<RegistroCola>> ObtenerRegistrosAsync(CancellationToken cancelacion = default);

	/// <summary>Cantidad de registros que siguen esperando envío.</summary>
	Task<int> ContarPendientesAsync(CancellationToken cancelacion = default);

	/// <summary>
	/// Registros que pueden intentarse, en el orden en que deben atenderse.
	/// </summary>
	/// <remarks>
	/// Primero la prioridad y después la antigüedad: una incidencia crítica sale antes que una
	/// normal capturada antes que ella. <b>Los borradores nunca entran</b> (JTT-1399 CA 3,
	/// JTT-1401 CA 11), y tampoco lo ya sincronizado.
	/// </remarks>
	Task<IReadOnlyList<IncidenciaEnviable>> ObtenerEnviablesAsync(CancellationToken cancelacion = default);

	/// <summary>Escribe el resultado de un intento en el registro.</summary>
	Task ActualizarEnvioAsync(ActualizacionEnvio actualizacion, CancellationToken cancelacion = default);

	/// <summary>
	/// Deja constancia de un intento en la bitácora de envíos.
	/// </summary>
	/// <remarks>
	/// Se registra tanto el éxito como el fallo: la traza de por qué una incidencia no ha salido
	/// es lo único con lo que se puede diagnosticar un dispositivo que vuelve de campo.
	/// </remarks>
	Task RegistrarIntentoAsync(
		string uuid,
		bool exito,
		string? codigo,
		string? mensaje,
		CancellationToken cancelacion = default);
}
