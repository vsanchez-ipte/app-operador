using AppOperador.Domain.Enums;

namespace AppOperador.Aplicacion.Modelos;

public sealed record ResultadoSincronizacion(
	int Confirmados,
	int Intentados,
	MotivoNoSincroniza? MotivoBloqueo,
	FamiliaErrorSincronizacion? FamiliaUltimoError = null,
	// El mensaje de Jacob se propaga tal cual: quien rechazó sabe mejor por qué.
	string? MensajeUltimoError = null,
	// En espera salen solos; por corregir no salen hasta que alguien los arregle.
	int OmitidosEnEspera = 0,
	int OmitidosPorCorregir = 0);

public enum MotivoNoSincroniza
{
	SinSesion = 1,

	SinEnlaceConJacob = 2,

	SinPermiso = 3,

	// No es un fallo: la tanda en curso ya atiende lo pendiente.
	YaEnCurso = 4,
}
