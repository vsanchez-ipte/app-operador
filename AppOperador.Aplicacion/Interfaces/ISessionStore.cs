using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Sesión vigente del operador, disponible para todas las pantallas.
/// </summary>
/// <remarks>
/// El nombre está fijado en inglés por el documento de arquitectura. En la
/// implementación real los tokens van en <c>SecureStorage</c> y los metadatos en SQLite;
/// este contrato solo expone lo que la interfaz necesita mostrar.
/// </remarks>
public interface ISessionStore
{
	/// <summary>Sesión actual, o <see langword="null"/> si nadie ha ingresado.</summary>
	SesionOperador? Actual { get; }

	/// <summary>Registra la sesión recién abierta.</summary>
	void Guardar(SesionOperador sesion);

	/// <summary>Elimina la sesión local. No borra los registros pendientes.</summary>
	void Limpiar();
}
