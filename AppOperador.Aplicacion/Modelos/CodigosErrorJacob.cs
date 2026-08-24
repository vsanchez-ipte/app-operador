using AppOperador.Domain.Enums;

namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Traduce los códigos de error del canal móvil de Jacob a la familia que decide el
/// comportamiento de la app (JTT-1401 CA 8 y 9).
/// </summary>
/// <remarks>
/// <para>
/// Vive en la capa de aplicación y no en el dominio porque <b>los códigos son contrato con
/// Jacob</b>, no reglas de negocio propias. Y no vive junto al cliente HTTP porque quien decide
/// qué hacer con cada familia es el caso de uso, no el transporte.
/// </para>
/// <para>
/// La lista es la del contrato publicado en <c>02-contrato-api-canal-movil.md</c> §7.
/// </para>
/// </remarks>
public static class CodigosErrorJacob
{
	/// <summary>
	/// Códigos funcionales: el registro es incorrecto y reenviarlo daría el mismo rechazo.
	/// </summary>
	private static readonly HashSet<string> Funcionales = new(StringComparer.OrdinalIgnoreCase)
	{
		"appincidencias.catalogo.invalido",
		"appincidencias.nota.requerida",
		"appincidencias.km.fueradecorredor",
		"appincidencias.validacion.campo",
		"appincidencias.permiso.revocado",
		"appincidencias.operador.ajeno",
		"appevidencias.formato.nopermitido",
		"appevidencias.archivo.demasiadogrande",
		"appevidencias.archivos.demasiados",
		"appevidencias.incidencia.noexiste",
	};

	/// <summary>
	/// Código que Jacob devuelve cuando el kilómetro no resolvió a exactamente una plaza.
	/// </summary>
	/// <remarks>
	/// <b>Es técnico a propósito, aunque suene a dato mal capturado.</b> No es culpa del operador:
	/// o al ambiente le falta cargar los rangos de las plazas, o hay rangos traslapados. Como
	/// funcional, el operador vería un rechazo que no puede arreglar por más que edite; como
	/// técnico, el pendiente se conserva y entra solo el día que se corrija el dato.
	/// </remarks>
	public const string PlazaNoResuelta = "appincidencias.plaza.noresuelta";

	/// <summary>
	/// Familia a la que pertenece un código.
	/// </summary>
	/// <remarks>
	/// <b>Lo desconocido se trata como técnico</b>, que es la opción prudente: un código nuevo
	/// que el servidor empiece a emitir se reintentará —gasto acotado por la espera creciente—
	/// en vez de dejar el registro parado para siempre esperando una corrección que nadie sabe
	/// que hace falta.
	/// </remarks>
	public static FamiliaErrorSincronizacion FamiliaDe(string? codigo) =>
		codigo is not null && Funcionales.Contains(codigo)
			? FamiliaErrorSincronizacion.Funcional
			: FamiliaErrorSincronizacion.Tecnico;

	/// <summary>Indica si un código impide reintentar sin que alguien corrija algo.</summary>
	public static bool EsFuncional(string? codigo) =>
		FamiliaDe(codigo) == FamiliaErrorSincronizacion.Funcional;
}
