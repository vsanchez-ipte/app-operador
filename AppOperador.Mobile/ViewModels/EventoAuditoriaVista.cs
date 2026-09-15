using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Mobile.ViewModels;

/// <summary>
/// Un <see cref="EventoAuditoria"/> preparado para mostrarse en la bitácora.
/// </summary>
/// <remarks>
/// El evento se guarda en UTC, pero al operador se le presenta en la hora de su
/// dispositivo (DA-10). La conversión vive aquí y no en el XAML: enlazar el instante UTC
/// con un formato de hora mostraría la hora equivocada sin que nada avise.
/// </remarks>
public sealed class EventoAuditoriaVista
{
	public EventoAuditoriaVista(EventoAuditoria evento)
	{
		HoraLocal = evento.InstanteUtc.ToLocalTime().ToString("HH:mm:ss");
		Mensaje = evento.Mensaje;

		// Desde dónde ocurrió (JTT-1392 CA 1): con enlace, sin enlace, o nada si la línea es
		// anterior a que se registrara.
		TextoOrigen = evento.Origen switch
		{
			OrigenAuditoria.Online => "en línea",
			OrigenAuditoria.Offline => "sin enlace",
			_ => string.Empty,
		};

		(TextoNivel, ColorFondoNivel, ColorTextoNivel) = evento.Nivel switch
		{
			NivelAuditoria.Advertencia => ("WARN", "#FBE9C8", "#8A6100"),
			_ => ("INFO", "#F7DCE6", "#92264F"),
		};
	}

	/// <summary>Hora local del evento, como la presenta la maqueta.</summary>
	public string HoraLocal { get; }

	public string TextoOrigen { get; }

	public bool HayOrigen => TextoOrigen.Length > 0;

	public string Mensaje { get; }

	public string TextoNivel { get; }

	public string ColorFondoNivel { get; }

	public string ColorTextoNivel { get; }
}
