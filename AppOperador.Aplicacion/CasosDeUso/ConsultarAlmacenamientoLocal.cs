using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.CasosDeUso;

/// <summary>
/// Lo que el perfil muestra sobre el almacenamiento del dispositivo (JTT-292 CA 4 y 6).
/// </summary>
/// <param name="Espacio">Medición del volumen de datos de la app.</param>
/// <param name="EvidenciasPendientes">
/// Evidencias del operador que el CCO todavía no confirmó, en el orden en que se adjuntaron.
/// </param>
public sealed record AlmacenamientoLocal(
	EspacioDispositivo Espacio,
	IReadOnlyList<EvidenciaPendiente> EvidenciasPendientes)
{
	public static readonly AlmacenamientoLocal Vacio = new(EspacioDispositivo.Desconocido, []);

	public int CuantasPendientes => EvidenciasPendientes.Count;

	public long BytesPendientes => EvidenciasPendientes.Sum(e => e.Bytes);
}

/// <summary>
/// Reúne el espacio libre y las evidencias pendientes del operador con sesión (JTT-292 CA 4 y 6).
/// </summary>
/// <remarks>
/// <para>
/// <b>Pendiente es todo lo que no está Sincronizado</b>, incluido lo fallido: mientras el CCO no
/// confirme, el archivo solo existe en el dispositivo, y presentarlo de otra forma sería darlo
/// por recibido (CA 6). La subida es idempotente por contenido, así que lo fallido se
/// reintentará sin duplicar.
/// </para>
/// <para>
/// <b>Solo lo del operador con sesión.</b> La evidencia no lleva operador: lo hereda de su
/// incidencia, y es la incidencia la que se filtra (JTT-1388 CA 9). Sin sesión no hay nada que
/// mostrar.
/// </para>
/// </remarks>
public sealed class ConsultarAlmacenamientoLocal
{
	private readonly ISessionStore _sesiones;
	private readonly IRepositorioEvidencias _evidencias;
	private readonly IEspacioDispositivo _espacio;

	public ConsultarAlmacenamientoLocal(
		ISessionStore sesiones,
		IRepositorioEvidencias evidencias,
		IEspacioDispositivo espacio)
	{
		_sesiones = sesiones;
		_evidencias = evidencias;
		_espacio = espacio;
	}

	public async Task<AlmacenamientoLocal> EjecutarAsync(CancellationToken cancelacion = default)
	{
		var operador = _sesiones.Actual?.Operador;
		if (string.IsNullOrWhiteSpace(operador))
		{
			return AlmacenamientoLocal.Vacio with { Espacio = _espacio.Medir() };
		}

		var pendientes = await _evidencias.ObtenerPendientesDelOperadorAsync(operador, cancelacion);

		return new AlmacenamientoLocal(_espacio.Medir(), pendientes);
	}
}
