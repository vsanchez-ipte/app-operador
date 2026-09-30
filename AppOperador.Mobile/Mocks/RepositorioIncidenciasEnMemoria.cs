using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Mobile.Mocks;

public sealed class RepositorioIncidenciasEnMemoria : IIncidentRepository
{
	private readonly AlmacenRegistrosEnMemoria _almacen;
	private readonly ISessionStore _sesiones;

	public RepositorioIncidenciasEnMemoria(AlmacenRegistrosEnMemoria almacen, ISessionStore sesiones)
	{
		_almacen = almacen;
		_sesiones = sesiones;
	}

	private string OperadorActual => _sesiones.Actual?.Operador ?? "-";

	public Task<string> GuardarAsync(
		TipoIncidencia tipo,
		Kilometer kilometro,
		KilometerSource fuenteKilometro,
		SeveridadIncidencia severidad,
		string nota,
		PosicionDispositivo? posicionGps = null,
		CancellationToken cancelacion = default)
	{
		// La prioridad de cola la decide la regla de dominio, no esta clase.
		var prioridad = ReglaPrioridadSincronizacion.Para(severidad.Orden);

		var registro = new RegistroCola(
			ClaveLocal: _almacen.SiguienteClaveLocal(),
			Clase: ClaseRegistro.Incidencia,
			Prioridad: prioridad,
			Descripcion: tipo.Nombre,
			Kilometro: kilometro.Valor,
			Estado: EstadoSincronizacion.Pendiente);

		_almacen.Agregar(registro, OperadorActual);
		return Task.FromResult(registro.ClaveLocal);
	}

	public Task<string> GuardarBorradorAsync(
		TipoIncidencia? tipo,
		string? kilometro,
		SeveridadIncidencia? severidad,
		string nota,
		CancellationToken cancelacion = default)
	{
		// Un borrador se guarda incompleto a propósito y nunca entra a la cola de envío.
		var registro = new RegistroCola(
			ClaveLocal: _almacen.SiguienteClaveLocal(),
			Clase: ClaseRegistro.Incidencia,
			Prioridad: ReglaPrioridadSincronizacion.Para(severidad?.Orden ?? int.MaxValue),
			Descripcion: tipo?.Nombre ?? "Sin tipo",
			Kilometro: kilometro ?? "-",
			Estado: EstadoSincronizacion.Borrador);

		_almacen.Agregar(registro, OperadorActual);
		return Task.FromResult(registro.ClaveLocal);
	}

	public Task<IReadOnlyList<RegistroCola>> ObtenerBorradoresAsync(CancellationToken cancelacion = default) =>
		Task.FromResult(_almacen.PorEstado(EstadoSincronizacion.Borrador, OperadorActual));

	public Task<BorradorIncidencia?> ObtenerBorradorAsync(
		string claveLocal,
		CancellationToken cancelacion = default) =>
		Task.FromResult<BorradorIncidencia?>(null);

	public Task<bool> ActualizarBorradorAsync(
		string claveLocal,
		TipoIncidencia? tipo,
		string? kilometro,
		SeveridadIncidencia? severidad,
		string nota,
		CancellationToken cancelacion = default) =>
		Task.FromResult(false);

	public Task<bool> EliminarBorradorAsync(
		string claveLocal,
		CancellationToken cancelacion = default) =>
		Task.FromResult(false);

	public Task<bool> ConvertirBorradorAsync(
		string claveLocal,
		TipoIncidencia tipo,
		Kilometer kilometro,
		KilometerSource fuenteKilometro,
		SeveridadIncidencia severidad,
		string nota,
		PosicionDispositivo? posicionGps = null,
		CancellationToken cancelacion = default) =>
		Task.FromResult(false);

	// El simulador no rechaza nada, así que no hay nada que corregir.
	public Task<IncidenciaRechazada?> ObtenerRechazadaAsync(
		string claveLocal,
		CancellationToken cancelacion = default) =>
		Task.FromResult<IncidenciaRechazada?>(null);

	public Task<bool> CorregirRechazadaAsync(
		string claveLocal,
		TipoIncidencia tipo,
		Kilometer kilometro,
		KilometerSource fuenteKilometro,
		SeveridadIncidencia severidad,
		string nota,
		PosicionDispositivo? posicionGps = null,
		CancellationToken cancelacion = default) =>
		Task.FromResult(false);
}
