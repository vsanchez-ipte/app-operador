using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Domain.Reglas;

public static class ReglaEvidenciaAdmisible
{
	// Primero si cabe algo más y después si cabe este archivo: al que llenó el cupo no le sirve oír que el formato no entra.
	public static MotivoEvidenciaRechazada Comprobar(
		LimitesEvidencia limites,
		string? tipoMime,
		long bytes,
		int yaAdjuntas)
	{
		ArgumentNullException.ThrowIfNull(limites);

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

		if (bytes <= 0)
		{
			return MotivoEvidenciaRechazada.ArchivoVacio;
		}

		return bytes > limites.TamanoMaximoBytes
			? MotivoEvidenciaRechazada.DemasiadoGrande
			: MotivoEvidenciaRechazada.Ninguno;
	}

	public static bool EsAdmisible(
		LimitesEvidencia limites,
		string? tipoMime,
		long bytes,
		int yaAdjuntas) =>
		Comprobar(limites, tipoMime, bytes, yaAdjuntas) == MotivoEvidenciaRechazada.Ninguno;
}
