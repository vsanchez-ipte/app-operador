namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Veredicto de la comprobación de ubicación: en qué estado está, si deja continuar y qué
/// puede hacer el operador si no.
/// </summary>
/// <remarks>
/// <para>
/// La correspondencia estado → acción está aquí, en un solo punto y sin dependencias, para
/// que se pueda probar sin dispositivo y para que ninguna pantalla la reinvente.
/// </para>
/// <para>
/// Como en <see cref="ResultadoAcceso"/>, negar el paso es un desenlace esperado y no una
/// excepción.
/// </para>
/// </remarks>
public sealed class ResultadoUbicacion
{
	private ResultadoUbicacion(EstadoUbicacion estado, bool permiteAcceder, AccionUbicacion accion)
	{
		Estado = estado;
		PermiteAcceder = permiteAcceder;
		Accion = accion;
	}

	/// <summary>Situación de la ubicación en el dispositivo.</summary>
	public EstadoUbicacion Estado { get; }

	/// <summary>
	/// Indica si el acceso puede continuar. Solo es verdadero con
	/// <see cref="EstadoUbicacion.Concedido"/>: JTT-279 lo declara prerrequisito (PR3).
	/// </summary>
	public bool PermiteAcceder { get; }

	/// <summary>Acción que la pantalla debe ofrecer para salir del bloqueo.</summary>
	public AccionUbicacion Accion { get; }

	/// <summary>Indica si hay algo que el operador pueda hacer desde la app.</summary>
	public bool HayAccion => Accion != AccionUbicacion.Ninguna;

	/// <summary>Construye el veredicto que corresponde a un estado.</summary>
	/// <remarks>
	/// El caso por defecto no es un descuido: un valor que esta versión no conoce se trata
	/// como fallo técnico y <b>nunca</b> concede el paso. Ante la duda, el prerrequisito
	/// obligatorio no se da por cumplido.
	/// </remarks>
	public static ResultadoUbicacion Para(EstadoUbicacion estado) => estado switch
	{
		EstadoUbicacion.Concedido =>
			new(estado, true, AccionUbicacion.Ninguna),

		// Todavía no hubo negativa: el sistema mostrará su diálogo.
		EstadoUbicacion.NoSolicitado =>
			new(estado, false, AccionUbicacion.SolicitarPermiso),

		// Hubo negativa, pero el sistema admite volver a preguntar.
		EstadoUbicacion.Rechazado =>
			new(estado, false, AccionUbicacion.SolicitarPermiso),

		// El diálogo ya no aparece: el único camino es la ficha de la app.
		EstadoUbicacion.BloqueadoPermanentemente =>
			new(estado, false, AccionUbicacion.AbrirAjustesDeLaApp),

		// El permiso no es el problema; el interruptor del sistema sí.
		EstadoUbicacion.ServicioDesactivado =>
			new(estado, false, AccionUbicacion.AbrirAjustesDeUbicacion),

		// No hay nada que activar: ofrecer un botón sería engañar al operador.
		EstadoUbicacion.NoDisponibleEnElDispositivo =>
			new(estado, false, AccionUbicacion.Ninguna),

		_ => new(EstadoUbicacion.ErrorAlConsultar, false, AccionUbicacion.Ninguna),
	};
}
