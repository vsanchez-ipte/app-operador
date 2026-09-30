using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.Interfaces;

public interface IIncidentRepository
{
	Task<string> GuardarAsync(
		TipoIncidencia tipo,
		Kilometer kilometro,
		KilometerSource fuenteKilometro,
		SeveridadIncidencia severidad,
		string nota,
		PosicionDispositivo? posicionGps = null,
		CancellationToken cancelacion = default);

	// Un borrador nunca se envía.
	Task<string> GuardarBorradorAsync(
		TipoIncidencia? tipo,
		string? kilometro,
		SeveridadIncidencia? severidad,
		string nota,
		CancellationToken cancelacion = default);

	Task<IReadOnlyList<RegistroCola>> ObtenerBorradoresAsync(CancellationToken cancelacion = default);

	Task<BorradorIncidencia?> ObtenerBorradorAsync(
		string claveLocal,
		CancellationToken cancelacion = default);

	Task<bool> ActualizarBorradorAsync(
		string claveLocal,
		TipoIncidencia? tipo,
		string? kilometro,
		SeveridadIncidencia? severidad,
		string nota,
		CancellationToken cancelacion = default);

	// Se borra de verdad: nunca llegó a Jacob.
	Task<bool> EliminarBorradorAsync(
		string claveLocal,
		CancellationToken cancelacion = default);

	Task<IncidenciaRechazada?> ObtenerRechazadaAsync(
		string claveLocal,
		CancellationToken cancelacion = default);

	// Conserva clave, uuid y evidencia; reinicia intentos y último error.
	Task<bool> CorregirRechazadaAsync(
		string claveLocal,
		TipoIncidencia tipo,
		Kilometer kilometro,
		KilometerSource fuenteKilometro,
		SeveridadIncidencia severidad,
		string nota,
		PosicionDispositivo? posicionGps = null,
		CancellationToken cancelacion = default);

	// Los tipos obligan a validar antes de convertir. Conserva clave y uuid del borrador.
	Task<bool> ConvertirBorradorAsync(
		string claveLocal,
		TipoIncidencia tipo,
		Kilometer kilometro,
		KilometerSource fuenteKilometro,
		SeveridadIncidencia severidad,
		string nota,
		PosicionDispositivo? posicionGps = null,
		CancellationToken cancelacion = default);
}
