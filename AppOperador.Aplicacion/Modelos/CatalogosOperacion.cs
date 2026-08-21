namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Grado de cierre de la vía que provoca la incidencia (JTT-1394).
/// </summary>
/// <remarks>
/// <b>No es el carril afectado</b>, aunque el nombre de la tabla lo sugiera: los valores reales
/// son «Total», «Parcial» y «Sin afectación». La confusión viene de documentos previos y llevó a
/// diseñar mal el formulario una vez; el nombre de la etiqueta importa aquí.
/// </remarks>
/// <param name="Id">Identificador en el catálogo de Jacob.</param>
/// <param name="Nombre">Texto que ve el operador.</param>
public sealed record AfectacionIncidencia(int Id, string Nombre)
{
	public override string ToString() => Nombre;
}

/// <summary>
/// Cuerpo de la vía donde ocurrió la incidencia (JTT-1394).
/// </summary>
/// <remarks>
/// Lista cerrada de cuatro. <b>No sale de ninguna tabla</b>: hasta hoy los literales estaban
/// escritos a mano en la webapp y en el API por separado. El canal móvil los recibe del servidor
/// para que el operador y el CCO lean el mismo nombre del mismo cuerpo.
/// </remarks>
/// <param name="Clave">Lo que se guarda en la incidencia: <c>A</c>, <c>B</c>, <c>C</c> o <c>D</c>.</param>
/// <param name="Nombre">Texto que ve el operador.</param>
public sealed record CuerpoVia(string Clave, string Nombre)
{
	public override string ToString() => Nombre;
}

/// <summary>
/// Los catálogos que el formulario de campo necesita, tal como los entrega Jacob.
/// </summary>
/// <remarks>
/// <para>
/// <b>Llegan juntos en una sola descarga</b>, y no cada uno por su lado, porque lo que se sella
/// en una incidencia capturada sin conexión es <b>una sola</b> <see cref="Version"/>. Cuatro
/// versiones distintas no dirían nada útil al sincronizar.
/// </para>
/// <para>
/// Es también lo que se guarda localmente: el CA 2 pide conservar una copia para operar sin
/// conexión, y el CA 4 que se refresque al validar en línea o al sincronizar.
/// </para>
/// </remarks>
/// <param name="Version">
/// Fecha en que se descargó el catálogo. <b>No es la de su última modificación</b>: ninguna de
/// las tablas de Jacob tiene esa columna. Dice cuándo se bajó, que es lo que la incidencia
/// necesita sellar (CA 5).
/// </param>
public sealed record CatalogosOperacion(
	DateOnly Version,
	IReadOnlyList<TipoIncidencia> Tipos,
	IReadOnlyList<SeveridadIncidencia> Severidades,
	IReadOnlyList<AfectacionIncidencia> Afectaciones,
	IReadOnlyList<CuerpoVia> Cuerpos)
{
	/// <summary>Catálogo vacío, para cuando no hay copia local ni respuesta del servidor.</summary>
	public static readonly CatalogosOperacion Vacio =
		new(default, [], [], [], []);

	/// <summary>Indica si el catálogo sirve para capturar.</summary>
	/// <remarks>
	/// Sin tipos ni severidades no se puede armar una incidencia válida, así que un catálogo al
	/// que le falte cualquiera de los dos se trata como si no hubiera.
	/// </remarks>
	public bool EsUtilizable => Tipos.Count > 0 && Severidades.Count > 0;
}
