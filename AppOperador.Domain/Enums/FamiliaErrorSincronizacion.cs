namespace AppOperador.Domain.Enums;

// Ninguna familia descarta el pendiente: solo deciden si la app reintenta sola.
public enum FamiliaErrorSincronizacion
{
	// El registro está mal; reenviarlo igual daría el mismo rechazo.
	Funcional = 1,

	// El registro está bien; falló la red o el servidor.
	Tecnico = 2,
}
