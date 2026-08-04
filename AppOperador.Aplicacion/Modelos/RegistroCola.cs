using AppOperador.Domain.Enums;

namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Elemento de la cola de sincronización tal como se lista en la pantalla de Cola.
/// </summary>
/// <remarks>
/// La maqueta muestra dos identificadores distintos y conviene no confundirlos:
/// <see cref="ClaveLocal"/> (<c>LOC-######</c>) lo genera la app al guardar, existe sin
/// conexión y nunca cambia; <see cref="FolioCentral"/> (<c>INC-####</c>) lo asigna Jacob
/// y solo aparece después de sincronizar. El folio es opcional y no bloquea (DA-15).
/// </remarks>
/// <param name="ClaveLocal">Identificador local, presente desde la captura.</param>
/// <param name="Clase">Qué es el registro: incidencia o evidencia.</param>
/// <param name="Prioridad">Prioridad con la que la cola lo atiende.</param>
/// <param name="Descripcion">Resumen legible del contenido.</param>
/// <param name="Kilometro">Punto kilométrico asociado, en forma canónica.</param>
/// <param name="Estado">Estado dentro de la cola.</param>
/// <param name="FolioCentral">Folio asignado por Jacob, si ya se sincronizó.</param>
public sealed record RegistroCola(
	string ClaveLocal,
	ClaseRegistro Clase,
	SyncPriority Prioridad,
	string Descripcion,
	string Kilometro,
	EstadoSincronizacion Estado,
	string? FolioCentral = null);

/// <summary>
/// Naturaleza de un elemento de la cola.
/// </summary>
/// <remarks>
/// Incidencias y evidencias viajan por separado y se reintentan por separado: una
/// evidencia fallida no revierte una incidencia ya confirmada.
/// </remarks>
public enum ClaseRegistro
{
	Incidencia = 1,
	Evidencia = 2,
}
