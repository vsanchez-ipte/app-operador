using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.Modelos;

public sealed class SesionOperador
{
	public SesionOperador(
		string operador,
		string rol,
		string unidadVehicular,
		VigenciaOffline vigencia,
		PermisosOperador permisos,
		string versionAplicacion,
		DateOnly versionCatalogos,
		string sessionId = "")
	{
		SessionId = sessionId;
		Operador = operador;
		Rol = rol;
		UnidadVehicular = unidadVehicular;
		Vigencia = vigencia;
		Permisos = permisos;
		VersionAplicacion = versionAplicacion;
		VersionCatalogos = versionCatalogos;
	}

	public string SessionId { get; }

	public string Operador { get; }

	public string Rol { get; }

	public string UnidadVehicular { get; }

	public VigenciaOffline Vigencia { get; }

	public PermisosOperador Permisos { get; }

	public string VersionAplicacion { get; }

	public DateOnly VersionCatalogos { get; }
}
