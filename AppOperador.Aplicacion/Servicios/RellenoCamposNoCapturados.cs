using System.Globalization;
using System.Text;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.Servicios;

// Existe para borrarse cuando el API deje de exigir cuerpo y afectación a la app.
public static class RellenoCamposNoCapturados
{
	// Recibe el catálogo en vez de leerlo: una caché interna acabaría sirviendo uno viejo.
	public static EnvioIncidencia Completar(IncidenciaEnviable incidencia, CatalogosOperacion catalogos)
	{
		ArgumentNullException.ThrowIfNull(incidencia);
		ArgumentNullException.ThrowIfNull(catalogos);
		var posicionGps = incidencia.FuenteKilometro == KilometerSource.GPS
			? incidencia.PosicionGps
			: null;

		return new EnvioIncidencia(
			Uuid: incidencia.Uuid,
			IdTipoIncidencia: incidencia.TipoId ?? 0,
			IdGravedad: incidencia.SeveridadId,
			IdAfectacion: ElegirAfectacion(catalogos),
			Km: ADecimal(incidencia.Kilometro),
			FuenteKilometro: incidencia.FuenteKilometro == KilometerSource.GPS ? "GPS" : "MANUAL",
			Cuerpo: ElegirCuerpo(catalogos),
			Nota: incidencia.Nota,
			FchCapturaCampo: incidencia.CapturadaUtc,
			IdSesionOrigen: Guid.TryParse(incidencia.SesionOrigen, out var sesion) ? sesion : null,
			Latitud: posicionGps is null ? null : (decimal)posicionGps.Latitud,
			Longitud: posicionGps is null ? null : (decimal)posicionGps.Longitud);
	}

	// «Sin afectación», buscada por nombre: la primera del catálogo es «Total» y declararía la vía cerrada.
	private static int ElegirAfectacion(CatalogosOperacion catalogos)
	{
		if (catalogos.Afectaciones.Count == 0)
		{
			return 0;
		}

		var sinAfectacion = catalogos.Afectaciones
			.FirstOrDefault(a => Normalizar(a.Nombre).Contains("sin afectacion", StringComparison.Ordinal));

		return sinAfectacion?.Id ?? catalogos.Afectaciones[^1].Id;
	}

	// «Ambos»: un cuerpo concreto mandaría a quien atiende al lado equivocado.
	private const string ClaveAmbosCuerpos = "C";

	private static string ElegirCuerpo(CatalogosOperacion catalogos)
	{
		if (catalogos.Cuerpos.Count == 0)
		{
			return ClaveAmbosCuerpos;
		}

		var ambos = catalogos.Cuerpos
			.FirstOrDefault(c => c.Clave.Equals(ClaveAmbosCuerpos, StringComparison.OrdinalIgnoreCase));

		return ambos?.Clave ?? catalogos.Cuerpos[0].Clave;
	}

	private static string Normalizar(string texto) =>
		string.Concat(texto.Normalize(NormalizationForm.FormD)
				.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
			.ToLowerInvariant();

	// Cultura invariante: con coma decimal, 130.200 viajaría como 130200.
	private static decimal ADecimal(string? canonico)
	{
		if (!Kilometer.IntentarCrear(canonico, out var kilometro))
		{
			return 0m;
		}

		return kilometro.Kilometros + (kilometro.Metros / 1000m);
	}
}
