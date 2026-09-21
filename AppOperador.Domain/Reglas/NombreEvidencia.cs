namespace AppOperador.Domain.Reglas;

/// <summary>
/// Cómo se nombra una evidencia que el dispositivo entregó sin nombre útil (JTT-1398).
/// </summary>
/// <remarks>
/// <para>
/// <b>Existe porque en el CCO se veían GUID.</b> Al tomar una fotografía, el sistema operativo
/// entrega un archivo temporal nombrado con un identificador —<c>6441d2f9fb6f…jpg</c>— y la app
/// lo enviaba tal cual. Quien consulta la incidencia veía tres nombres ilegibles, y la columna
/// del servidor se llama <c>nombre_original</c> justamente para mostrar algo reconocible.
/// </para>
/// <para>
/// <b>Se nombra con la clave local y no con el folio.</b> El folio lo asigna el servidor al
/// sincronizar y aquí todavía no existe: la evidencia se adjunta antes de guardar, incluso sin
/// conexión. La clave local nace con el registro, <b>nunca cambia</b> —tampoco al convertir el
/// borrador (JTT-1399)— y es la misma que el operador ve en la app, así que el archivo se llama
/// igual en el dispositivo, en la pantalla y en el CCO. Con el folio harían falta dos nombres.
/// </para>
/// <para>
/// <b>La hora va en UTC</b>, la misma con la que el servidor sella <c>fch_alta</c>. Así el nombre
/// y la fila cuadran al compararlos. Es lo único discutible de este formato: al operador le
/// resultaría más natural su hora local, y cambiarlo es pasar aquí un instante ya convertido.
/// </para>
/// </remarks>
public static class NombreEvidencia
{
	/// <summary>Lo más largo que se admite de una extensión, punto incluido.</summary>
	/// <remarks>
	/// Un nombre puede traer cualquier cosa después del último punto. Se acota para no arrastrar
	/// medio nombre como si fuera extensión.
	/// </remarks>
	private const int LargoMaximoExtension = 10;

	/// <summary>
	/// Compone el nombre con el que se guarda y se envía la evidencia.
	/// </summary>
	/// <param name="claveLocal">Clave de la incidencia, <c>LOC-######</c>.</param>
	/// <param name="instanteUtc">Momento de la captura.</param>
	/// <param name="nombreDelSistema">Lo que entregó el dispositivo. Solo se usa su extensión.</param>
	/// <returns>
	/// <c>LOC-000123-181409.jpg</c>. Si no hay clave con la que componer, devuelve el nombre del
	/// sistema sin tocarlo: <b>un nombre feo es mejor que ninguno</b>.
	/// </returns>
	public static string Componer(
		string? claveLocal,
		DateTime instanteUtc,
		string? nombreDelSistema)
	{
		var clave = claveLocal?.Trim();

		if (string.IsNullOrEmpty(clave))
		{
			return nombreDelSistema ?? string.Empty;
		}

		return $"{clave}-{instanteUtc:HHmmss}{ExtensionDe(nombreDelSistema)}";
	}

	/// <summary>Extensión del nombre, en minúsculas y con su punto, o vacío si no tiene.</summary>
	private static string ExtensionDe(string? nombre)
	{
		if (string.IsNullOrWhiteSpace(nombre))
		{
			return string.Empty;
		}

		var punto = nombre.LastIndexOf('.');

		// Un punto al final no es extensión, y uno al principio es un archivo oculto.
		if (punto <= 0 || punto == nombre.Length - 1)
		{
			return string.Empty;
		}

		var extension = nombre[punto..];

		if (extension.Length > LargoMaximoExtension)
		{
			return string.Empty;
		}

		// Cualquier cosa que no sea letra o dígito descarta la extensión entera: lo que sigue al
		// punto tiene que parecerse a una, o vale más no ponerle ninguna.
		return extension[1..].All(char.IsLetterOrDigit)
			? extension.ToLowerInvariant()
			: string.Empty;
	}
}
