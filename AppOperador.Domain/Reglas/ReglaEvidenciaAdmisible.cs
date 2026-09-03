using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Domain.Reglas;

/// <summary>
/// Decide si un archivo puede adjuntarse a una incidencia (JTT-1398 CA 6, 7 y 8).
/// </summary>
/// <remarks>
/// <para>
/// <b>Función pura del dominio, como el resto de las reglas.</b> No toca el archivo, no lo copia
/// y no sabe de dónde salió: recibe lo que ya se sabe de él y responde. Así el criterio se puede
/// probar entero, que es lo que no ocurre con lo que vive en la pantalla.
/// </para>
/// <para>
/// <b>Devuelve un motivo y no un booleano</b>, por el mismo argumento que
/// <see cref="ReglaNotaIncidencia"/>: un «no se puede» frente a un archivo que el operador acaba
/// de elegir en carretera obliga a adivinar si el problema es el tipo, el peso o que ya llenó el
/// cupo. Cada uno se corrige de forma distinta.
/// </para>
/// <para>
/// <b>Se valida antes de subir, no después.</b> El servidor vuelve a validar —es su trabajo— pero
/// descubrirlo allá cuesta los datos móviles de un archivo que se iba a rechazar de todos modos.
/// </para>
/// </remarks>
public static class ReglaEvidenciaAdmisible
{
	/// <summary>
	/// Comprueba un archivo contra los límites vigentes y lo que ya lleva la incidencia.
	/// </summary>
	/// <param name="limites">Lo que el servidor declara admitir. Ver <see cref="LimitesEvidencia"/>.</param>
	/// <param name="tipoMime">Tipo del archivo elegido.</param>
	/// <param name="bytes">Tamaño del archivo elegido.</param>
	/// <param name="yaAdjuntas">Cuántas evidencias tiene ya esa incidencia.</param>
	/// <remarks>
	/// El orden de las comprobaciones no es casual: primero si se puede adjuntar algo, después si
	/// se puede adjuntar <b>esto</b>. Decirle «el formato no sirve» a quien ya llenó el cupo lo
	/// mandaría a buscar otro archivo que tampoco va a entrar.
	/// </remarks>
	public static MotivoEvidenciaRechazada Comprobar(
		LimitesEvidencia limites,
		string? tipoMime,
		long bytes,
		int yaAdjuntas)
	{
		ArgumentNullException.ThrowIfNull(limites);

		// Sin límites no se valida contra nada. Es el estado de una app que todavía no ha
		// descargado el catálogo, y adjuntar a ciegas produciría un rechazo al sincronizar.
		if (!limites.EstanDefinidos)
		{
			return MotivoEvidenciaRechazada.LimitesDesconocidos;
		}

		if (yaAdjuntas >= limites.MaximoArchivosPorIncidencia)
		{
			return MotivoEvidenciaRechazada.CupoLleno;
		}

		if (!limites.AdmiteFormato(tipoMime))
		{
			return MotivoEvidenciaRechazada.FormatoNoAdmitido;
		}

		// Un archivo de cero bytes no es una evidencia: es una captura que salió mal, y subirla
		// gasta una de las plazas del cupo para no mostrar nada.
		if (bytes <= 0)
		{
			return MotivoEvidenciaRechazada.ArchivoVacio;
		}

		return bytes > limites.TamanoMaximoBytes
			? MotivoEvidenciaRechazada.DemasiadoGrande
			: MotivoEvidenciaRechazada.Ninguno;
	}

	/// <summary>Indica si el archivo puede adjuntarse.</summary>
	public static bool EsAdmisible(
		LimitesEvidencia limites,
		string? tipoMime,
		long bytes,
		int yaAdjuntas) =>
		Comprobar(limites, tipoMime, bytes, yaAdjuntas) == MotivoEvidenciaRechazada.Ninguno;
}
