using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Mobile.Mocks;

/// <summary>
/// Bitácora de auditoría guardada en memoria.
/// </summary>
/// <remarks>
/// Se pierde al cerrar la app; la definitiva vive en SQLite (JTT-1345).
/// </remarks>
public sealed class BitacoraEnMemoria : IAuditLog
{
	private const int MaximoEventos = 50;

	private readonly IClock _reloj;
	private readonly List<EventoAuditoria> _eventos = [];

	public BitacoraEnMemoria(IClock reloj) => _reloj = reloj;

	public Task<IReadOnlyList<EventoAuditoria>> ObtenerEventosAsync(CancellationToken cancelacion = default)
	{
		// Del más reciente al más antiguo, como los presenta la maqueta.
		IReadOnlyList<EventoAuditoria> copia = _eventos.OrderByDescending(e => e.InstanteUtc).ToList();
		return Task.FromResult(copia);
	}

	public Task RegistrarAsync(NivelAuditoria nivel, string mensaje, CancellationToken cancelacion = default)
	{
		_eventos.Add(new EventoAuditoria(_reloj.UtcAhora, nivel, mensaje));

		// Se acota para que la bitácora local no crezca sin límite.
		if (_eventos.Count > MaximoEventos)
		{
			_eventos.RemoveRange(0, _eventos.Count - MaximoEventos);
		}

		return Task.CompletedTask;
	}
}
