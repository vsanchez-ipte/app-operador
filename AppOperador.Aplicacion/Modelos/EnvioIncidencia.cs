using AppOperador.Domain.Enums;

namespace AppOperador.Aplicacion.Modelos;

// Operador, sesión y vehículo salen del token; plaza y tramo, del kilómetro. Cuerpo y afectación se rellenan.
public sealed record EnvioIncidencia(
	string Uuid,
	int IdTipoIncidencia,
	Guid? IdGravedad,
	int IdAfectacion,
	decimal Km,
	string FuenteKilometro,
	string Cuerpo,
	string Nota,
	DateTime FchCapturaCampo,
	Guid? IdSesionOrigen,
	decimal? Latitud = null,
	decimal? Longitud = null);

// YaExistia no es error: se perdió la respuesta anterior y el folio es el mismo.
public sealed record IncidenciaRegistrada(
	string Folio,
	DateTime FchRecepcionCentral,
	bool YaExistia);

public sealed record ResultadoEnvio
{
	private ResultadoEnvio(
		IncidenciaRegistrada? registrada,
		FamiliaErrorSincronizacion? familia,
		string? codigo,
		string? mensaje)
	{
		Registrada = registrada;
		Familia = familia;
		Codigo = codigo;
		Mensaje = mensaje;
	}

	public IncidenciaRegistrada? Registrada { get; }

	public FamiliaErrorSincronizacion? Familia { get; }

	// Se decide por el código, nunca por el mensaje.
	public string? Codigo { get; }

	public string? Mensaje { get; }

	public bool Exito => Registrada is not null;

	public static ResultadoEnvio Aceptada(IncidenciaRegistrada registrada) =>
		new(registrada, null, null, null);

	public static ResultadoEnvio Rechazada(
		FamiliaErrorSincronizacion familia,
		string codigo,
		string mensaje) =>
		new(null, familia, codigo, mensaje);
}
