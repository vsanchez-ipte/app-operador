using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;

namespace AppOperador.Infrastructure.Http;

// Nunca lanza por un rechazo ni por la red: la cola tiene que seguir con los demás.
public sealed class ClienteIncidenciasJacob : IIncidenciasJacobClient
{
	private static readonly JsonSerializerOptions OpcionesJson = new()
	{
		PropertyNameCaseInsensitive = true,
	};

	private const string CodigoFalloLocal = "app.envio.error.tecnico";

	private readonly HttpClient _http;
	private readonly ConfiguracionApi _configuracion;

	public ClienteIncidenciasJacob(HttpClient http, ConfiguracionApi configuracion)
	{
		_http = http;
		_configuracion = configuracion;
	}

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
			throw;
		}
		catch (Exception ex) when (FalloDeComunicacion.Es(ex) || ex is TaskCanceledException or JsonException)
		{
			// Red, tiempo agotado o respuesta ilegible: el registro está bien, falló el camino.
			return Tecnico(CodigoFalloLocal, "No se pudo contactar al CCO. Se reintentará.");
		}
	}

	// Decimal en cultura invariante y fechas en UTC con Z, como pide el contrato.
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

	// El éxito también llega en Envelope, aunque el contrato diga lo contrario; se aceptan las dos formas.
	private static ResultadoEnvio LeerAceptada(string cuerpo)
	{
		var dto = Deserializar<SobreRespuestaDto>(cuerpo)?.Resultado
			?? Deserializar<RespuestaIncidenciaDto>(cuerpo);

		// Un 200 sin folio no sirve: el operador necesita el folio para dictarla.
		if (dto is null || string.IsNullOrWhiteSpace(dto.Folio))
		{
			return Tecnico(CodigoFalloLocal, "El CCO respondió sin folio.");
		}

		return ResultadoEnvio.Aceptada(new IncidenciaRegistrada(
			dto.Folio,
			dto.FchRecepcionCentral ?? DateTime.UtcNow,
			dto.YaExistia));
	}

	private static ResultadoEnvio LeerRechazo(string cuerpo)
	{
		var sobre = Deserializar<SobreErrorDto>(cuerpo);
		var codigo = sobre?.CodigoError;

		if (string.IsNullOrWhiteSpace(codigo))
		{
			// Sin código no se puede clasificar: lo prudente es reintentar.
			return Tecnico(CodigoFalloLocal, sobre?.MensajeError ?? "El CCO rechazó el envío.");
		}

		var mensaje = sobre?.MensajeError ?? "El CCO rechazó el envío.";

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
