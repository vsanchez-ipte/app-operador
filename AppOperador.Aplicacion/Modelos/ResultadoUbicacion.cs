namespace AppOperador.Aplicacion.Modelos;

public sealed class ResultadoUbicacion
{
	private ResultadoUbicacion(EstadoUbicacion estado, bool permiteAcceder, AccionUbicacion accion)
	{
		Estado = estado;
		PermiteAcceder = permiteAcceder;
		Accion = accion;
	}

	public EstadoUbicacion Estado { get; }

	public bool PermiteAcceder { get; }

	public AccionUbicacion Accion { get; }

	public bool HayAccion => Accion != AccionUbicacion.Ninguna;

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

		// Un estado desconocido nunca concede el paso.
		_ => new(EstadoUbicacion.ErrorAlConsultar, false, AccionUbicacion.Ninguna),
	};
}
