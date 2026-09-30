using System.Collections;
using System.Collections.Immutable;

namespace AppOperador.Domain.ValueObjects;

// Solo nace de una respuesta del servidor: la app no puede agregar ni quitar permisos.
public sealed class PermisosOperador : IReadOnlyCollection<string>
{
	public static readonly PermisosOperador Ninguno = new(ImmutableArray<string>.Empty);

	private readonly ImmutableArray<string> _permisos;

	private PermisosOperador(ImmutableArray<string> permisos)
	{
		_permisos = permisos;
	}

	public static PermisosOperador DelServidor(IEnumerable<string>? permisos)
	{
		if (permisos is null)
		{
			return Ninguno;
		}

		var normalizados = permisos
			.Where(permiso => !string.IsNullOrWhiteSpace(permiso))
			.Select(permiso => permiso.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(permiso => permiso, StringComparer.OrdinalIgnoreCase)
			.ToImmutableArray();

		return normalizados.IsEmpty ? Ninguno : new PermisosOperador(normalizados);
	}

	public int Count => _permisos.Length;

	public bool Contiene(string permiso) =>
		!string.IsNullOrWhiteSpace(permiso)
		&& _permisos.Contains(permiso.Trim(), StringComparer.OrdinalIgnoreCase);

	// La app no verifica la firma del token; solo evita conceder lo que el token no trae.
	public bool RespaldadosPor(IEnumerable<string>? modulosDelToken)
	{
		if (_permisos.IsEmpty)
		{
			return true;
		}

		if (modulosDelToken is null)
		{
			return false;
		}

		var respaldo = modulosDelToken
			.Where(modulo => !string.IsNullOrWhiteSpace(modulo))
			.Select(modulo => modulo.Trim())
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

		return _permisos.All(respaldo.Contains);
	}

	public IEnumerator<string> GetEnumerator() => ((IEnumerable<string>)_permisos).GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
