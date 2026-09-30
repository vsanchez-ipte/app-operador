using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.Modelos;

// Sin contraseña ni token. MonotonicoAlValidar permite medir el tiempo aunque se mueva el reloj.
public sealed record SesionOfflinePersistida(
	string SessionId,
	string Operador,
	string Rol,
	UnidadVehicular Unidad,
	PermisosOperador Permisos,
	VigenciaOffline Vigencia,
	TimeSpan MonotonicoAlValidar,
	DatosDeInstalacion Instalacion)
{
	public SesionOperador ComoSesionOperador() => new(
		operador: Operador,
		rol: Rol,
		unidadVehicular: Unidad.Clave,
		vigencia: Vigencia,
		permisos: Permisos,
		versionAplicacion: Instalacion.VersionAplicacion,
		versionCatalogos: Instalacion.VersionCatalogos,
		sessionId: SessionId);
}
