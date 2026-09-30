using AppOperador.Domain.Enums;

namespace AppOperador.Aplicacion.Modelos;

public static class CodigosErrorJacob
{
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
		"appevidencias.archivo.noesta",
	};

	// Técnico a propósito: falta cargar o corregir rangos de plazas, y el operador no puede arreglarlo.
	public const string PlazaNoResuelta = "appincidencias.plaza.noresuelta";

	public const string ErrorTecnico = "appincidencias.error.tecnico";

	// Código propio (lo produce nginx con HTML). Técnico: sube solo cuando se levante el límite.
	public const string EvidenciaRechazadaPorProxy = "appevidencias.proxy.demasiadogrande";

	// Código propio y funcional: reintentar no devuelve el archivo.
	public const string EvidenciaSinArchivo = "appevidencias.archivo.noesta";

	// Lo desconocido es técnico: mejor reintentar con espera que dejarlo parado sin que nadie sepa.
	public static FamiliaErrorSincronizacion FamiliaDe(string? codigo) =>
		codigo is not null && Funcionales.Contains(codigo)
			? FamiliaErrorSincronizacion.Funcional
			: FamiliaErrorSincronizacion.Tecnico;

	public static bool EsFuncional(string? codigo) =>
		FamiliaDe(codigo) == FamiliaErrorSincronizacion.Funcional;
}
