using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Lectura cruda de la ubicación del dispositivo.
/// </summary>
/// <remarks>
/// <para>
/// El nombre está fijado en inglés por el documento de arquitectura.
/// </para>
/// <para>
/// <b>Devuelve coordenadas, no kilómetros.</b> Hasta JTT-1395 esta interfaz entregaba un
/// <c>Kilometer?</c> ya resuelto, con lo que la conversión al kilometraje del corredor —que es
/// una regla del negocio— vivía dentro del adaptador de plataforma, donde no se puede probar y
/// donde cada plataforma podría acabar decidiendo distinto. Ahora esto solo lee el GPS y quien
/// convierte es <c>ObtenerKilometroPorUbicacion</c>.
/// </para>
/// </remarks>
public interface ILocationService
{
	/// <summary>
	/// Obtiene la posición actual del dispositivo.
	/// </summary>
	/// <remarks>
	/// <b>Nunca lanza por un fallo esperable.</b> Que no haya servicio, que falte el permiso o
	/// que el GPS no fije a tiempo son la mitad del trabajo de leer una ubicación en carretera, no
	/// situaciones excepcionales: llegan como motivo dentro del resultado.
	/// </remarks>
	Task<LecturaUbicacion> ObtenerPosicionAsync(CancellationToken cancelacion = default);
}

/// <summary>
/// Lo que se consiguió al pedir la ubicación: una posición, o el motivo por el que no la hay.
/// </summary>
/// <remarks>
/// Mismo patrón que <c>ResultadoSondeo</c> y <c>ResultadoEnvio</c>: <b>el motivo viaja con el
/// fallo</b> en vez de perderse en un nulo. Es lo que permite que la pantalla dé un aviso
/// distinto por causa, que es el CA 1 de JTT-1396.
/// </remarks>
public sealed record LecturaUbicacion
{
	private LecturaUbicacion(PosicionDispositivo? posicion, MotivoSinKilometro? motivo)
	{
		Posicion = posicion;
		Motivo = motivo;
	}

	/// <summary>La posición leída, o <see langword="null"/> si no se pudo.</summary>
	public PosicionDispositivo? Posicion { get; }

	/// <summary>Por qué no hay posición. Nulo cuando sí la hay.</summary>
	public MotivoSinKilometro? Motivo { get; }

	/// <summary>Indica si hay posición con la que trabajar.</summary>
	public bool Obtenida => Posicion is not null;

	/// <summary>Lectura conseguida.</summary>
	public static LecturaUbicacion Con(PosicionDispositivo posicion)
	{
		ArgumentNullException.ThrowIfNull(posicion);
		return new LecturaUbicacion(posicion, null);
	}

	/// <summary>
	/// No hubo lectura, y se dice por qué.
	/// </summary>
	/// <remarks>
	/// Solo caben aquí los motivos que el dispositivo conoce —no hay servicio, falta el permiso,
	/// falló el intento—. Si la posición cae fuera del corredor o su precisión no alcanza, eso lo
	/// decide el caso de uso, que es quien conoce la geometría y los umbrales.
	/// </remarks>
	public static LecturaUbicacion Sin(MotivoSinKilometro motivo) => new(null, motivo);
}
