using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Servicios;

/// <summary>
/// Por qué terminó la última sesión, para poder decírselo al operador al volver al acceso.
/// </summary>
/// <remarks>
/// <para>
/// La sesión puede cerrarse desde sitios que no son la pantalla de acceso: la ventana
/// offline que vence mientras se trabaja (JTT-1384), una revalidación que Jacob niega
/// (JTT-1383 CA 11) o un permiso retirado (JTT-1379 CA 6). En los tres casos el operador
/// acaba en el formulario de acceso, y sin esto llegaría sin explicación.
/// </para>
/// <para>
/// <b>El aviso se consume una sola vez.</b> Si quedara guardado, reaparecería en el
/// siguiente acceso correcto y diría algo que ya no es cierto.
/// </para>
/// </remarks>
public sealed class AvisoDeSesionTerminada
{
	private MotivoRechazoAcceso? _motivo;

	/// <summary>Deja constancia de por qué se cerró la sesión.</summary>
	public void Registrar(MotivoRechazoAcceso motivo) => _motivo = motivo;

	/// <summary>Devuelve el aviso pendiente, si lo hay, y lo descarta.</summary>
	public MotivoRechazoAcceso? Consumir()
	{
		var motivo = _motivo;
		_motivo = null;

		return motivo;
	}
}
