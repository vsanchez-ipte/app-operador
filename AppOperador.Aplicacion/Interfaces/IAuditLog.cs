using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

// La implementación pone operador, rol, unidad, sesión y enlace; quien registra no los pasa.
public interface IAuditLog
{
	Task<IReadOnlyList<EventoAuditoria>> ObtenerEventosAsync(CancellationToken cancelacion = default);

	Task<IReadOnlyList<EventoAuditoria>> ObtenerEventosAsync(
		int omitir, int cantidad, CancellationToken cancelacion = default);

	Task RegistrarAsync(NivelAuditoria nivel, string mensaje, CancellationToken cancelacion = default);

	Task RegistrarAsync(
		OperacionAuditada operacion,
		ResultadoAuditoria resultado,
		string mensaje,
		string? motivoCodigo = null,
		string? operador = null,
		CancellationToken cancelacion = default);

	// El acceso escribe a nombre del correo tecleado; esto las pasa al nombre del operador.
	Task AtribuirAsync(string alias, string operador, CancellationToken cancelacion = default);
}
