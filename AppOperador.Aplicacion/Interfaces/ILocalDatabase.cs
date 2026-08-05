namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Ciclo de vida de la base de datos local.
/// </summary>
/// <remarks>
/// El nombre está fijado en inglés por el documento de arquitectura. El contrato no
/// menciona SQLite a propósito: la capa de aplicación no debe conocer el motor, solo que
/// existe un almacén local que hay que preparar antes de usarlo.
/// </remarks>
public interface ILocalDatabase
{
	/// <summary>
	/// Crea el archivo y las tablas si no existen y aplica las migraciones pendientes.
	/// </summary>
	/// <remarks>
	/// Es idempotente y seguro de llamar varias veces: las pantallas la invocan sin
	/// coordinarse entre ellas.
	/// </remarks>
	Task InicializarAsync(CancellationToken cancelacion = default);

	/// <summary>
	/// Versión de esquema aplicada al archivo local.
	/// </summary>
	/// <remarks>
	/// Permite comprobar en pruebas que una base creada con una versión anterior queda
	/// migrada, que es el criterio de "migración local" del documento de arquitectura.
	/// </remarks>
	Task<int> ObtenerVersionEsquemaAsync(CancellationToken cancelacion = default);

	/// <summary>Ruta del archivo de base de datos, para diagnóstico.</summary>
	string RutaArchivo { get; }
}
