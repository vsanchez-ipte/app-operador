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
/// Lo que el servidor admite como evidencia: formatos, tamaño y cuántos archivos (JTT-1398).
/// </summary>
/// <remarks>
/// <para>
/// <b>Los tres valores los publica el catálogo; la app no los codifica.</b> Producto no contestó
/// DA-14, así que hoy están asumidos —cuatro formatos, 5 MB y 3 archivos— y el servidor los
/// declara en cada descarga. Leerlos de ahí es lo que hace que corregirlos sea tocar un
/// <c>appsettings</c> del API y no publicar una versión nueva en las tiendas.
/// </para>
/// <para>
/// Es también lo que abarata <b>JTT-289</b>, que ya fijó ocho archivos de 15 MB con video: con
/// los límites leídos, es cambiar dos números del lado del servidor.
/// </para>
/// <para>
/// <b>Se valida con los mismos números que el servidor</b>, no con unos parecidos. Un archivo
/// que la app acepta y el servidor rechaza gasta datos móviles del operador para acabar en un
/// error que se podía haber dicho antes de subirlo.
/// </para>
/// </remarks>
/// <param name="FormatosPermitidos">Tipos MIME admitidos, tal como los nombra el servidor.</param>
/// <param name="TamanoMaximoMb">Tope por archivo, en megabytes.</param>
/// <param name="MaximoArchivosPorIncidencia">Cuántos archivos admite una incidencia.</param>
public sealed record LimitesEvidencia(
	IReadOnlyList<string> FormatosPermitidos,
	int TamanoMaximoMb,
	int MaximoArchivosPorIncidencia)
{
	/// <summary>Límites desconocidos, cuando todavía no se ha descargado el catálogo.</summary>
	/// <remarks>
	/// <b>No son «sin límite» ni un valor de reserva</b>: son la ausencia del dato. Mientras
	/// estén así no se puede adjuntar, igual que sin catálogo no se puede capturar. Inventar un
	/// tope aquí sería justo lo que este diseño evita.
	/// </remarks>
	public static readonly LimitesEvidencia Desconocidos = new([], 0, 0);

	/// <summary>Indica si el servidor declaró unos límites con los que se pueda validar.</summary>
	public bool EstanDefinidos =>
		FormatosPermitidos.Count > 0 && TamanoMaximoMb > 0 && MaximoArchivosPorIncidencia > 0;

	/// <summary>Tope por archivo en bytes, que es la unidad en la que se mide un archivo.</summary>
	public long TamanoMaximoBytes => (long)TamanoMaximoMb * 1024 * 1024;

	/// <summary>Indica si el servidor admite ese tipo MIME.</summary>
    /// <remarks>
    /// Se compara sin distinguir mayúsculas: el tipo lo determina el servidor por contenido y
    /// no hay garantía de con qué caja lo escriba.
    /// </remarks>
	public bool AdmiteFormato(string? tipoMime) =>
		!string.IsNullOrWhiteSpace(tipoMime)
		&& FormatosPermitidos.Any(f => string.Equals(f, tipoMime, StringComparison.OrdinalIgnoreCase));
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
/// <param name="LimitesEvidencia">
/// Lo que el servidor admite como evidencia (JTT-1398). Viaja con los catálogos, y no aparte,
/// porque se descarga y se sella igual que ellos.
/// </param>
public sealed record CatalogosOperacion(
	DateOnly Version,
	IReadOnlyList<TipoIncidencia> Tipos,
	IReadOnlyList<SeveridadIncidencia> Severidades,
	IReadOnlyList<AfectacionIncidencia> Afectaciones,
	IReadOnlyList<CuerpoVia> Cuerpos,
	LimitesEvidencia LimitesEvidencia)
{
	/// <summary>Catálogo vacío, para cuando no hay copia local ni respuesta del servidor.</summary>
	public static readonly CatalogosOperacion Vacio =
		new(default, [], [], [], [], LimitesEvidencia.Desconocidos);

	/// <summary>Indica si el catálogo sirve para capturar.</summary>
	/// <remarks>
	/// Sin tipos ni severidades no se puede armar una incidencia válida, así que un catálogo al
	/// que le falte cualquiera de los dos se trata como si no hubiera.
	/// </remarks>
	public bool EsUtilizable => Tipos.Count > 0 && Severidades.Count > 0;
}
