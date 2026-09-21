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
	/// <summary>Lo que se muestra cuando la fila no tiene un dato: no lo hubo, no se olvidó.</summary>
	private const string SinDato = "—";

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

		// Quién y con qué (JTT-1392 CA 1): los cinco datos que la fila guarda además del
		// origen, cada uno con su etiqueta para que la línea se lea sola. Un rechazo sin sesión
		// —usuario que no existe, permiso retirado— no tiene rol, unidad ni sesión, y eso se
		// dice con «—» en vez de omitirlo. Las líneas anteriores al esquema 10 no guardan nada
		// de esto y no llevan la línea.
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

	/// <summary>Hora local del evento, como la presenta la maqueta.</summary>
	public string HoraLocal { get; }

	public string TextoOrigen { get; }

	public bool HayOrigen => TextoOrigen.Length > 0;

	/// <summary>Usuario, rol, permiso, unidad y sesión de la línea, etiquetados.</summary>
	public string Contexto { get; }

	public bool HayContexto => Contexto.Length > 0;

	public string Mensaje { get; }

	public string TextoNivel { get; }

	public string ColorFondoNivel { get; }

	public string ColorTextoNivel { get; }

	// La bitácora guarda la lista completa separada por comas; se presenta con espacio para
	// que el texto pueda partirse entre permisos.
	private static string Permisos(string? lista) =>
		Vacio(lista)?.Replace(",", ", ") ?? SinDato;

	// Los recorridos simulados dejan la sesión como cadena vacía, no nula.
	private static string? Vacio(string? valor) =>
		string.IsNullOrWhiteSpace(valor) ? null : valor;
}
