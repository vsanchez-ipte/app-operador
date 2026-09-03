using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Copia de diagnóstico de la base local, legible sin clave.
/// </summary>
/// <remarks>
/// <para>
/// Existe para que quien desarrolla y quien prueba puedan abrir la base con una herramienta
/// de escritorio y ver qué se guardó y cómo se relacionan las tablas. La base del dispositivo
/// está cifrada con SQLCipher y su clave vive en el almacén seguro del sistema, que no la
/// entrega: copiar el archivo no sirve de nada, y descifrarlo fuera del teléfono es imposible
/// por diseño. Por eso la conversión la hace la propia app.
/// </para>
/// <para>
/// <b>La copia queda sin cifrar.</b> El contrato no menciona SQLite ni SQLCipher —la capa de
/// aplicación no conoce el motor—, pero sí deja dicho que lo producido es legible por
/// cualquiera que tenga el archivo.
/// </para>
/// <para>
/// Solo se registra en paquetes compilados expresamente con esta capacidad; en un paquete
/// normal nadie resuelve esta interfaz.
/// </para>
/// </remarks>
public interface IExportadorBaseDatos
{
	/// <summary>
	/// Crea una copia legible de toda la base y la deja lista para guardarse.
	/// </summary>
	/// <param name="etiquetaAmbiente">
	/// A qué ambiente apunta el paquete. Solo se usa para nombrar el archivo, de modo que dos
	/// exportaciones de ambientes distintos no se confundan en la carpeta de descargas.
	/// </param>
	/// <returns>
	/// La copia ya validada. <b>Hay que liberarla siempre</b>: al hacerlo se borra el archivo
	/// temporal, y ese es el único momento en que se borra.
	/// </returns>
	/// <exception cref="ExportacionBaseDatosException">
	/// La copia no se pudo crear, o se creó y no pasó la comprobación. En ningún caso queda
	/// archivo temporal detrás.
	/// </exception>
	Task<ExportacionBaseDatos> ExportarAsync(
		string etiquetaAmbiente,
		CancellationToken cancelacion = default);
}
