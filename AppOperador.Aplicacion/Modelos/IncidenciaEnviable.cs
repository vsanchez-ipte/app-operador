using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.Modelos;

public sealed record IncidenciaEnviable(
	string Uuid,
	string ClaveLocal,
	int? TipoId,
	Guid? SeveridadId,
	string? Kilometro,
	KilometerSource FuenteKilometro,
	string Nota,
	DateTime CapturadaUtc,
	string SesionOrigen,
	EstadoSincronizacion Estado,
	int Intentos,
	DateTime UltimoIntentoUtc,
	// Sobrevive al cierre de la app: es lo que separa un rechazo funcional de uno técnico.
	string? UltimoErrorCodigo,
	int? KilometroMetros = null,
	PosicionDispositivo? PosicionGps = null);

public sealed record ActualizacionEnvio(
	string Uuid,
	EstadoSincronizacion Estado,
	int Intentos,
	string? FolioCentral,
	string? UltimoErrorCodigo);
