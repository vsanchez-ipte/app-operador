using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Bitácora local de eventos operativos.
/// </summary>
/// <remarks>
/// La maqueta la muestra en el perfil. El documento de arquitectura pide conservar
/// trazabilidad, pero no llegó a nombrar esta interfaz; el nombre sigue el estilo en
/// inglés del resto de contratos de esta carpeta.
/// </remarks>
public interface IAuditLog
{
	/// <summary>Eventos registrados, del más reciente al más antiguo.</summary>
	Task<IReadOnlyList<EventoAuditoria>> ObtenerEventosAsync(CancellationToken cancelacion = default);

	/// <summary>Agrega un evento a la bitácora.</summary>
	Task RegistrarAsync(NivelAuditoria nivel, string mensaje, CancellationToken cancelacion = default);
}
