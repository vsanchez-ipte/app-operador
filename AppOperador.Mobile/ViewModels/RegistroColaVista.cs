using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;

namespace AppOperador.Mobile.ViewModels;

// Formato y color del estado se resuelven aquí, donde se pueden probar, y no con convertidores en el XAML.
public sealed class RegistroColaVista
{
	public RegistroColaVista(RegistroCola registro)
	{
		Registro = registro;

		var clase = registro.Clase == ClaseRegistro.Incidencia ? "Incidencia" : "Evidencia local";

		// La severidad, no la prioridad: la prioridad solo distingue lo crítico.
		TextoDatos =
			$"{clase} / {registro.SeveridadLegible} / {registro.Descripcion} / KM {registro.Kilometro}";

		// Con fecha solo cuando no es de hoy.
		TextoHora = registro.CapturadaUtc is { } capturada
			? FormatearHora(capturada.ToLocalTime())
			: string.Empty;

		(TextoEstado, ColorFondoEstado, ColorTextoEstado) = registro.Estado switch
		{
			EstadoSincronizacion.Sincronizado => ("SINCRONIZADO", "#D8F0E0", "#1E7A46"),
			EstadoSincronizacion.Enviando => ("ENVIANDO", "#FBE9C8", "#8A6100"),
			EstadoSincronizacion.Fallido => ("FALLIDO", "#F8D7D5", "#A03028"),
			EstadoSincronizacion.Borrador => ("BORRADOR", "#E9E0CE", "#3A2F2A"),
			_ => ("PENDIENTE", "#F7DCE6", "#92264F"),
		};
	}

	public RegistroCola Registro { get; }

	public string ClaveLocal => Registro.ClaveLocal;

	public string ReferenciaPrincipal => Registro.ReferenciaPrincipal;

	public string TextoDatos { get; }

	public string TextoHora { get; }

	public bool MuestraHora => TextoHora.Length > 0;

	public bool MuestraCorregir => Registro.SePuedeCorregir;

	public string TextoEstado { get; }

	public string ColorFondoEstado { get; }

	public string ColorTextoEstado { get; }

	// Con folio, la clave local baja aquí: es lo que permite rastrear el registro en el dispositivo.
	public string TextoTrazabilidad => $"{Registro.ClaveLocal} · Visible en Incidencias";

	public bool MuestraTrazabilidad => Registro.TieneFolio;

	public string TextoMotivoFallo => Registro.MotivoFallo;

	public bool MuestraMotivoFallo => Registro.HayMotivoFallo;

	// La hora y no una cuenta atrás, en hora local: se compara con el reloj y no hay que repintar.
	public string TextoReintento
	{
		get
		{
			if (Registro.ReintentoUtc is not { } reintento)
			{
				return string.Empty;
			}

			// Ya venció: anunciar una hora pasada haría dudar de si la app sigue intentando.
			return reintento <= DateTime.UtcNow
				? "Listo para reintentar."
				: $"Reintento a las {reintento.ToLocalTime():HH:mm}.";
		}
	}

	public bool MuestraReintento => Registro.HayReintentoProgramado;

	public string TextoEvidenciaSinEnviar => Registro.AvisoEvidencia;

	public bool MuestraEvidenciaSinEnviar => Registro.HayEvidenciaSinEnviar;

	private static string FormatearHora(DateTime local) =>
		local.Date == DateTime.Now.Date
			? $"Capturada a las {local:HH:mm}."
			: $"Capturada el {local:dd/MM/yyyy} a las {local:HH:mm}.";
}
