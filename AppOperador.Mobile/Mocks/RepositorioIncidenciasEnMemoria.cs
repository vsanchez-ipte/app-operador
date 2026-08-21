using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Mobile.Mocks;

/// <summary>
/// Incidencias y borradores guardados en memoria.
/// </summary>
/// <remarks>
/// Comparte la lista con <see cref="ColaSincronizacionEnMemoria"/>: una incidencia
/// guardada aparece en la cola, igual que en la maqueta. Al llegar SQLite (JTT-1345),
/// ambas clases se reemplazan por repositorios sobre la misma base local.
/// </remarks>
public sealed class RepositorioIncidenciasEnMemoria : IIncidentRepository
{
	private readonly AlmacenRegistrosEnMemoria _almacen;
	private readonly ISessionStore _sesiones;

	public RepositorioIncidenciasEnMemoria(AlmacenRegistrosEnMemoria almacen, ISessionStore sesiones)
	{
		_almacen = almacen;
		_sesiones = sesiones;
	}

	/// <summary>
	/// Operador que captura. Sin sesión no debería llegarse aquí: las pantallas de captura
	/// están detrás del acceso.
	/// </summary>
	private string OperadorActual => _sesiones.Actual?.Operador ?? "-";

	public Task<string> GuardarAsync(
		TipoIncidencia tipo,
		Kilometer kilometro,
		KilometerSource fuenteKilometro,
		SeveridadIncidencia severidad,
		string nota,
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
}
