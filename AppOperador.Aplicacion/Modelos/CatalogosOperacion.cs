using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.Modelos;

// Grado de cierre de la vía (Total, Parcial, Sin afectación), no el carril.
public sealed record AfectacionIncidencia(int Id, string Nombre)
{
	public override string ToString() => Nombre;
}

public sealed record CuerpoVia(string Clave, string Nombre)
{
	public override string ToString() => Nombre;
}

public sealed record CatalogosOperacion(
	// Fecha de descarga: las tablas de Jacob no guardan cuándo cambiaron.
	DateOnly Version,
	IReadOnlyList<TipoIncidencia> Tipos,
	IReadOnlyList<SeveridadIncidencia> Severidades,
	IReadOnlyList<AfectacionIncidencia> Afectaciones,
	IReadOnlyList<CuerpoVia> Cuerpos,
	LimitesEvidencia LimitesEvidencia)
{
	public static readonly CatalogosOperacion Vacio =
		new(default, [], [], [], [], LimitesEvidencia.Desconocidos);

	public bool EsUtilizable => Tipos.Count > 0 && Severidades.Count > 0;
}
