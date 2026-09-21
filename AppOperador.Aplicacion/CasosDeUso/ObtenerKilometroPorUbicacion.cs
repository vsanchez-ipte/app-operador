using AppOperador.Aplicacion.Interfaces;
using AppOperador.Domain.Entidades;
using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.CasosDeUso;

/// <summary>
/// Convierte la ubicación del dispositivo en el punto kilométrico del corredor (JTT-1395).
/// </summary>
/// <remarks>
/// <para>
/// <b>Es el único dueño de la decisión.</b> Antes esto vivía repartido entre el adaptador de
/// plataforma —que devolvía un kilómetro ya resuelto— y el ViewModel —que trataba el nulo—. Con
/// la regla partida en dos capas que no se pueden probar juntas, las cinco causas del CA 1 de
/// JTT-1396 acabaron colapsadas en un solo aviso y la rama que las atendía quedó inalcanzable.
/// </para>
/// <para>
/// <b>El orden de las comprobaciones importa y no es arbitrario:</b> primero si hay lectura,
/// después si la lectura es fiable, y solo entonces dónde cae. Comprobar la geometría antes que
/// la precisión daría «fuera del corredor» a un operador que está en la autopista con mala señal
/// —un rechazo que no puede corregir y que además es mentira—; y proyectar una lectura de 400 m
/// de incertidumbre devuelve un kilómetro con toda la pinta de ser bueno.
/// </para>
/// </remarks>
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

	/// <summary>
	/// Pide una posición y la sitúa en el corredor.
	/// </summary>
	/// <remarks>
	/// Se llama al abrir el formulario y cada vez que el operador pulsa recalcular
	/// (JTT-1395 CA 1 y 11). No guarda estado entre llamadas a propósito: el vehículo se mueve, y
	/// una lectura cacheada es una lectura equivocada en cuanto arranca.
	/// </remarks>
	public async Task<ResultadoKilometroPorUbicacion> EjecutarAsync(CancellationToken cancelacion = default)
	{
		var lectura = await _ubicacion.ObtenerPosicionAsync(cancelacion);

		if (!lectura.Obtenida)
		{
			// El dispositivo ya dijo por qué. No hay nada que añadir y nada que suponer.
			return ResultadoKilometroPorUbicacion.Sin(lectura.Motivo ?? MotivoSinKilometro.ErrorAlObtener, null);
		}

		var posicion = lectura.Posicion!;

		if (!ReglaToleranciaCorredor.PrecisionAceptable(posicion.PrecisionMetros))
		{
			// Se devuelve la posición aunque no sirva para el kilómetro: la auditoría del CA 9
			// quiere saber qué se leyó, y «no se pudo» sin el dato no explica nada después.
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
			// Una geometría ausente o dañada no debe tumbar la captura. El dato leído se
			// conserva para auditoría y el operador puede continuar con captura manual.
			return ResultadoKilometroPorUbicacion.Sin(MotivoSinKilometro.ErrorAlObtener, posicion);
		}

		if (!ReglaToleranciaCorredor.DentroDelCorredor(proyeccion.DesviacionMetros))
		{
			// Estar lejos de la traza son dos cosas distintas, y confundirlas culpa al operador de
			// una carencia del sistema: al lado de la traza es que no va por la autopista; más
			// allá de sus puntas es que de ese tramo no se ha cargado geometría.
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

/// <summary>
/// El kilómetro deducido de la ubicación, o el motivo por el que no se pudo deducir.
/// </summary>
/// <param name="Kilometro">Punto kilométrico en forma canónica, o nulo si no se pudo.</param>
/// <param name="Posicion">
/// La lectura original, <b>presente incluso cuando no hubo kilómetro</b> siempre que llegara a
/// haber lectura. Es lo que el CA 9 manda conservar para auditoría, y lo que permitiría
/// recalcular el kilómetro el día que se corrija la geometría.
/// </param>
/// <param name="Motivo">Por qué no hay kilómetro. Nulo cuando sí lo hay.</param>
/// <param name="DesviacionMetros">Cuánto se apartaba la lectura de la traza. Nulo si no se proyectó.</param>
public sealed record ResultadoKilometroPorUbicacion(
	Kilometer? Kilometro,
	PosicionDispositivo? Posicion,
	MotivoSinKilometro? Motivo,
	double? DesviacionMetros)
{
	/// <summary>Indica si hay kilómetro con el que rellenar el formulario.</summary>
	public bool HayKilometro => Kilometro is not null;

	/// <summary>
	/// Punto kilométrico normalizado en metros (JTT-1395 CA 8), o nulo si no hubo cálculo.
	/// </summary>
	public int? Metros => Kilometro?.MetrosNormalizados;

	/// <summary>Se calculó el kilómetro.</summary>
	public static ResultadoKilometroPorUbicacion Con(
		Kilometer kilometro,
		PosicionDispositivo posicion,
		double desviacionMetros)
	{
		ArgumentNullException.ThrowIfNull(kilometro);
		ArgumentNullException.ThrowIfNull(posicion);
		return new ResultadoKilometroPorUbicacion(kilometro, posicion, null, desviacionMetros);
	}

	/// <summary>No se calculó, y se dice por qué.</summary>
	public static ResultadoKilometroPorUbicacion Sin(MotivoSinKilometro motivo, PosicionDispositivo? posicion) =>
		new(null, posicion, motivo, null);
}
