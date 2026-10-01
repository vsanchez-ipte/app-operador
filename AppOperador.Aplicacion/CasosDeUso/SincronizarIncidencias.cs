using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;

namespace AppOperador.Aplicacion.CasosDeUso;

// Orquestación de aplicación: las reglas de reintento no se entierran en la persistencia.
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
	private readonly ISessionStore _sesiones;

	// Tres disparadores que no saben uno del otro; el intento tardío se rechaza en vez de encolarse.
	private readonly SemaphoreSlim _unaTandaALaVez = new(1, 1);

	// Lo enciende un envío inmediato que encontró la tanda ocupada; la tanda relee la cola al terminar.
	private int _llegoAlgoDuranteLaTanda;

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
		IEvidenciasJacobClient clienteEvidencias,
		ISessionStore sesiones)
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
		_sesiones = sesiones;
	}

	public async Task<ResultadoSincronizacion> EjecutarAsync(CancellationToken cancelacion = default)
	{
		if (!await _unaTandaALaVez.WaitAsync(0, cancelacion))
		{
			return new ResultadoSincronizacion(0, 0, MotivoNoSincroniza.YaEnCurso);
		}

		ResultadoSincronizacion resultado;
		try
		{
			resultado = await EjecutarTandaAsync(cancelacion);
		}
		finally
		{
			_unaTandaALaVez.Release();
		}

		// La señal pudo encenderse antes de soltar el cerrojo: se mira una vez más y corre una sola tanda extra.
		if (Interlocked.Exchange(ref _llegoAlgoDuranteLaTanda, 0) == 1
			&& await _unaTandaALaVez.WaitAsync(0, cancelacion))
		{
			try
			{
				var extra = await EjecutarTandaAsync(cancelacion);
				resultado = resultado with
				{
					Confirmados = resultado.Confirmados + extra.Confirmados,
					Intentados = resultado.Intentados + extra.Intentados,
				};
			}
			finally
			{
				_unaTandaALaVez.Release();
			}
		}

		return resultado;
	}

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

		// Antes de leer la cola: un registro atrapado en Enviando no lo tomaría nadie.
		var recuperados = await _cola.RecuperarEnviosInterrumpidosAsync(cancelacion);
		if (recuperados > 0)
		{
			await _bitacora.RegistrarAsync(
				OperacionAuditada.RecuperacionPendientes, ResultadoAuditoria.Exito,
				$"Se recuperaron {recuperados} envíos interrumpidos que quedaron en Enviando.",
				cancelacion: cancelacion);
		}

		// Una vez por tanda: a media tanda no cambia.
		var catalogos = await _catalogo.ObtenerAsync(cancelacion);

		Interlocked.Exchange(ref _llegoAlgoDuranteLaTanda, 0);
		var cuenta = new CuentaDeTanda();

		// Dos pasadas como mucho, para que una captura tras otra no mantenga la tanda corriendo sin fin.
		for (var pasada = 0; pasada < 2; pasada++)
		{
			var enviables = await _cola.ObtenerEnviablesAsync(cancelacion);
			await RecorrerAsync(enviables, catalogos, token, cuenta, cancelacion);

			if (Interlocked.Exchange(ref _llegoAlgoDuranteLaTanda, 0) == 0)
			{
				break;
			}
		}

		await ReintentarEvidenciasRezagadasAsync(token, cancelacion);

		await _bitacora.RegistrarAsync(
			OperacionAuditada.Sincronizacion, ResultadoAuditoria.Exito,
			$"Sync intentado: {cuenta.Confirmados}/{cuenta.Intentados} registros creados en Incidencias.",
			cancelacion: cancelacion);

		return new ResultadoSincronizacion(
			cuenta.Confirmados, cuenta.Intentados, null,
			cuenta.UltimoRechazo?.Familia, cuenta.UltimoRechazo?.Mensaje,
			cuenta.EnEspera, cuenta.PorCorregir);
	}

	private async Task RecorrerAsync(
		IReadOnlyList<IncidenciaEnviable> enviables,
		CatalogosOperacion catalogos,
		string token,
		CuentaDeTanda cuenta,
		CancellationToken cancelacion)
	{
		foreach (var incidencia in enviables)
		{
			cancelacion.ThrowIfCancellationRequested();

			if (!TocaIntentar(incidencia))
			{
				// Espera sola y necesita corrección son cosas distintas para quien mira la cola.
				if (CodigosErrorJacob.EsFuncional(incidencia.UltimoErrorCodigo))
				{
					cuenta.PorCorregir++;
				}
				else
				{
					cuenta.EnEspera++;
				}

				continue;
			}

			cuenta.Intentados++;

			// Cada registro se resuelve antes del siguiente, para que una falla no arrastre a las demás.
			var envio = await IntentarUnaAsync(incidencia, catalogos, token, cancelacion);

			if (envio is { Exito: true })
			{
				cuenta.Confirmados++;
			}
			else if (envio is not null)
			{
				cuenta.UltimoRechazo = envio;
			}
		}
	}

	private sealed class CuentaDeTanda
	{
		public int Confirmados;
		public int Intentados;
		public int EnEspera;
		public int PorCorregir;
		public ResultadoEnvio? UltimoRechazo;
	}

	public async Task<ResultadoSincronizacion> EnviarUnaAsync(
		string claveLocal,
		CancellationToken cancelacion = default)
	{
		// El mismo cerrojo que la tanda: el registro recién guardado puede ser uno de los que ella recorre.
		if (!await _unaTandaALaVez.WaitAsync(0, cancelacion))
		{
			// Queda Pendiente y la tanda en curso lo saca al releer la cola.
			Interlocked.Exchange(ref _llegoAlgoDuranteLaTanda, 1);
			return new ResultadoSincronizacion(0, 0, MotivoNoSincroniza.YaEnCurso);
		}

		ResultadoSincronizacion resultado;
		try
		{
			resultado = await EnviarSoloEsaAsync(claveLocal, cancelacion);
		}
		finally
		{
			_unaTandaALaVez.Release();
		}

		// A otra captura se le prometió salir en la sincronización en curso: se cumple aquí.
		if (Interlocked.Exchange(ref _llegoAlgoDuranteLaTanda, 0) == 1)
		{
			await EjecutarAsync(cancelacion);
		}

		return resultado;
	}

	private async Task<ResultadoSincronizacion> EnviarSoloEsaAsync(
		string claveLocal,
		CancellationToken cancelacion)
	{
		// Las mismas compuertas que la cola: capturar no autoriza más que sincronizar.
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
			// No existe, es de otro operador o ya salió; no es un error.
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

	// Enlace con Jacob y no solo red: un punto de acceso sin salida a internet dice que hay red.
	private async Task<MotivoNoSincroniza?> ComprobarCondicionesAsync(CancellationToken cancelacion)
	{
		if (!_capacidades.Puede(CapacidadOperador.Sincronizar))
		{
			// Sin bitácora: sin permiso se repetiría en cada intento.
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

	// Un rechazo funcional espera corrección y no se descarta; uno técnico espera a que venza su plazo.
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

	private async Task<ResultadoEnvio?> IntentarUnaAsync(
		IncidenciaEnviable incidencia,
		CatalogosOperacion catalogos,
		string token,
		CancellationToken cancelacion)
	{
		// Fallido pasa por Pendiente antes de Enviando, según el grafo de la regla de dominio.
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
			// Se resuelve aquí para no dejarlo en Enviando, y como técnico: no se pudo preguntar, el registro no está mal.
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

		// Después y solo si confirmó: el servidor rechaza la evidencia de una incidencia que no existe.
		if (resultado.Exito)
		{
			await EnviarEvidenciasDeAsync(incidencia.Uuid, token, cancelacion);
		}

		return resultado;
	}

	// Una evidencia que falle no revierte la incidencia ni detiene a las siguientes.
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

			// Lo funcional no se reintenta: daría el mismo rechazo y gastaría datos.
			if (CodigosErrorJacob.EsFuncional(evidencia.UltimoErrorCodigo))
			{
				continue;
			}

			await SubirEvidenciaAsync(evidencia, token, cancelacion);
		}
	}

	private async Task ReintentarEvidenciasRezagadasAsync(string token, CancellationToken cancelacion)
	{
		var operador = _sesiones.Actual?.Operador;
		if (string.IsNullOrWhiteSpace(operador))
		{
			return;
		}

		var rezagadas = await _evidencias.ObtenerRezagadasDelOperadorAsync(operador, cancelacion);
		var ahora = _reloj.UtcAhora;
		var intentadas = 0;
		var subidas = 0;

		foreach (var rezagada in rezagadas)
		{
			cancelacion.ThrowIfCancellationRequested();

			if (!rezagada.TocaIntentar(ahora))
			{
				continue;
			}

			intentadas++;
			if (await SubirEvidenciaAsync(rezagada.Evidencia, token, cancelacion))
			{
				subidas++;
			}
		}

		if (intentadas > 0)
		{
			await _bitacora.RegistrarAsync(
				OperacionAuditada.Sincronizacion, ResultadoAuditoria.Exito,
				$"Evidencias reintentadas: {subidas}/{intentadas} llegaron al CCO.",
				cancelacion: cancelacion);
		}
	}

	private async Task<bool> SubirEvidenciaAsync(
		EvidenciaAdjunta evidencia,
		string token,
		CancellationToken cancelacion)
	{
		var resultado = await _clienteEvidencias.SubirAsync(
			evidencia.IncidenciaUuid,
			evidencia.RutaArchivo,
			evidencia.NombreOriginal,
			token,
			cancelacion);

		var destino = resultado.Exito
			? EstadoSincronizacion.Sincronizado
			: EstadoSincronizacion.Fallido;

		await _evidencias.ActualizarEnvioAsync(
			evidencia.Uuid, destino, resultado.Codigo, resultado.Mensaje, cancelacion);

		if (!resultado.Exito)
		{
			await _bitacora.RegistrarAsync(
				NivelAuditoria.Advertencia,
				$"Evidencia {evidencia.NombreOriginal} no llegó al CCO: " +
				$"{resultado.Mensaje ?? "sin mensaje"} [{resultado.Codigo ?? "sin código"}]",
				cancelacion);
		}

		return resultado.Exito;
	}
}
