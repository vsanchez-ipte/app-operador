namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Fuente de tiempo de la aplicación.
/// </summary>
/// <remarks>
/// Existe para que ninguna regla llame a <see cref="DateTime.UtcNow"/> por su cuenta: si
/// lo hicieran, la vigencia de sesión no sería comprobable. El nombre está fijado en
/// inglés por el documento de arquitectura.
/// </remarks>
public interface IClock
{
	/// <summary>Instante actual en UTC.</summary>
	DateTime UtcAhora { get; }
}
