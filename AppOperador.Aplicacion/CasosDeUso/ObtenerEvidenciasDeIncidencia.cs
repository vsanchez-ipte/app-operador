using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.CasosDeUso;

// Las decisiones se resuelven aquí y no en la vista, a la que no llegan las pruebas.
public sealed record ResumenEvidencias(
	IReadOnlyList<EvidenciaAdjunta> Adjuntas,
	LimitesEvidencia Limites,
	long? BytesLibres = null)
{
	public static readonly ResumenEvidencias Vacio = new([], LimitesEvidencia.Desconocidos);

	public int Cuantas => Adjuntas.Count;

	// Sin límites descargados es cero: no saber el tope no es tener tope infinito.
	public int Faltantes => Limites.EstanDefinidos
		? Math.Max(0, Limites.MaximoArchivosPorIncidencia - Cuantas)
		: 0;

	// Apaga el botón antes de mandar al operador a la galería por un rechazo ya sabido.
	public bool PuedeAdjuntar => Faltantes > 0;

	// Distingue cupo lleno de catálogo sin descargar: solo lo primero es del operador.
	public MotivoEvidenciaRechazada MotivoParaNoAdjuntar => !Limites.EstanDefinidos
		? MotivoEvidenciaRechazada.LimitesDesconocidos
		: Faltantes > 0
			? MotivoEvidenciaRechazada.Ninguno
			: MotivoEvidenciaRechazada.CupoLleno;

	public long BytesTotales => Adjuntas.Sum(e => e.Bytes);

	// Tres condiciones: que quepa otro archivo, que el servidor admita video y que haya espacio.
	public bool PuedeAdjuntarVideo => PuedeAdjuntar && Limites.AdmiteVideo && HayEspacioParaVideo;

	// Antes de grabar y con el máximo: el tamaño real no existe hasta que termina la grabación.
	public bool HayEspacioParaVideo =>
		ReglaEspacioParaEvidencia.Cabe(BytesLibres, Limites.TamanoMaximoBytes);

	// Cero cuando no se puede grabar: pedirle a la cámara que corte en cero la dejaría sin grabar.
	public long TopeParaGrabarVideo =>
		PuedeAdjuntarVideo ? Limites.TamanoMaximoBytes : 0;

	// Aparte: tiene una causa más, que el servidor no admita el formato.
	public MotivoEvidenciaRechazada MotivoParaNoAdjuntarVideo =>
		MotivoParaNoAdjuntar is not MotivoEvidenciaRechazada.Ninguno
			? MotivoParaNoAdjuntar
			: !Limites.AdmiteVideo
				? MotivoEvidenciaRechazada.FormatoNoAdmitido
				: !HayEspacioParaVideo
					? MotivoEvidenciaRechazada.SinEspacio
					: MotivoEvidenciaRechazada.Ninguno;
}

public sealed class ObtenerEvidenciasDeIncidencia
{
	private readonly IRepositorioEvidencias _evidencias;
	private readonly ICatalogoRepository _catalogo;
	private readonly IEspacioDispositivo _espacio;

	public ObtenerEvidenciasDeIncidencia(
		IRepositorioEvidencias evidencias,
		ICatalogoRepository catalogo,
		IEspacioDispositivo espacio)
	{
		_espacio = espacio;
		_evidencias = evidencias;
		_catalogo = catalogo;
	}

	// Sin incidencia guardada no hay evidencias, pero sí límites para encender el botón.
	public async Task<ResumenEvidencias> EjecutarAsync(
		string? incidenciaUuid,
		CancellationToken cancelacion = default)
	{
		var limites = (await _catalogo.ObtenerAsync(cancelacion)).LimitesEvidencia;

		if (string.IsNullOrWhiteSpace(incidenciaUuid))
		{
			return new ResumenEvidencias([], limites, _espacio.Medir().BytesLibres);
		}

		var adjuntas = await _evidencias.ObtenerDeIncidenciaAsync(incidenciaUuid, cancelacion);

		// El espacio viaja en el resumen: la pantalla no mide nada por su cuenta.
		return new ResumenEvidencias(adjuntas, limites, _espacio.Medir().BytesLibres);
	}
}
