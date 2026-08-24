using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;

namespace AppOperador.Aplicacion.CasosDeUso;

/// <summary>
/// Envía a Jacob las incidencias pendientes de la cola local (JTT-1401).
/// </summary>
/// <remarks>
/// <para>
/// <b>Vive aquí y no en la cola de SQLite a propósito.</b> Decidir si toca sincronizar, en qué
/// orden, qué se reintenta y qué se queda esperando es orquestación de aplicación. Estuvo dentro
/// del repositorio hasta JTT-1401, cuando las reglas del reintento y de las familias de error
/// habrían quedado sepultadas en la capa de persistencia, donde nadie las busca.
/// </para>
/// <para>
/// <b>Las tres condiciones del CA 1 se exigen juntas</b> —enlace con Jacob, sesión válida y
/// permiso—, y ninguna se da por supuesta a partir de otra.
/// </para>
/// </remarks>
public sealed class SincronizarIncidencias : ISincronizadorIncidencias
{
	private readonly ISyncQueueService _cola;
	private readonly IIncidenciasJacobClient _jacob;
	private readonly IConnectivityService _conectividad;
	private readonly ITokenProvider _tokens;
	private readonly CapacidadesDeLaSesion _capacidades;
	private readonly IClock _reloj;
	private readonly IAuditLog _bitacora;
	private readonly ICatalogoRepository _catalogo;

	public SincronizarIncidencias(
		ISyncQueueService cola,
		IIncidenciasJacobClient jacob,
		IConnectivityService conectividad,
		ITokenProvider tokens,
		CapacidadesDeLaSesion capacidades,
		IClock reloj,
		IAuditLog bitacora,
		ICatalogoRepository catalogo)
	{
		_cola = cola;
		_jacob = jacob;
		_conectividad = conectividad;
		_tokens = tokens;
		_capacidades = capacidades;
		_reloj = reloj;
		_bitacora = bitacora;
		_catalogo = catalogo;
	}

	/// <summary>
	/// Intenta enviar lo pendiente y devuelve cuántos confirmó Jacob.
	/// </summary>
	/// <inheritdoc />
	public async Task<ResultadoSincronizacion> EjecutarAsync(CancellationToken cancelacion = default)
	{
		var bloqueo = await ComprobarCondicionesAsync(cancelacion);
		if (bloqueo is not null)
		{
			return new ResultadoSincronizacion(0, 0, bloqueo);
		}

		var token = await _tokens.ObtenerAsync(cancelacion);
		if (string.IsNullOrWhiteSpace(token))
		{
			return new ResultadoSincronizacion(0, 0, MotivoNoSincroniza.SinSesion);
		}

		var enviables = await _cola.ObtenerEnviablesAsync(cancelacion);

		// El catálogo se lee una vez por sincronización, no por registro: una consulta por
		// incidencia sobre una cola larga es gasto puro, y a media tanda no va a cambiar.
		var catalogos = await _catalogo.ObtenerAsync(cancelacion);

		var confirmados = 0;
		var intentados = 0;
		ResultadoEnvio? ultimoRechazo = null;

		foreach (var incidencia in enviables)
		{
			cancelacion.ThrowIfCancellationRequested();

			if (!TocaIntentar(incidencia))
			{
				continue;
			}

			intentados++;

			// Cada registro es su propia unidad: se marca, se envía y se resuelve antes de
			// pasar al siguiente (CA 12). Así una falla no arrastra a las demás (CA 13).
			var envio = await IntentarUnaAsync(incidencia, catalogos, token, cancelacion);

			if (envio is { Exito: true })
			{
				confirmados++;
			}
			else if (envio is not null)
			{
				ultimoRechazo = envio;
			}
		}

		await _bitacora.RegistrarAsync(
			NivelAuditoria.Info,
			$"Sync intentado: {confirmados}/{intentados} registros creados en Incidencias.",
			cancelacion);

		return new ResultadoSincronizacion(
			confirmados, intentados, null, ultimoRechazo?.Familia, ultimoRechazo?.Mensaje);
	}

	/// <inheritdoc />
	public async Task<ResultadoSincronizacion> EnviarUnaAsync(
		string claveLocal,
		CancellationToken cancelacion = default)
	{
		// Las mismas compuertas que el envío de la cola: capturar no autoriza más que
		// sincronizar, y sin enlace tampoco hay a dónde mandar.
		var bloqueo = await ComprobarCondicionesAsync(cancelacion);
		if (bloqueo is not null)
		{
			return new ResultadoSincronizacion(0, 0, bloqueo);
		}

		var token = await _tokens.ObtenerAsync(cancelacion);
		if (string.IsNullOrWhiteSpace(token))
		{
			return new ResultadoSincronizacion(0, 0, MotivoNoSincroniza.SinSesion);
		}

		var incidencia = await _cola.ObtenerEnviablePorClaveAsync(claveLocal, cancelacion);
		if (incidencia is null)
		{
			// No existe, es de otro operador o ya salió. Nada que hacer, y no es un error:
			// lo guardado sigue en la cola y se atenderá por el camino normal.
			return new ResultadoSincronizacion(0, 0, null);
		}

		var catalogos = await _catalogo.ObtenerAsync(cancelacion);
		var envio = await IntentarUnaAsync(incidencia, catalogos, token, cancelacion);

		return new ResultadoSincronizacion(
			envio is { Exito: true } ? 1 : 0,
			1,
			null,
			envio?.Exito == false ? envio.Familia : null,
			envio?.Exito == false ? envio.Mensaje : null);
	}

	/// <summary>
	/// Comprueba las tres condiciones del CA 1, o devuelve cuál falta.
	/// </summary>
	/// <remarks>
	/// <b>El CA 2 está aquí, en el orden de las preguntas.</b> No basta con que exista WiFi o red
	/// móvil: se pregunta por el enlace <b>con Jacob</b>, que es una sonda autenticada y no el
	/// estado de la radio. Un punto de acceso de carretera sin salida a internet contesta que sí
	/// hay red, y sincronizar contra él solo quema intentos.
	/// </remarks>
	private async Task<MotivoNoSincroniza?> ComprobarCondicionesAsync(CancellationToken cancelacion)
	{
		if (!_capacidades.Puede(CapacidadOperador.Sincronizar))
		{
			// No se registra en bitácora: sin permiso esto se repite en cada intento y llenaría
			// la traza de ruido idéntico.
			return MotivoNoSincroniza.SinPermiso;
		}

		if (!_conectividad.HayEnlace)
		{
			await _bitacora.RegistrarAsync(
				NivelAuditoria.Advertencia,
				"Sync cancelado: sin conexion.",
				cancelacion);
			return MotivoNoSincroniza.SinEnlaceConJacob;
		}

		return null;
	}

	/// <summary>
	/// Decide si un registro toca ahora, sin llegar a tocar la red.
	/// </summary>
	/// <remarks>
	/// Dos motivos para saltárselo, y son distintos:
	/// <list type="bullet">
	/// <item>
	/// <b>Falló por algo funcional</b> (CA 8): reenviarlo igual daría el mismo rechazo. Espera a
	/// que alguien lo corrija. <b>No se descarta</b> —JTT-1404 CA 4—, solo deja de reintentarse.
	/// </item>
	/// <item>
	/// <b>Falló por algo técnico y su espera no ha vencido</b> (CA 7).
	/// </item>
	/// </list>
	/// </remarks>
	private bool TocaIntentar(IncidenciaEnviable incidencia)
	{
		if (CodigosErrorJacob.EsFuncional(incidencia.UltimoErrorCodigo))
		{
			return false;
		}

		if (incidencia.Estado != EstadoSincronizacion.Fallido)
		{
			return true;
		}

		var transcurrido = _reloj.UtcAhora - incidencia.UltimoIntentoUtc;
		return ReglaEsperaReintento.YaPuedeReintentarse(incidencia.Intentos, transcurrido);
	}

	/// <summary>Marca, envía y resuelve un registro. Devuelve lo que contestó Jacob.</summary>
	private async Task<ResultadoEnvio?> IntentarUnaAsync(
		IncidenciaEnviable incidencia,
		CatalogosOperacion catalogos,
		string token,
		CancellationToken cancelacion)
	{
		// Fallido vuelve a Pendiente antes de poder pasar a Enviando: es el grafo de la regla
		// de dominio, no un paso de más.
		var estadoPrevio = incidencia.Estado == EstadoSincronizacion.Fallido
			? EstadoSincronizacion.Pendiente
			: incidencia.Estado;

		if (!ReglaTransicionSincronizacion.EsTransicionValida(estadoPrevio, EstadoSincronizacion.Enviando))
		{
			return null;
		}

		var intentos = incidencia.Intentos + 1;

		await _cola.ActualizarEnvioAsync(
			new ActualizacionEnvio(
				incidencia.Uuid, EstadoSincronizacion.Enviando, intentos, null, incidencia.UltimoErrorCodigo),
			cancelacion);

		var envio = RellenoCamposNoCapturados.Completar(incidencia, catalogos);
		var resultado = await _jacob.RegistrarAsync(envio, token, cancelacion);

		var destino = resultado.Exito ? EstadoSincronizacion.Sincronizado : EstadoSincronizacion.Fallido;

		await _cola.ActualizarEnvioAsync(
			new ActualizacionEnvio(
				incidencia.Uuid,
				destino,
				intentos,
				resultado.Registrada?.Folio,
				resultado.Codigo),
			cancelacion);

		await _cola.RegistrarIntentoAsync(
			incidencia.Uuid, resultado.Exito, resultado.Codigo, resultado.Mensaje, cancelacion);

		return resultado;
	}
}
