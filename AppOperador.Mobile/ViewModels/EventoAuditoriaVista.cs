using System.Globalization;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Mobile.ViewModels;

// Se guarda en UTC y se muestra en hora local; la conversión va aquí y no en el XAML.
public sealed class EventoAuditoriaVista
{
	private const string SinDato = "—";

	public EventoAuditoriaVista(EventoAuditoria evento)
	{
		FechaHoraLocal = evento.InstanteUtc.ToLocalTime()
			.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
		Mensaje = evento.Mensaje;

		TextoOrigen = evento.Origen switch
		{
			OrigenAuditoria.Online => "en línea",
			OrigenAuditoria.Offline => "sin enlace",
			_ => string.Empty,
		};

		// Un dato ausente se muestra «—»; las líneas anteriores al esquema 10 no llevan contexto.
		Contexto = evento.Origen == OrigenAuditoria.Desconocido
			? string.Empty
			: string.Join("  ·  ",
				$"Usuario: {evento.Operador ?? SinDato}",
				$"Rol: {evento.Rol ?? SinDato}",
				$"Permiso: {Permisos(evento.Permiso)}",
				$"Unidad: {evento.UnidadClave ?? SinDato}",
				$"Sesión: {Vacio(evento.SesionId) ?? SinDato}");

		(TextoNivel, ColorFondoNivel, ColorTextoNivel) = evento.Nivel switch
		{
			NivelAuditoria.Advertencia => ("WARN", "#FBE9C8", "#8A6100"),
			_ => ("INFO", "#F7DCE6", "#92264F"),
		};
	}

	public string FechaHoraLocal { get; }

	public string TextoOrigen { get; }

	public bool HayOrigen => TextoOrigen.Length > 0;

	public string Contexto { get; }

	public bool HayContexto => Contexto.Length > 0;

	public string Mensaje { get; }

	public string TextoNivel { get; }

	public string ColorFondoNivel { get; }

	public string ColorTextoNivel { get; }

	// Con espacio tras la coma, para que el texto pueda partirse.
	private static string Permisos(string? lista) =>
		Vacio(lista)?.Replace(",", ", ") ?? SinDato;

	// Los recorridos simulados dejan la sesión vacía, no nula.
	private static string? Vacio(string? valor) =>
		string.IsNullOrWhiteSpace(valor) ? null : valor;
}
