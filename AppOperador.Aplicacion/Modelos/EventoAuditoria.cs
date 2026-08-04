namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Entrada de la bitácora local que el operador ve en su perfil.
/// </summary>
/// <param name="InstanteUtc">Momento del evento, en UTC. La vista lo presenta en hora local.</param>
/// <param name="Nivel">Severidad del evento.</param>
/// <param name="Mensaje">Texto del evento.</param>
public sealed record EventoAuditoria(DateTime InstanteUtc, NivelAuditoria Nivel, string Mensaje);

/// <summary>Severidad de una entrada de la bitácora local.</summary>
public enum NivelAuditoria
{
	Info = 1,
	Advertencia = 2,
}
