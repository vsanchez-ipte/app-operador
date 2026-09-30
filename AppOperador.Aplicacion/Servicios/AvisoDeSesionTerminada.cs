using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Servicios;

public sealed class AvisoDeSesionTerminada
{
	private MotivoRechazoAcceso? _motivo;

	public void Registrar(MotivoRechazoAcceso motivo) => _motivo = motivo;

	// Se consume una vez: si quedara, reaparecería en el siguiente acceso correcto.
	public MotivoRechazoAcceso? Consumir()
	{
		var motivo = _motivo;
		_motivo = null;

		return motivo;
	}
}
