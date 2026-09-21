using System.Diagnostics;
using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppOperador.Mobile.ViewModels;

/// <summary>El intento de envío inmediato tras guardar y su aviso (JTT-1401, JTT-1406).</summary>
public sealed partial class CapturaViewModel
{
	/// <summary>
	/// Qué pasó al enviar la incidencia recién guardada, o <see langword="null"/> si no se ha
	/// guardado ninguna en esta pasada.
	/// </summary>
	/// <remarks>
	/// Va aparte de <see cref="MensajeError"/> a propósito: <b>no es un error</b>. Que una
	/// incidencia se quede en la cola por falta de señal es el funcionamiento normal en campo, y
	/// pintarlo en rojo enseñaría al operador a ignorar los mensajes rojos.
	/// </remarks>
	[ObservableProperty]
	public partial string? MensajeEnvio { get; set; }

	/// <summary>Indica si hay algo que decir sobre el último envío.</summary>
	public bool HayMensajeEnvio => !string.IsNullOrEmpty(MensajeEnvio);

	/// <summary>
	/// Intenta enviar a Jacob la incidencia que se acaba de guardar.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Guardar primero, enviar después, y siempre en ese orden.</b> La incidencia queda escrita
	/// en el dispositivo antes de tocar la red: si el envío falla, se cae, o el operador sale de la
	/// pantalla, lo capturado ya está a salvo y espera en la cola. Enviar antes de guardar
	/// convertiría cualquier fallo en pérdida de lo que el operador acaba de escribir.
	/// </para>
	/// <para>
	/// <b>Un fallo aquí no es un error de la captura.</b> Sin enlace, o con Jacob caído, la
	/// incidencia se queda pendiente y sale sola por el camino normal de reintentos; el operador
	/// solo necesita saber cuál de las dos cosas pasó.
	/// </para>
	/// </remarks>
	private async Task IntentarEnviarRecienGuardadaAsync(string claveLocal)
	{
		try
		{
			var resultado = await _sincronizador.EnviarUnaAsync(claveLocal);
			MensajeEnvio = TextoDelEnvio(claveLocal, resultado);
		}
		catch (Exception) when (!Debugger.IsAttached)
		{
			// Nada de lo que pueda fallar al enviar puede tumbar la captura: la incidencia ya
			// está guardada, que es lo que importa.
			MensajeEnvio = $"{claveLocal} quedó en la cola. Se enviará al recuperar la señal.";
		}
	}

	/// <summary>
	/// Qué se le dice al operador después de guardar (JTT-1401 CA 10).
	/// </summary>
	/// <remarks>
	/// <b>Siempre se nombra la clave local.</b> Es lo único que el operador puede volver a buscar
	/// en la cola, y con folio o sin él es su referencia hasta que Jacob conteste.
	/// <para>
	/// Textos provisionales: Producto no ha fijado los literales de esta pantalla.
	/// </para>
	/// </remarks>
	private static string TextoDelEnvio(string claveLocal, ResultadoSincronizacion resultado)
	{
		if (resultado.Confirmados == 1)
		{
			return $"{claveLocal} enviada al CCO.";
		}

		if (resultado.MotivoBloqueo is { } motivo)
		{
			return motivo switch
			{
				MotivoNoSincroniza.SinEnlaceConJacob =>
					$"{claveLocal} guardada sin conexión. Se enviará al recuperar la señal.",
				MotivoNoSincroniza.SinSesion =>
					$"{claveLocal} guardada. La sesión expiró: vuelva a ingresar para enviarla.",
				// Había una tanda corriendo, así que esta no se envió por su cuenta: sale con
				// las demás. Se nombra aparte porque, sin esta rama, caería en la del permiso y
				// le diría al operador que no está autorizado cuando sí lo está (JTT-1406 CA 6).
				MotivoNoSincroniza.YaEnCurso =>
					$"{claveLocal} guardada. Saldrá al terminar la sincronización que está en curso.",
				_ => $"{claveLocal} guardada. Su cuenta no tiene autorizado sincronizar.",
			};
		}

		// Falló el envío, y las dos familias significan cosas muy distintas para el operador:
		// una la puede corregir y la otra no depende de él.
		return resultado.FamiliaUltimoError switch
		{
			// Técnico: Jacob NO llegó a evaluarla —servidor caído, red que se cortó a medias—.
			// Decir «no la aceptó» sería mentir y mandaría al operador a revisar una captura
			// que está bien.
			FamiliaErrorSincronizacion.Tecnico =>
				$"{claveLocal} guardada. No se pudo contactar al CCO; se reintentará sola.",

			// Funcional: Jacob la evaluó y la rechazó. Se muestra SU mensaje, no uno propio:
			// el servidor sabe qué está mal —«el kilómetro 119.999 está fuera del corredor»— y
			// reescribirlo como «no se aceptó» obliga al operador a adivinar qué corregir.
			FamiliaErrorSincronizacion.Funcional =>
				$"{claveLocal}: {resultado.MensajeUltimoError ?? "el CCO la rechazó. Revise la cola."}",

			_ => $"{claveLocal} quedó en la cola.",
		};
	}

	partial void OnMensajeEnvioChanged(string? value) => OnPropertyChanged(nameof(HayMensajeEnvio));
}
