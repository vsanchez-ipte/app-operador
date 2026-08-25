using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;

namespace AppOperador.Infrastructure.Http;

/// <summary>
/// Registra incidencias en el canal móvil de Jacob CCO (JTT-1401).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>La respuesta va envuelta SIEMPRE, en éxito y en error.</b> El §3 del contrato dice que
/// el éxito llega sin sobre, y es falso: <c>BaseController.FromResult</c> devuelve un
/// <c>Envelope</c> con <c>resultado</c>, <c>codigoError</c> y <c>mensajeError</c> en los dos
/// casos. Comprobado el 21-ago contra el API corriendo. Buscar <c>folio</c> en la raíz hacía
/// que una incidencia creada con éxito se marcara como fallida.
/// </para>
/// <para>
/// <b>Nunca lanza por un rechazo.</b> Los fallos de red y los tiempos agotados también salen como
/// resultado técnico: quien orquesta necesita seguir con el resto de la cola, y una excepción
/// aquí obligaría a envolver cada envío en un <c>try</c> allá.
/// </para>
/// </remarks>
public sealed class ClienteIncidenciasJacob : IIncidenciasJacobClient
{
	private static readonly JsonSerializerOptions OpcionesJson = new()
	{
		PropertyNameCaseInsensitive = true,
	};

	/// <summary>Código con el que se reportan los fallos que no traen uno del servidor.</summary>
	private const string CodigoFalloLocal = "app.envio.error.tecnico";

	private readonly HttpClient _http;
	private readonly ConfiguracionApi _configuracion;

	public ClienteIncidenciasJacob(HttpClient http, ConfiguracionApi configuracion)
	{
		_http = http;
		_configuracion = configuracion;
	}

	/// <inheritdoc />
	public async Task<ResultadoEnvio> RegistrarAsync(
		EnvioIncidencia incidencia,
		string accessToken,
		CancellationToken cancelacion = default)
	{
		ArgumentNullException.ThrowIfNull(incidencia);

		if (string.IsNullOrWhiteSpace(accessToken))
		{
			return Tecnico(CodigoFalloLocal, "No hay sesión con la que enviar.");
		}

		try
		{
			using var peticion = new HttpRequestMessage(
				HttpMethod.Post,
				new Uri(new Uri(_configuracion.UrlBase), ConfiguracionApi.RutaIncidencias));

			peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
			peticion.Content = JsonContent.Create(ACuerpo(incidencia));

			using var respuesta = await _http.SendAsync(peticion, cancelacion).ConfigureAwait(false);
			var cuerpo = await respuesta.Content.ReadAsStringAsync(cancelacion).ConfigureAwait(false);

			return respuesta.IsSuccessStatusCode
				? LeerAceptada(cuerpo)
				: LeerRechazo(cuerpo);
		}
		catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
		{
			// Cancelación pedida por quien llama, no un fallo del envío: se propaga.
			throw;
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
		{
			// Red caída, servidor inalcanzable, tiempo agotado o respuesta ilegible. Todo eso es
			// técnico: el registro está bien y el problema es del camino.
			return Tecnico(CodigoFalloLocal, "No se pudo contactar al CCO. Se reintentará.");
		}
	}

	/// <summary>
	/// Traduce el envío al cuerpo que espera el API, en camelCase.
	/// </summary>
	/// <remarks>
	/// <b>El kilómetro y las fechas son lo delicado.</b> El decimal viaja en cultura invariante,
	/// porque con una cultura de coma decimal <c>130.200</c> se serializaría de forma que el
	/// servidor lo leería como otro número. Las fechas van en UTC con <c>Z</c>, que es lo que el
	/// contrato pide.
	/// </remarks>
	private static object ACuerpo(EnvioIncidencia incidencia) => new
	{
		uuid = incidencia.Uuid,
		idTipoIncidencia = incidencia.IdTipoIncidencia,
		idGravedad = incidencia.IdGravedad,
		idAfectacion = incidencia.IdAfectacion,
		km = incidencia.Km,
		fuenteKilometro = incidencia.FuenteKilometro,
		latitud = incidencia.Latitud,
		longitud = incidencia.Longitud,
		cuerpo = incidencia.Cuerpo,
		nota = incidencia.Nota,
		fchCapturaCampo = incidencia.FchCapturaCampo.ToString("O", CultureInfo.InvariantCulture),
		idSesionOrigen = incidencia.IdSesionOrigen,
	};

	/// <summary>
	/// Lee la respuesta de éxito.
	/// </summary>
	/// <remarks>
	/// ⚠️ <b>Llega envuelta, al contrario de lo que dice el contrato.</b> El §3 de
	/// <c>02-contrato-api-canal-movil.md</c> afirma que en éxito el cuerpo es el objeto pelón y
	/// que solo el error trae sobre. <b>No es cierto:</b> <c>BaseController.FromResult</c>
	/// devuelve siempre un <c>Envelope</c> con <c>resultado</c>, <c>codigoError</c> y
	/// <c>mensajeError</c>, y se comprobó el 21-ago contra el API corriendo.
	/// <para>
	/// Costó un diagnóstico entero: la incidencia se creaba en Jacob con su folio, el API
	/// respondía 200, y la app la marcaba fallida porque buscaba <c>folio</c> en la raíz.
	/// </para>
	/// <para>
	/// Se aceptan las dos formas a propósito —con sobre y sin él—: si algún día el API se
	/// alinea con el contrato, la app no se rompe.
	/// </para>
	/// </remarks>
	private static ResultadoEnvio LeerAceptada(string cuerpo)
	{
		var dto = Deserializar<SobreRespuestaDto>(cuerpo)?.Resultado
			?? Deserializar<RespuestaIncidenciaDto>(cuerpo);

		// Un 200 sin folio no es un éxito utilizable: sin folio la incidencia queda marcada
		// como sincronizada y sin la referencia que el operador necesita para dictarla.
		if (dto is null || string.IsNullOrWhiteSpace(dto.Folio))
		{
			return Tecnico(CodigoFalloLocal, "El CCO respondió sin folio.");
		}

		return ResultadoEnvio.Aceptada(new IncidenciaRegistrada(
			dto.Folio,
			dto.FchRecepcionCentral ?? DateTime.UtcNow,
			dto.YaExistia));
	}

	/// <summary>Lee la respuesta de error, que sí llega envuelta.</summary>
	private static ResultadoEnvio LeerRechazo(string cuerpo)
	{
		var sobre = Deserializar<SobreErrorDto>(cuerpo);
		var codigo = sobre?.CodigoError;

		if (string.IsNullOrWhiteSpace(codigo))
		{
			// Rechazo sin código: no se puede clasificar, y lo prudente es reintentar.
			return Tecnico(CodigoFalloLocal, sobre?.MensajeError ?? "El CCO rechazó el envío.");
		}

		var mensaje = sobre?.MensajeError ?? "El CCO rechazó el envío.";

		// La familia la decide el catálogo de códigos, no este cliente: qué se reintenta es
		// una decisión de aplicación y no de transporte.
		return ResultadoEnvio.Rechazada(CodigosErrorJacob.FamiliaDe(codigo), codigo, mensaje);
	}

	private static ResultadoEnvio Tecnico(string codigo, string mensaje) =>
		ResultadoEnvio.Rechazada(FamiliaErrorSincronizacion.Tecnico, codigo, mensaje);

	private static T? Deserializar<T>(string cuerpo)
		where T : class
	{
		if (string.IsNullOrWhiteSpace(cuerpo))
		{
			return null;
		}

		try
		{
			return JsonSerializer.Deserialize<T>(cuerpo, OpcionesJson);
		}
		catch (JsonException)
		{
			return null;
		}
	}

	private sealed class RespuestaIncidenciaDto
	{
		public string? Folio { get; set; }

		public DateTime? FchCapturaCampo { get; set; }

		public DateTime? FchRecepcionCentral { get; set; }

		public bool YaExistia { get; set; }
	}

	/// <summary>El sobre que devuelve <c>BaseController.FromResult</c> en éxito.</summary>
	private sealed class SobreRespuestaDto
	{
		public RespuestaIncidenciaDto? Resultado { get; set; }
	}

	private sealed class SobreErrorDto
	{
		public string? CodigoError { get; set; }

		public string? MensajeError { get; set; }
	}
}
