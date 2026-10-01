using AppOperador.Domain.Reglas;

namespace AppOperador.Aplicacion.Modelos;

public sealed record EvidenciaRezagada(
	EvidenciaAdjunta Evidencia,
	int Intentos,
	DateTime? UltimoIntentoUtc)
{
	public bool RechazadaPorElCco => CodigosErrorJacob.EsFuncional(Evidencia.UltimoErrorCodigo);

	public DateTime? ProximoIntentoUtc
	{
		get
		{
			if (RechazadaPorElCco)
			{
				return null;
			}

			return UltimoIntentoUtc is { } ultimo
				? ultimo + ReglaEsperaReintento.Para(Intentos)
				: DateTime.MinValue;
		}
	}

	public bool TocaIntentar(DateTime ahoraUtc) =>
		ProximoIntentoUtc is { } proximo && proximo <= ahoraUtc;
}
