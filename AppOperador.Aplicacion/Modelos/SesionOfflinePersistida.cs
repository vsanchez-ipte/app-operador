using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Lo que hay que guardar de una sesión para poder reanudarla sin conexión (JTT-1383).
/// </summary>
/// <remarks>
/// <para>
/// <b>No incluye la contraseña</b>, que nunca se guarda en ningún sitio (CA 3), ni el
/// token, que vive aparte en el almacenamiento seguro.
/// </para>
/// <para>
/// Sí incluye <see cref="MonotonicoAlValidar"/>, la lectura del contador del sistema en el
/// instante de validar. Sin ella no se puede saber cuánto tiempo pasó de verdad cuando el
/// reloj del dispositivo se mueve.
/// </para>
/// </remarks>
/// <param name="SessionId">Identificador de la sesión en Jacob, para trazas y revalidación.</param>
/// <param name="Operador">Nombre visible del operador.</param>
/// <param name="Rol">Rol funcional con el que ingresó.</param>
/// <param name="Unidad">Unidad con la que opera, con su identificador técnico.</param>
/// <param name="Permisos">Capacidades concedidas, que se vuelven a cotejar al restaurar.</param>
/// <param name="Vigencia">Ventana offline tal como la calculó el servidor.</param>
/// <param name="MonotonicoAlValidar">Contador monotónico en el instante de la validación.</param>
/// <param name="Instalacion">Versiones que muestra el perfil.</param>
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
	/// <summary>Reconstruye la sesión que ven las pantallas.</summary>
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
