using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;

namespace AppOperador.Mobile.Mocks;

public sealed class AlmacenRegistrosEnMemoria
{
	private readonly List<(string Operador, RegistroCola Registro)> _registros = [];
	private int _consecutivo = 673_526;

	public string SiguienteClaveLocal() => $"LOC-{++_consecutivo:D6}";

	public void Agregar(RegistroCola registro, string operador) => _registros.Add((operador, registro));

	public IReadOnlyList<RegistroCola> Todos(string? operador) =>
		Suyos(operador).Reverse().ToList();

	public IReadOnlyList<RegistroCola> PorEstado(EstadoSincronizacion estado, string? operador) =>
		Suyos(operador).Where(r => r.Estado == estado).Reverse().ToList();

	public int Contar(EstadoSincronizacion estado, string? operador) =>
		Suyos(operador).Count(r => r.Estado == estado);

	public void Reemplazar(RegistroCola registro)
	{
		var indice = _registros.FindIndex(r => r.Registro.ClaveLocal == registro.ClaveLocal);
		if (indice >= 0)
		{
			_registros[indice] = (_registros[indice].Operador, registro);
		}
	}

	private IEnumerable<RegistroCola> Suyos(string? operador) =>
		operador is null
			? []
			: _registros.Where(r => r.Operador == operador).Select(r => r.Registro);
}
