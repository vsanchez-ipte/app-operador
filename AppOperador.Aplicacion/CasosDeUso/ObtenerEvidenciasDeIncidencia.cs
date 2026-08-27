using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.CasosDeUso;

/// <summary>
/// Lo que la pantalla necesita saber de las evidencias de una incidencia (JTT-1398 CA 1).
/// </summary>
/// <remarks>
/// <para>
/// <b>Las decisiones se resuelven aquí y no en la vista</b>, y esto no es preferencia de estilo:
/// la pantalla de la Cola se contradijo dos veces en un mismo día —26 de agosto— por tener la
/// suya en el ViewModel, donde el proyecto de pruebas no llega y ninguna prueba podía atraparlo.
/// Lo que se puede probar se prueba.
/// </para>
/// <para>
/// Por eso el resumen trae <see cref="PuedeAdjuntar"/> y <see cref="Faltantes"/> ya calculados:
/// la vista enlaza, no decide.
/// </para>
/// </remarks>
/// <param name="Adjuntas">Las evidencias registradas, en orden de captura.</param>
/// <param name="Limites">Lo que el servidor admite. Ver <see cref="LimitesEvidencia"/>.</param>
public sealed record ResumenEvidencias(
	IReadOnlyList<EvidenciaAdjunta> Adjuntas,
	LimitesEvidencia Limites)
{
	/// <summary>Resumen de una incidencia que todavía no existe o no tiene evidencias.</summary>
	public static readonly ResumenEvidencias Vacio = new([], LimitesEvidencia.Desconocidos);

	/// <summary>Cuántas evidencias tiene ya.</summary>
	public int Cuantas => Adjuntas.Count;

	/// <summary>
	/// Cuántas más admite. Cero cuando no se sabe con qué validar.
	/// </summary>
	/// <remarks>
	/// Sin límites descargados responde cero y no «las que quieras»: no saber el tope no es
	/// tener tope infinito.
	/// </remarks>
	public int Faltantes => Limites.EstanDefinidos
		? Math.Max(0, Limites.MaximoArchivosPorIncidencia - Cuantas)
		: 0;

	/// <summary>
	/// Indica si se puede adjuntar una más.
	/// </summary>
	/// <remarks>
	/// <b>Es lo que apaga el botón antes de que el operador elija el archivo.</b> Dejarlo
	/// encendido lo mandaría a la galería, a esperar la copia y a leer un rechazo que se sabía
	/// desde antes de abrirla.
	/// </remarks>
	public bool PuedeAdjuntar => Faltantes > 0;

	/// <summary>
	/// Por qué no se puede adjuntar, cuando no se puede.
	/// </summary>
	/// <remarks>
	/// Distingue las dos causas —el cupo lleno y el catálogo sin descargar— porque una es del
	/// operador y la otra no. Decirle «ya no caben más» a quien nunca ha conectado la app lo
	/// manda a borrar archivos que no existen.
	/// </remarks>
	public MotivoEvidenciaRechazada MotivoParaNoAdjuntar => !Limites.EstanDefinidos
		? MotivoEvidenciaRechazada.LimitesDesconocidos
		: Faltantes > 0
			? MotivoEvidenciaRechazada.Ninguno
			: MotivoEvidenciaRechazada.CupoLleno;

	/// <summary>Suma de lo que ocupan, para que la vista pueda advertir de una cola pesada.</summary>
	public long BytesTotales => Adjuntas.Sum(e => e.Bytes);
}

/// <summary>Arma el resumen de evidencias de una incidencia.</summary>
public sealed class ObtenerEvidenciasDeIncidencia
{
	private readonly IRepositorioEvidencias _evidencias;
	private readonly ICatalogoRepository _catalogo;

	public ObtenerEvidenciasDeIncidencia(
		IRepositorioEvidencias evidencias,
		ICatalogoRepository catalogo)
	{
		_evidencias = evidencias;
		_catalogo = catalogo;
	}

	/// <summary>
	/// Devuelve las evidencias de la incidencia y lo que se puede hacer con ellas.
	/// </summary>
	/// <param name="incidenciaUuid">
	/// La incidencia. Vacío o nulo cuando la captura todavía no se ha guardado: entonces no hay
	/// evidencias, pero <b>sí hay límites</b>, y la pantalla los necesita para saber si el botón
	/// va encendido antes de que exista nada.
	/// </param>
	public async Task<ResumenEvidencias> EjecutarAsync(
		string? incidenciaUuid,
		CancellationToken cancelacion = default)
	{
		var limites = (await _catalogo.ObtenerAsync(cancelacion)).LimitesEvidencia;

		if (string.IsNullOrWhiteSpace(incidenciaUuid))
		{
			return new ResumenEvidencias([], limites);
		}

		var adjuntas = await _evidencias.ObtenerDeIncidenciaAsync(incidenciaUuid, cancelacion);

		return new ResumenEvidencias(adjuntas, limites);
	}
}
