using System.Collections;
using System.Collections.Immutable;

namespace AppOperador.Domain.ValueObjects;

/// <summary>
/// Capacidades funcionales que Jacob CCO concede a un operador (JTT-1379).
/// </summary>
/// <remarks>
/// <para>
/// <b>Solo puede nacer de una respuesta del servidor.</b> No hay constructor público ni
/// método para agregar, quitar o reemplazar un permiso: la única entrada es
/// <see cref="DelServidor"/>. Es el CA 7 —«la app no permite modificar localmente la lista
/// de permisos»— expresado en el tipo, no confiado a la disciplina de quien programe
/// después.
/// </para>
/// <para>
/// Una lista expuesta como <c>IReadOnlyList</c> no bastaba: es una vista, no una copia, y
/// quien conservara la <c>List</c> original podía mutarla y con ella la sesión. Aquí el
/// contenido se copia a un arreglo inmutable en la frontera.
/// </para>
/// <para>
/// La comparación es <b>insensible a mayúsculas</b>: Jacob emite los códigos en mayúsculas
/// (<c>APP_OPERADOR_MOVIL</c>), pero una diferencia de caja no debe convertirse en una
/// negativa de acceso.
/// </para>
/// </remarks>
public sealed class PermisosOperador : IReadOnlyCollection<string>
{
	/// <summary>Sin ninguna capacidad concedida.</summary>
	public static readonly PermisosOperador Ninguno = new(ImmutableArray<string>.Empty);

	private readonly ImmutableArray<string> _permisos;

	private PermisosOperador(ImmutableArray<string> permisos)
	{
		_permisos = permisos;
	}

	/// <summary>
	/// Adopta la lista tal como la devolvió Jacob CCO.
	/// </summary>
	/// <remarks>
	/// Normaliza sin interpretar: recorta espacios, descarta entradas vacías, elimina
	/// repetidos y ordena. Ordenar no es cosmético —hace que dos respuestas con los mismos
	/// permisos en distinto orden sean iguales— pero <b>no</b> agrega, traduce ni deduce
	/// ningún permiso: lo que no venga del servidor no existe.
	/// </remarks>
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

	/// <summary>Cuántas capacidades hay concedidas.</summary>
	public int Count => _permisos.Length;

	/// <summary>Indica si el operador tiene la capacidad indicada.</summary>
	public bool Contiene(string permiso) =>
		!string.IsNullOrWhiteSpace(permiso)
		&& _permisos.Contains(permiso.Trim(), StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Indica si cada permiso de la lista aparece entre los que el token trae firmados.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Es el cotejo del CA 8. Jacob emite el permiso funcional como claim <c>module</c>
	/// dentro del token, firmado con un secreto que la app no tiene. Un cuerpo de respuesta
	/// que conceda más de lo que el token respalda no puede aceptarse: o alguien lo alteró
	/// en tránsito, o el servidor y el token no coinciden. En cualquiera de los dos casos la
	/// sesión no debe abrirse.
	/// </para>
	/// <para>
	/// <b>Lo que esto no es.</b> La app no verifica la firma —no tiene el secreto—, así que
	/// esto no sustituye la validación del servidor, que es la que manda en cada petición.
	/// Lo que cierra es que la app conceda capacidades que su propio token no respalda.
	/// </para>
	/// <para>
	/// Una lista vacía está respaldada por cualquier token: no concede nada.
	/// </para>
	/// </remarks>
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
