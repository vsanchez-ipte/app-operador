using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

// Nunca lanza por un rechazo ni por falta de red: van dentro del resultado.
public interface IAccesoJacobClient
{
	// Recibe la contraseña en claro; la implementación la cifra antes de enviarla.
	Task<ResultadoPreauth> PreautenticarAsync(
		string email,
		string contrasena,
		CancellationToken cancelacion = default);

	// El desafío es de un solo uso y dura cinco minutos: si se rechaza, se repite el paso 1.
	Task<ResultadoLogin> CompletarAccesoAsync(
		string challengeId,
		string unidadId,
		CancellationToken cancelacion = default);

	// No poder avisar no es un fallo: el cierre local ocurre igual.
	Task<bool> CerrarSesionAsync(string accessToken, CancellationToken cancelacion = default);

	// Renueva la ventana, pero no emite un token nuevo.
	Task<ResultadoRevalidacion> RevalidarAsync(string accessToken, CancellationToken cancelacion = default);

	// Sonda liviana para uso frecuente; no confundir con RevalidarAsync.
	Task<ResultadoSondeo> ComprobarEnlaceAsync(string accessToken, CancellationToken cancelacion = default);
}
