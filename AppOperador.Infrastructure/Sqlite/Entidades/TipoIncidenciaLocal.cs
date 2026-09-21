using SQLite;

namespace AppOperador.Infrastructure.Sqlite.Entidades;

/// <summary>
/// Fila de <c>catalogo_tipo_incidencia</c>: copia local del catálogo de Jacob (JTT-1394 CA 2).
/// </summary>
/// <remarks>
/// <para>
/// Es una <b>caché</b>, no una fuente: se reemplaza entera con lo que devuelva
/// <c>GET /ITS/AppCatalogos/Vigentes</c> al validar en línea o al sincronizar (CA 4). Existe
/// para que el formulario funcione sin conexión, que es el CA 2.
/// </para>
/// <para>
/// <b>La llave dejó de ser texto en JTT-1394.</b> Antes era una clave inventada en la maqueta
/// —<c>OBJETO</c>, <c>VEHICULO</c>…— que no existe en ningún servidor; ahora es el entero
/// autonumérico de Jacob, que es el que viaja al sincronizar.
/// </para>
/// </remarks>
[Table("catalogo_tipo_incidencia")]
internal sealed class TipoIncidenciaLocal
{
	/// <summary>Identificador del tipo en Jacob.</summary>
	/// <remarks>
	/// <c>AutoIncrement</c> queda fuera a propósito: el valor lo asigna el servidor y aquí solo
	/// se copia.
	/// </remarks>
	[PrimaryKey]
	[Column("id")]
	public int Id { get; set; }

	/// <summary>Texto que ve el operador.</summary>
	[Column("nombre")]
	public string Nombre { get; set; } = string.Empty;

	/// <summary>Si obliga a capturar una nota mínima (JTT-1397).</summary>
	/// <remarks>
	/// La publica el catálogo y el backend revalida contra ella. Es lo que permite que ninguna
	/// capa de la app conozca la palabra «Otro».
	/// </remarks>
	[Column("exige_descripcion")]
	public bool ExigeDescripcion { get; set; }

	/// <summary>Orden de presentación en el desplegable.</summary>
	/// <remarks>
	/// El endpoint entrega los tipos ya ordenados por nombre; esto conserva ese orden para que
	/// la lista no cambie de posición entre aperturas.
	/// </remarks>
	[Column("orden")]
	public int Orden { get; set; }
}
