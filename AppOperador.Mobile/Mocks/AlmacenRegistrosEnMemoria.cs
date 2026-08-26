using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;

namespace AppOperador.Mobile.Mocks;

/// <summary>
/// Lista compartida de registros locales, para que el repositorio y la cola vean lo mismo.
/// </summary>
/// <remarks>
/// <para>
/// Ocupa el lugar que tendrá la base SQLite. Se registra como singleton para que una
/// incidencia guardada en Captura aparezca de inmediato en Cola, igual que en la maqueta.
/// </para>
/// <para>
/// Cada registro se guarda con el operador que lo capturó y las consultas exigen uno
/// (JTT-1390 CA 7). Es lo mismo que hace la tabla real, que ya tenía esa columna: quien
/// entra después no ve la cola de quien salió, aunque los registros sigan ahí.
/// </para>
/// </remarks>
public sealed class AlmacenRegistrosEnMemoria
{
	private readonly List<(string Operador, RegistroCola Registro)> _registros = [];
	private int _consecutivo = 673_526;

	/// <summary>Genera la siguiente clave local con la forma <c>LOC-######</c>.</summary>
	public string SiguienteClaveLocal() => $"LOC-{++_consecutivo:D6}";

	public void Agregar(RegistroCola registro, string operador) => _registros.Add((operador, registro));

	/// <summary>Registros del operador indicado, del más reciente al más antiguo.</summary>
	public IReadOnlyList<RegistroCola> Todos(string? operador) =>
		Suyos(operador).Reverse().ToList();

	public IReadOnlyList<RegistroCola> PorEstado(EstadoSincronizacion estado, string? operador) =>
		Suyos(operador).Where(r => r.Estado == estado).Reverse().ToList();

	public int Contar(EstadoSincronizacion estado, string? operador) =>
		Suyos(operador).Count(r => r.Estado == estado);

	/// <summary>
	/// Reemplaza un registro por otro con el mismo identificador local.
	/// </summary>
	/// <remarks>
	/// La clave local nunca cambia: es la identidad del registro. El operador tampoco, para
	/// que un pendiente conserve a quién pertenece aunque lo sincronice otra sesión.
	/// </remarks>
	public void Reemplazar(RegistroCola registro)
	{
		var indice = _registros.FindIndex(r => r.Registro.ClaveLocal == registro.ClaveLocal);
		if (indice >= 0)
		{
			_registros[indice] = (_registros[indice].Operador, registro);
		}
	}

	/// <summary>
	/// Registros de un operador. Sin sesión no se ve ninguno.
	/// </summary>
	/// <remarks>
	/// No es que se hayan borrado: es que nadie tiene derecho a verlos hasta que alguien
	/// entre.
	/// </remarks>
	private IEnumerable<RegistroCola> Suyos(string? operador) =>
		operador is null
			? []
			: _registros.Where(r => r.Operador == operador).Select(r => r.Registro);
}
