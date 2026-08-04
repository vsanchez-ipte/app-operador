using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;

namespace AppOperador.Mobile.Mocks;

/// <summary>
/// Lista compartida de registros locales, para que el repositorio y la cola vean lo mismo.
/// </summary>
/// <remarks>
/// Ocupa el lugar que tendrá la base SQLite. Se registra como singleton para que una
/// incidencia guardada en Captura aparezca de inmediato en Cola, igual que en la maqueta.
/// </remarks>
public sealed class AlmacenRegistrosEnMemoria
{
	private readonly List<RegistroCola> _registros = [];
	private int _consecutivo = 673_526;

	/// <summary>Genera la siguiente clave local con la forma <c>LOC-######</c>.</summary>
	public string SiguienteClaveLocal() => $"LOC-{++_consecutivo:D6}";

	public void Agregar(RegistroCola registro) => _registros.Add(registro);

	/// <summary>Todos los registros, del más reciente al más antiguo.</summary>
	public IReadOnlyList<RegistroCola> Todos() =>
		_registros.AsEnumerable().Reverse().ToList();

	public IReadOnlyList<RegistroCola> PorEstado(EstadoSincronizacion estado) =>
		_registros.Where(r => r.Estado == estado).Reverse().ToList();

	/// <summary>
	/// Reemplaza un registro por otro con el mismo identificador local.
	/// </summary>
	/// <remarks>La clave local nunca cambia: es la identidad del registro.</remarks>
	public void Reemplazar(RegistroCola registro)
	{
		var indice = _registros.FindIndex(r => r.ClaveLocal == registro.ClaveLocal);
		if (indice >= 0)
		{
			_registros[indice] = registro;
		}
	}

	public int Contar(EstadoSincronizacion estado) => _registros.Count(r => r.Estado == estado);
}
