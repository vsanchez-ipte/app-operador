namespace AppOperador.Domain.Reglas;

/// <summary>
/// Decide si la nota de una incidencia alcanza para enviarla (JTT-1397 CA 1 a 5).
/// </summary>
/// <remarks>
/// <para>
/// Función pura del dominio, como <see cref="ReglaPrioridadSincronizacion"/>, y por el mismo
/// motivo: <b>recibe primitivos y no modelos de catálogo</b>. El dominio no conoce
/// <c>TipoIncidencia</c> —vive en la capa de aplicación— y no tiene por qué: lo que la regla
/// necesita saber es si <i>ese</i> tipo exige descripción, no cuál es.
/// </para>
/// <para>
/// <b>Estaba escrita dentro del ViewModel del formulario</b>, con su constante propia. Se
/// extrajo al descubrir que JTT-1399 CA 9 obliga a ejecutar las mismas validaciones al
/// convertir un borrador: con la regla en la pantalla, el segundo camino la habría duplicado o
/// —peor— se la habría saltado sin que nada lo delatara.
/// </para>
/// </remarks>
public static class ReglaNotaIncidencia
{
	/// <summary>
	/// Mínimo de caracteres útiles cuando el tipo exige descripción.
	/// </summary>
	/// <remarks>
	/// El backend revalida con este mismo mínimo (JTT-1397 CA 5). Si alguna vez cambia, cambia
	/// en los dos lados o la app va a dejar pasar capturas que el servidor rechace en el envío,
	/// cuando el operador ya no está frente al hecho.
	/// </remarks>
	public const int MinimoCaracteres = 8;

	/// <summary>Tope de caracteres de la nota (JTT-1393 CA 7).</summary>
	public const int MaximoCaracteres = 1000;

	/// <summary>
	/// Indica si la nota cumple para que la incidencia se pueda enviar.
	/// </summary>
	/// <param name="tipoExigeDescripcion">
	/// Bandera <c>exige_descripcion</c> del tipo elegido, tal como la publica el catálogo.
	/// <b>Nunca se decide por el nombre del tipo ni por su id</b>: el tipo «Otro» es 107 en
	/// local y puede ser otro en QA.
	/// </param>
	/// <param name="nota">Nota capturada, tal como la escribió el operador.</param>
	/// <remarks>
	/// Los espacios no cuentan para el mínimo: ocho espacios en blanco no son una descripción.
	/// Sí cuentan para el máximo, porque ahí lo que se protege es el tamaño de la columna.
	/// </remarks>
	public static bool EsSuficiente(bool tipoExigeDescripcion, string? nota)
	{
		var texto = nota?.Trim() ?? string.Empty;

		if (texto.Length > MaximoCaracteres)
		{
			return false;
		}

		return !tipoExigeDescripcion || texto.Length >= MinimoCaracteres;
	}
}
