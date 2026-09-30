using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.Interfaces;

public interface ILocationService
{
	// Devuelve coordenadas, no kilómetros, y nunca lanza por un fallo esperable: el motivo va en el resultado.
	Task<LecturaUbicacion> ObtenerPosicionAsync(CancellationToken cancelacion = default);
}

public sealed record LecturaUbicacion
{
	private LecturaUbicacion(PosicionDispositivo? posicion, MotivoSinKilometro? motivo)
	{
		Posicion = posicion;
		Motivo = motivo;
	}

	public PosicionDispositivo? Posicion { get; }

	public MotivoSinKilometro? Motivo { get; }

	public bool Obtenida => Posicion is not null;

	public static LecturaUbicacion Con(PosicionDispositivo posicion)
	{
		ArgumentNullException.ThrowIfNull(posicion);
		return new LecturaUbicacion(posicion, null);
	}

	public static LecturaUbicacion Sin(MotivoSinKilometro motivo) => new(null, motivo);
}
