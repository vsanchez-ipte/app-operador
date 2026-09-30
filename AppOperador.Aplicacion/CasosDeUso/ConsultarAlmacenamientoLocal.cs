using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.CasosDeUso;

public sealed record AlmacenamientoLocal(
	EspacioDispositivo Espacio,
	IReadOnlyList<EvidenciaPendiente> EvidenciasPendientes)
{
	public static readonly AlmacenamientoLocal Vacio = new(EspacioDispositivo.Desconocido, []);

	public int CuantasPendientes => EvidenciasPendientes.Count;

	public long BytesPendientes => EvidenciasPendientes.Sum(e => e.Bytes);
}

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
