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

	public RepositorioIncidenciasEnMemoria(AlmacenRegistrosEnMemoria almacen) => _almacen = almacen;

	public Task<IReadOnlyList<TipoIncidencia>> ObtenerTiposAsync(CancellationToken cancelacion = default)
	{
		// Catálogo provisional: el definitivo lo entrega Jacob. Lo único fijado es que
		// existe "Otro" y que obliga a describir la incidencia (JTT-333).
		IReadOnlyList<TipoIncidencia> tipos =
		[
			new("OBJ", "Objeto en camino"),
			new("VEH", "Vehículo detenido"),
			new("ACC", "Accidente"),
			new("ANI", "Animal en la vía"),
			new("PAV", "Daño en pavimento"),
			new("OTR", "Otro", ExigeDescripcion: true),
		];

		return Task.FromResult(tipos);
	}

	public Task<string> GuardarAsync(
		TipoIncidencia tipo,
		Kilometer kilometro,
		KilometerSource fuenteKilometro,
		Gravedad gravedad,
		string nota,
		CancellationToken cancelacion = default)
	{
		// La prioridad de cola la decide la regla de dominio, no esta clase.
		var prioridad = ReglaPrioridadSincronizacion.Para(gravedad);


		var registro = new RegistroCola(
			ClaveLocal: _almacen.SiguienteClaveLocal(),
			Clase: ClaseRegistro.Incidencia,
			Prioridad: prioridad,
			Descripcion: tipo.Nombre,
			Kilometro: kilometro.Valor,
			Estado: EstadoSincronizacion.Pendiente);

		_almacen.Agregar(registro);
		return Task.FromResult(registro.ClaveLocal);
	}

	public Task<string> GuardarBorradorAsync(
		TipoIncidencia? tipo,
		string? kilometro,
		Gravedad gravedad,
		string nota,
		CancellationToken cancelacion = default)
	{
		// Un borrador se guarda incompleto a propósito y nunca entra a la cola de envío.
		var registro = new RegistroCola(
			ClaveLocal: _almacen.SiguienteClaveLocal(),
			Clase: ClaseRegistro.Incidencia,
			Prioridad: ReglaPrioridadSincronizacion.Para(gravedad),
			Descripcion: tipo?.Nombre ?? "Sin tipo",
			Kilometro: kilometro ?? "-",
			Estado: EstadoSincronizacion.Borrador);

		_almacen.Agregar(registro);
		return Task.FromResult(registro.ClaveLocal);
	}

	public Task<IReadOnlyList<RegistroCola>> ObtenerBorradoresAsync(CancellationToken cancelacion = default) =>
		Task.FromResult(_almacen.PorEstado(EstadoSincronizacion.Borrador));
}
