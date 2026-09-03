using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Servicios;

/// <summary>
/// Responde qué puede hacer el operador con la sesión que hay abierta (JTT-1385).
/// </summary>
/// <remarks>
/// <para>
/// Es la puerta única para preguntarlo. Las pantallas no leen los permisos ni saben qué código
/// concede qué: piden una capacidad por su nombre y reciben un sí o un no.
/// </para>
/// <para>
/// La decisión vive en <see cref="ReglaCapacidades"/>, que es una función pura y por eso
/// probable; esto solo le acerca la sesión vigente.
/// </para>
/// </remarks>
public sealed class CapacidadesDeLaSesion
{
	private readonly ISessionStore _sesiones;

	public CapacidadesDeLaSesion(ISessionStore sesiones)
	{
		_sesiones = sesiones;
	}

	/// <summary>
	/// Indica si la sesión actual autoriza la capacidad indicada.
	/// </summary>
	/// <remarks>
	/// Se consulta en el momento y no se guarda el resultado: la sesión puede cerrarse o
	/// vencer mientras la pantalla está abierta, y una respuesta guardada seguiría diciendo
	/// que sí.
	/// </remarks>
	public bool Puede(CapacidadOperador capacidad) =>
		ReglaCapacidades.Concede(_sesiones.Actual?.Permisos, capacidad);
}
