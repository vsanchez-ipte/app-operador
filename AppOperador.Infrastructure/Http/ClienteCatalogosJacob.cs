using System.Net.Http.Headers;
using System.Text.Json;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Infrastructure.Http.Dtos;

namespace AppOperador.Infrastructure.Http;

/// <summary>
/// Descarga los catálogos vigentes del canal móvil de Jacob CCO (JTT-1394).
/// </summary>
/// <remarks>
/// <para>
/// Va aparte de <see cref="ClienteAccesoJacob"/> aunque comparta el canal: aquel no registra
/// nada a propósito porque por él pasan contraseñas y desafíos, y mezclar aquí una consulta de
/// catálogo obligaría a mantener esa disciplina en un sitio donde no hace falta.
/// </para>
/// <para>
/// <b>Exige el permiso general de la app, no el de captura.</b> Consultar el catálogo no es
/// capturar: un operador sin permiso de captura tiene que poder abrir el formulario aunque no
/// pueda enviarlo, o el aviso que le explica por qué no puede sería lo único que vería.
/// </para>
/// </remarks>
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

	/// <inheritdoc />
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
			excepcion is HttpRequestException or JsonException or UriFormatException)
		{
			// Sin red o con una respuesta ilegible se sigue con la copia local: es exactamente
			// el caso que el CA 2 contempla, no una condición de error.
			return null;
		}
		catch (TaskCanceledException) when (!cancelacion.IsCancellationRequested)
		{
			return null;
		}
	}

	/// <summary>
	/// Convierte la respuesta en el catálogo de la aplicación, o <see langword="null"/> si no
	/// sirve para capturar.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Descarta las entradas incompletas en vez de aceptarlas con valores por omisión: un tipo
	/// sin id no se puede enviar a Jacob, y ofrecerlo en el desplegable solo produciría una
	/// captura que se rechaza al sincronizar.
	/// </para>
	/// <para>
	/// Si tras el filtrado no quedan tipos o no quedan severidades, devuelve
	/// <see langword="null"/> para que <b>no se pise la copia local</b> con una inservible.
	/// </para>
	/// </remarks>
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
			cuerpos);

		return catalogo.EsUtilizable ? catalogo : null;
	}
}
