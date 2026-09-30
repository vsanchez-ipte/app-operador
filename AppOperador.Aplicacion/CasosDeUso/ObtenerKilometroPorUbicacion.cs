using AppOperador.Aplicacion.Interfaces;
using AppOperador.Domain.Entidades;
using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.CasosDeUso;

// Orden: hay lectura, es precisa y solo entonces dónde cae. Sin caché: el vehículo se mueve.
public sealed class ObtenerKilometroPorUbicacion
{
	private readonly ILocationService _ubicacion;
	private readonly IRepositorioGeometriaCorredor _geometria;

	public ObtenerKilometroPorUbicacion(
		ILocationService ubicacion,
		IRepositorioGeometriaCorredor geometria)
	{
		_ubicacion = ubicacion;
		_geometria = geometria;
	}

	public async Task<ResultadoKilometroPorUbicacion> EjecutarAsync(CancellationToken cancelacion = default)
	{
		var lectura = await _ubicacion.ObtenerPosicionAsync(cancelacion);

		if (!lectura.Obtenida)
		{
			return ResultadoKilometroPorUbicacion.Sin(lectura.Motivo ?? MotivoSinKilometro.ErrorAlObtener, null);
		}

		var posicion = lectura.Posicion!;

		if (!ReglaToleranciaCorredor.PrecisionAceptable(posicion.PrecisionMetros))
		{
			// La posición se conserva para auditoría aunque no sirva para el kilómetro.
			return ResultadoKilometroPorUbicacion.Sin(MotivoSinKilometro.PrecisionInsuficiente, posicion);
		}

		ProyeccionEnCorredor proyeccion;
		try
		{
			var corredor = await _geometria.ObtenerAsync(cancelacion);
			proyeccion = corredor.Proyectar(posicion);
		}
		catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception)
		{
			// Una geometría dañada no tumba la captura: queda la captura manual.
			return ResultadoKilometroPorUbicacion.Sin(MotivoSinKilometro.ErrorAlObtener, posicion);
		}

		if (!ReglaToleranciaCorredor.DentroDelCorredor(proyeccion.DesviacionMetros))
		{
			// Al lado de la traza no va por la autopista; más allá de sus puntas falta la geometría del tramo.
			return ResultadoKilometroPorUbicacion.Sin(
				proyeccion.MasAllaDeLaTraza
					? MotivoSinKilometro.TramoSinGeometria
					: MotivoSinKilometro.FueraDelCorredor,
				posicion);
		}

		return ResultadoKilometroPorUbicacion.Con(
			Kilometer.DesdeMetros(proyeccion.Metros),
			posicion,
			proyeccion.DesviacionMetros);
	}
}

public sealed record ResultadoKilometroPorUbicacion(
	Kilometer? Kilometro,
	PosicionDispositivo? Posicion,
	MotivoSinKilometro? Motivo,
	double? DesviacionMetros)
{
	public bool HayKilometro => Kilometro is not null;

	public int? Metros => Kilometro?.MetrosNormalizados;

	public static ResultadoKilometroPorUbicacion Con(
		Kilometer kilometro,
		PosicionDispositivo posicion,
		double desviacionMetros)
	{
		ArgumentNullException.ThrowIfNull(kilometro);
		ArgumentNullException.ThrowIfNull(posicion);
		return new ResultadoKilometroPorUbicacion(kilometro, posicion, null, desviacionMetros);
	}

	public static ResultadoKilometroPorUbicacion Sin(MotivoSinKilometro motivo, PosicionDispositivo? posicion) =>
		new(null, posicion, motivo, null);
}
