namespace AppOperador.Aplicacion.Modelos;

// Operador, rol y sesión son copias del momento: la sesión local se sobrescribe al cambiar de turno.
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

public enum NivelAuditoria
{
	Info = 1,
	Advertencia = 2,
}

// Mismo vocabulario que OperacionMovil del API, para poder casar las dos bitácoras.
public enum OperacionAuditada
{
	Otra = 0,

	Autenticacion = 1,
	SeleccionUnidad = 2,
	CreacionSesion = 3,
	RevalidacionSesion = 4,
	CierreSesion = 5,
	ComprobacionComunicacion = 6,

	RecuperacionPendientes = 20,
	Sincronizacion = 21,
	Captura = 22,
	Evidencia = 23,
}

public enum ResultadoAuditoria
{
	Exito = 1,
	Rechazo = 2,
}

public enum OrigenAuditoria
{
	Desconocido = 0,
	Online = 1,
	Offline = 2,
}
