using System.Globalization;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.Servicios;

/// <summary>
/// Completa los campos que el modelo de Jacob exige y el formulario móvil no captura.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Esta clase existe para borrarse.</b> Está aislada y con nombre explícito para que el día
/// que el API deje de exigir esos campos a los orígenes que no los tienen, quitarla sea un
/// <c>delete</c> y no una cacería por todo el proyecto.
/// </para>
/// <para>
/// <b>Qué rellena y por qué.</b> <c>cuerpo</c> e <c>idAfectacion</c> son obligatorios en
/// <c>bit_incidencias</c>, pero <b>ningún criterio de la App Operador los pide</b>: se cotejó el
/// 21-ago contra los once criterios de JTT-1393, y el CA 1 lista exactamente tipo, severidad,
/// kilómetro, nota y evidencia. Se decidió no agregar campos que nadie pidió, así que se toman
/// del catálogo local al enviar.
/// </para>
/// <para>
/// <b>Se elige del catálogo, no se inventa un literal.</b> Un valor fijo escrito en el código
/// dejaría de ser válido en cuanto alguien depurara el catálogo, y el rechazo aparecería en el
/// envío —cuando el operador ya no está frente al hecho— en vez de aquí.
/// </para>
/// <para>
/// <b>Las incidencias afectadas son identificables sin marcarlas:</b> todas las de la app llevan
/// <c>origen_modulo = 'APP_OPERADOR'</c> y folio <c>INC-APK-</c>. No hace falta una columna nueva
/// para encontrarlas cuando haya que revisarlas.
/// </para>
/// </remarks>
public static class RellenoCamposNoCapturados
{
	/// <summary>
	/// Arma el envío de una incidencia, rellenando lo que el formulario no captura.
	/// </summary>
	/// <param name="catalogos">
	/// Copia local del catálogo. <b>La recibe, no la lee.</b> Leerla aquí obligaría a una consulta
	/// por registro, o a cachearla dentro de un servicio que el contenedor puede reutilizar entre
	/// sincronizaciones y acabaría sirviendo un catálogo viejo.
	/// </param>
	public static EnvioIncidencia Completar(IncidenciaEnviable incidencia, CatalogosOperacion catalogos)
	{
		ArgumentNullException.ThrowIfNull(incidencia);
		ArgumentNullException.ThrowIfNull(catalogos);

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
			IdSesionOrigen: Guid.TryParse(incidencia.SesionOrigen, out var sesion) ? sesion : null);
	}

	/// <summary>
	/// Afectación con la que se completa el envío.
	/// </summary>
	/// <remarks>
	/// Se toma la primera del catálogo. <b>No se elige por nombre</b> —«Sin afectación» podría
	/// renombrarse o retirarse— y no se aleatoriza entre envíos: dos incidencias del mismo turno
	/// con grados de cierre distintos y ninguno observado se leerían en el CCO como si alguien
	/// hubiera valorado cada una.
	/// </remarks>
	private static int ElegirAfectacion(CatalogosOperacion catalogos) =>
		catalogos.Afectaciones.Count > 0 ? catalogos.Afectaciones[0].Id : 0;

	/// <summary>
	/// Cuerpo de la vía con el que se completa el envío. Mismo criterio que la afectación.
	/// </summary>
	private static string ElegirCuerpo(CatalogosOperacion catalogos) =>
		catalogos.Cuerpos.Count > 0 ? catalogos.Cuerpos[0].Clave : "A";

	/// <summary>
	/// Convierte el kilómetro canónico <c>130+200</c> al decimal <c>130.200</c> que espera Jacob.
	/// </summary>
	/// <remarks>
	/// En cultura invariante siempre. Con una cultura de coma decimal, <c>130.200</c> viajaría
	/// como ciento treinta mil doscientos y el servidor lo rechazaría por fuera del corredor.
	/// </remarks>
	private static decimal ADecimal(string? canonico)
	{
		if (!Kilometer.IntentarCrear(canonico, out var kilometro))
		{
			return 0m;
		}

		return kilometro.Kilometros + (kilometro.Metros / 1000m);
	}
}
