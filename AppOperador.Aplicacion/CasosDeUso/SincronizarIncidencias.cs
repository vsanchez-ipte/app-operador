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
	private readonly IRepositorioEvidencias _evidencias;
	private readonly IEvidenciasJacobClient _clienteEvidencias;

	/// <summary>
	/// Deja pasar una sola sincronización a la vez (JTT-1406 CA 6).
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Hay tres disparadores y ninguno sabe de los otros:</b> el botón de la pantalla de Cola,
	/// la revalidación de sesión y —desde esta historia— la recuperación del enlace. Solapar dos
	/// tandas no duplicaría incidencias, porque el alta es idempotente por <c>uuid</c>, pero sí
	/// haría que las dos leyeran la misma cola y se pisaran las transiciones de estado: la
	/// segunda podría devolver a <c>Pendiente</c> un registro que la primera acaba de poner en
	/// <c>Enviando</c>, y contarlo dos veces en el aviso.
	/// </para>
	/// <para>
	/// <b>Se toma sin esperar y se rechaza el intento tardío, en vez de encolarlo.</b> Encolarlo
	/// dejaría al operador mirando un botón ocupado para que después corriera una tanda sobre una
	/// cola que la primera ya vació. Nada se pierde: lo pendiente lo está atendiendo la tanda que
	/// ya corre, y lo que llegue después sale en la siguiente.
	/// </para>
	/// </remarks>
	private readonly SemaphoreSlim _unaTandaALaVez = new(1, 1);

	public SincronizarIncidencias(
		ISyncQueueService cola,
		IIncidenciasJacobClient jacob,
		IConnectivityService conectividad,
		ITokenProvider tokens,
		CapacidadesDeLaSesion capacidades,
		IClock reloj,
		IAuditLog bitacora,
		ICatalogoRepository catalogo,
		IRepositorioEvidencias evidencias,
		IEvidenciasJacobClient clienteEvidencias)
	{
		_cola = cola;
		_jacob = jacob;
		_conectividad = conectividad;
		_tokens = tokens;
		_capacidades = capacidades;
		_reloj = reloj;
		_bitacora = bitacora;
		_catalogo = catalogo;
		_evidencias = evidencias;
		_clienteEvidencias = clienteEvidencias;
	}

	/// <summary>
	/// Intenta enviar lo pendiente y devuelve cuántos confirmó Jacob.
	/// </summary>
	/// <inheritdoc />
	public async Task<ResultadoSincronizacion> EjecutarAsync(CancellationToken cancelacion = default)
	{
		if (!await _unaTandaALaVez.WaitAsync(0, cancelacion))
		{
			return new ResultadoSincronizacion(0, 0, MotivoNoSincroniza.YaEnCurso);
		}

		try
		{
			return await EjecutarTandaAsync(cancelacion);
		}
		finally
		{
			_unaTandaALaVez.Release();
		}
	}

	/// <summary>Recorre la cola. Ya con la exclusión tomada.</summary>
	private async Task<ResultadoSincronizacion> EjecutarTandaAsync(CancellationToken cancelacion)
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

		// Antes de leer la cola: lo que quedó a medio enviar vuelve a Pendiente. Si no, un
		// registro atrapado en Enviando no lo toma nadie —ni esta consulta ni el contador— y no
		// llega nunca a Jacob, sin que el operador tenga forma de saberlo.
		var recuperados = await _cola.RecuperarEnviosInterrumpidosAsync(cancelacion);
		if (recuperados > 0)
		{
			await _bitacora.RegistrarAsync(
				NivelAuditoria.Advertencia,
				$"Se recuperaron {recuperados} envíos interrumpidos que quedaron en Enviando.",
				cancelacion);
		}

		var enviables = await _cola.ObtenerEnviablesAsync(cancelacion);

		// El catálogo se lee una vez por sincronización, no por registro: una consulta por
		// incidencia sobre una cola larga es gasto puro, y a media tanda no va a cambiar.
		var catalogos = await _catalogo.ObtenerAsync(cancelacion);

		var confirmados = 0;
		var intentados = 0;
		var enEspera = 0;
		var porCorregir = 0;
		ResultadoEnvio? ultimoRechazo = null;

		foreach (var incidencia in enviables)
		{
			cancelacion.ThrowIfCancellationRequested();

			if (!TocaIntentar(incidencia))
			{
				// Se cuenta por qué se salta, no solo que se saltó: «espera sola» y «necesita
				// corrección» son cosas distintas para quien está mirando la cola.
				if (CodigosErrorJacob.EsFuncional(incidencia.UltimoErrorCodigo))
				{
					porCorregir++;
				}
				else
				{
					enEspera++;
				}

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
			confirmados, intentados, null,
			ultimoRechazo?.Familia, ultimoRechazo?.Mensaje,
			enEspera, porCorregir);
	}

	/// <inheritdoc />
	public async Task<ResultadoSincronizacion> EnviarUnaAsync(
		string claveLocal,
		CancellationToken cancelacion = default)
	{
		// Comparte la exclusión con la tanda completa, y no una propia: el registro que el
		// operador acaba de guardar puede ser justo uno de los que la tanda está recorriendo.
		// Con dos cerrojos distintos, los dos caminos escribirían su estado a la vez.
		if (!await _unaTandaALaVez.WaitAsync(0, cancelacion))
		{
			// Se queda como Pendiente y sale en la tanda que ya corre o en la siguiente. Para el
			// operador es lo mismo que no haber tenido enlace: guardada y en camino.
			return new ResultadoSincronizacion(0, 0, MotivoNoSincroniza.YaEnCurso);
		}

		try
		{
			return await EnviarSoloEsaAsync(claveLocal, cancelacion);
		}
		finally
		{
			_unaTandaALaVez.Release();
		}
	}

	/// <summary>Envía un registro concreto. Ya con la exclusión tomada.</summary>
	private async Task<ResultadoSincronizacion> EnviarSoloEsaAsync(
		string claveLocal,
		CancellationToken cancelacion)
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

		ResultadoEnvio resultado;
		try
		{
			resultado = await _jacob.RegistrarAsync(envio, token, cancelacion);
		}
		catch (Exception excepcion) when (excepcion is not OperationCanceledException)
		{
			// El registro ya está marcado como Enviando. Si la excepción se propagara desde
			// aquí, se quedaría ahí colgado hasta la siguiente recuperación; resolverlo en el
			// acto lo devuelve al camino normal de reintentos.
			//
			// Técnico y no funcional: una excepción del cliente no dice que el registro esté
			// mal, dice que no se pudo preguntar. Como funcional, dejaría de reintentarse por
			// un fallo que el operador no puede corregir.
			resultado = ResultadoEnvio.Rechazada(
				FamiliaErrorSincronizacion.Tecnico,
				CodigosErrorJacob.ErrorTecnico,
				"No se pudo completar el envío. Se reintentará solo.");

			await _bitacora.RegistrarAsync(
				NivelAuditoria.Advertencia,
				$"Envío interrumpido por excepción: {excepcion.GetType().Name}.",
				cancelacion);
		}

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

		// Las evidencias van DESPUÉS y solo si la incidencia confirmó: el servidor valida que el
		// uuid exista, y una evidencia que salga antes recibe appevidencias.incidencia.noexiste.
		if (resultado.Exito)
		{
			await EnviarEvidenciasDeAsync(incidencia.Uuid, token, cancelacion);
		}

		return resultado;
	}

	/// <summary>
	/// Sube las evidencias pendientes de una incidencia ya confirmada (JTT-1398 CA 11).
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Una evidencia que falle no revierte la incidencia.</b> Ya está en el CCO con su folio,
	/// y marcarla como fallida por una foto que no subió mandaría al operador a recapturar algo
	/// que sí llegó. Cada evidencia lleva su propio estado y su propio reintento, que es para lo
	/// que <c>evidencia_local</c> se diseñó así desde el primer esquema.
	/// </para>
	/// <para>
	/// <b>Y una que falle no detiene a las siguientes</b>, por el mismo argumento del CA 13 de
	/// JTT-1401: que la tercera foto no suba no puede impedir que suban la cuarta y la quinta.
	/// </para>
	/// </remarks>
	private async Task EnviarEvidenciasDeAsync(
		string incidenciaUuid,
		string token,
		CancellationToken cancelacion)
	{
		var pendientes = await _evidencias.ObtenerPendientesDeIncidenciaAsync(
			incidenciaUuid, cancelacion);

		foreach (var evidencia in pendientes)
		{
			cancelacion.ThrowIfCancellationRequested();

			// Lo funcional no se reintenta, igual que en las incidencias: reenviar un formato
			// que el servidor no admite da el mismo rechazo y gasta datos del operador.
            if (CodigosErrorJacob.EsFuncional(evidencia.UltimoErrorCodigo))
            {
                continue;
            }

			var resultado = await _clienteEvidencias.SubirAsync(
				incidenciaUuid,
				evidencia.RutaArchivo,
				evidencia.NombreOriginal,
				token,
				cancelacion);

			// yaExistia es éxito: el servidor ya tenía este contenido para esta incidencia. La
			// subida es idempotente por contenido, así que un reintento tras una respuesta
			// perdida devuelve lo mismo sin duplicar ni gastar cupo.
			var destino = resultado.Exito
				? EstadoSincronizacion.Sincronizado
				: EstadoSincronizacion.Fallido;

			await _evidencias.ActualizarEnvioAsync(
				evidencia.Uuid, destino, resultado.Codigo, cancelacion);
		}
	}
}
