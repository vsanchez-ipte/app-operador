using System.Net.Http.Headers;
using System.Text.Json;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.ValueObjects;
using AppOperador.Infrastructure.Http.Dtos;

namespace AppOperador.Infrastructure.Http;

// Exige el permiso general, no el de captura: sin captura también se debe poder abrir el formulario.
public sealed class ClienteCatalogosJacob : ICatalogosJacobClient
{
	private static readonly JsonSerializerOptions OpcionesJson = new()
	{
		PropertyNameCaseInsensitive = true,
	};

	private readonly HttpClient _http;
	private readonly ConfiguracionApi _configuracion;

	public ClienteCatalogosJacob(HttpClient http, ConfiguracionApi configuracion)
	{
		_http = http;
		_configuracion = configuracion;
	}

	public async Task<CatalogosOperacion?> ObtenerVigentesAsync(
		string accessToken,
		CancellationToken cancelacion = default)
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			return null;
		}

		try
		{
			using var peticion = new HttpRequestMessage(
				HttpMethod.Get,
				new Uri(new Uri(_configuracion.UrlBase), ConfiguracionApi.RutaCatalogos));

			peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

			using var respuesta = await _http.SendAsync(peticion, cancelacion).ConfigureAwait(false);
			var cuerpo = await respuesta.Content.ReadAsStringAsync(cancelacion).ConfigureAwait(false);

			var sobre = JsonSerializer.Deserialize<Envelope<RespuestaCatalogos>>(cuerpo, OpcionesJson);

			if (sobre is null || sobre.HayError || sobre.Resultado is null)
			{
				return null;
			}

			return Convertir(sobre.Resultado);
		}
		catch (Exception excepcion) when (
			FalloDeComunicacion.Es(excepcion) || excepcion is JsonException or UriFormatException)
		{
			// Sin red o con respuesta ilegible se sigue con la copia local.
			return null;
		}
		catch (TaskCanceledException) when (!cancelacion.IsCancellationRequested)
		{
			return null;
		}
	}

	// Descarta entradas incompletas; sin tipos o severidades devuelve null para no pisar la copia local.
	private static CatalogosOperacion? Convertir(RespuestaCatalogos respuesta)
	{
		var tipos = (respuesta.Tipos ?? [])
			.Where(t => t.Id is > 0 && !string.IsNullOrWhiteSpace(t.Nombre))
			.Select(t => new TipoIncidencia(t.Id!.Value, t.Nombre!.Trim(), t.ExigeDescripcion ?? false))
			.ToList();

		var severidades = (respuesta.Severidades ?? [])
			.Where(s => s.Id is not null && s.Id != Guid.Empty && !string.IsNullOrWhiteSpace(s.Nivel))
			.Select(s => new SeveridadIncidencia(
				s.Id!.Value,
				s.Nivel!.Trim(),
				s.Orden ?? int.MaxValue,
				(s.Hexadecimal ?? string.Empty).Trim()))
			.OrderBy(s => s.Orden)
			.ToList();

		var afectaciones = (respuesta.Afectaciones ?? [])
			.Where(a => a.Id is > 0 && !string.IsNullOrWhiteSpace(a.Nombre))
			.Select(a => new AfectacionIncidencia(a.Id!.Value, a.Nombre!.Trim()))
			.ToList();

		var cuerpos = (respuesta.Cuerpos ?? [])
			.Where(c => !string.IsNullOrWhiteSpace(c.Clave) && !string.IsNullOrWhiteSpace(c.Nombre))
			.Select(c => new CuerpoVia(c.Clave!.Trim(), c.Nombre!.Trim()))
			.ToList();

		var catalogo = new CatalogosOperacion(
			respuesta.Version ?? DateOnly.FromDateTime(DateTime.UtcNow),
			tipos,
			severidades,
			afectaciones,
			cuerpos,
			ConvertirLimites(respuesta.LimitesEvidencia));

		return catalogo.EsUtilizable ? catalogo : null;
	}

	// Todo o nada: la app nunca completa un límite con un número propio.
	private static LimitesEvidencia ConvertirLimites(LimitesEvidenciaCatalogo? limites)
	{
		if (limites is null)
		{
			return LimitesEvidencia.Desconocidos;
		}

		var formatos = (limites.FormatosPermitidos ?? [])
			.Where(f => !string.IsNullOrWhiteSpace(f))
			.Select(f => f.Trim())
			.ToList();

		var convertidos = new LimitesEvidencia(
			formatos,
			limites.TamanoMaximoMb ?? 0,
			limites.MaximoArchivosPorIncidencia ?? 0);

		return convertidos.EstanDefinidos ? convertidos : LimitesEvidencia.Desconocidos;
	}
}
