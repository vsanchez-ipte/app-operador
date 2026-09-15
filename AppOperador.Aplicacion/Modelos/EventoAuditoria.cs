namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Una línea de la bitácora local (JTT-1392).
/// </summary>
/// <remarks>
/// <para>
/// Además del instante, el nivel y el texto, cada línea lleva <b>quién y desde dónde</b>: el
/// operador, su rol, el permiso con el que operaba, la unidad, la sesión y si había enlace con
/// el CCO en ese momento (CA 1). Son copias tomadas en el instante del evento, no referencias:
/// la sesión local guarda una sola fila y se sobrescribe al entrar otro operador, así que una
/// referencia a ella reescribiría el historial con cada cambio de turno.
/// </para>
/// <para>
/// <see cref="MonotonicoTicks"/> es el sello que sobrevive a que muevan el reloj del
/// dispositivo: es por lo que se ordena. <see cref="InstanteUtc"/> es lo que se muestra.
/// </para>
/// </remarks>
public sealed record EventoAuditoria(
	DateTime InstanteUtc,
	NivelAuditoria Nivel,
	string Mensaje,
	OperacionAuditada Operacion = OperacionAuditada.Otra,
	ResultadoAuditoria? Resultado = null,
	string? MotivoCodigo = null,
	string? Operador = null,
	string? Rol = null,
	string? Permiso = null,
	string? UnidadClave = null,
	string? SesionId = null,
	OrigenAuditoria Origen = OrigenAuditoria.Desconocido,
	long MonotonicoTicks = 0);

/// <summary>Severidad de una línea de bitácora.</summary>
public enum NivelAuditoria
{
	Info = 1,
	Advertencia = 2,
}

/// <summary>
/// Qué operación se registra. Es el mismo vocabulario que usa el CCO en su auditoría móvil
/// (<c>OperacionMovil</c> del API), para que las dos bitácoras se puedan casar cuando la local
/// viaje al servidor.
/// </summary>
public enum OperacionAuditada
{
	/// <summary>Sin operación concreta: avisos generales, diagnóstico.</summary>
	Otra = 0,

	Autenticacion = 1,
	SeleccionUnidad = 2,
	CreacionSesion = 3,
	RevalidacionSesion = 4,
	CierreSesion = 5,
	ComprobacionComunicacion = 6,

	/// <summary>Propias de la app: el CCO las conoce por sus efectos, no como operación.</summary>
	RecuperacionPendientes = 20,
	Sincronizacion = 21,
	Captura = 22,
	Evidencia = 23,
}

/// <summary>Cómo terminó la operación.</summary>
public enum ResultadoAuditoria
{
	Exito = 1,
	Rechazo = 2,
}

/// <summary>Si había enlace con el CCO cuando ocurrió el evento (CA 1, «estado online u offline»).</summary>
public enum OrigenAuditoria
{
	/// <summary>Líneas anteriores a la versión 10 del esquema, que no lo registraban.</summary>
	Desconocido = 0,
	Online = 1,
	Offline = 2,
}
