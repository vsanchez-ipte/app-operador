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

public sealed partial class CapturaViewModel
{
	// Aparte de MensajeError: quedarse en la cola sin señal es lo normal, no un error rojo.
	[ObservableProperty]
	public partial string? MensajeEnvio { get; set; }

	public bool HayMensajeEnvio => !string.IsNullOrEmpty(MensajeEnvio);

	// Primero se guarda y después se envía: un fallo nunca pierde lo capturado.
	private async Task IntentarEnviarRecienGuardadaAsync(string claveLocal)
	{
		try
		{
			var resultado = await _sincronizador.EnviarUnaAsync(claveLocal);
			MensajeEnvio = TextoDelEnvio(claveLocal, resultado);
		}
		catch (Exception) when (!Debugger.IsAttached)
		{
			// La incidencia ya está guardada: nada del envío puede tumbar la captura.
			MensajeEnvio = $"{claveLocal} quedó en la cola. Se enviará al recuperar la señal.";
		}
	}

	// Siempre la clave local: es lo que el operador puede buscar en la cola. Textos provisionales.
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
				// Sin esta rama caería en la del permiso y diría que no está autorizado.
				MotivoNoSincroniza.YaEnCurso =>
					$"{claveLocal} guardada. Saldrá al terminar la sincronización que está en curso.",
				_ => $"{claveLocal} guardada. Su cuenta no tiene autorizado sincronizar.",
			};
		}

		return resultado.FamiliaUltimoError switch
		{
			// Jacob no llegó a evaluarla: decir «no la aceptó» sería falso.
			FamiliaErrorSincronizacion.Tecnico =>
				$"{claveLocal} guardada. No se pudo contactar al CCO; se reintentará sola.",

			// Se muestra el mensaje de Jacob: sabe mejor qué corregir.
			FamiliaErrorSincronizacion.Funcional =>
				$"{claveLocal}: {resultado.MensajeUltimoError ?? "el CCO la rechazó. Revise la cola."}",

			_ => $"{claveLocal} quedó en la cola.",
		};
	}

	partial void OnMensajeEnvioChanged(string? value) => OnPropertyChanged(nameof(HayMensajeEnvio));
}
