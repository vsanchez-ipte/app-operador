using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;

namespace AppOperador.Aplicacion.Modelos;

// Severidad no es Prioridad: la prioridad solo distingue lo crítico, y confundirlas ya fue un defecto.
public sealed record RegistroCola(
	string ClaveLocal,
	ClaseRegistro Clase,
	SyncPriority Prioridad,
	string Descripcion,
	string Kilometro,
	EstadoSincronizacion Estado,
	string? FolioCentral = null,
	string? Severidad = null,
	string? UltimoErrorCodigo = null,
	string? UltimoErrorMensaje = null,
	int Intentos = 0,
	DateTime? UltimoIntentoUtc = null,
	DateTime? CapturadaUtc = null,
	int EvidenciasSinEnviar = 0,
	DateTime? ReintentoEvidenciaUtc = null)
{
	// Vive aquí y no en la vista, que no tiene pruebas. Un borrador puede no tener severidad todavía.
	public string SeveridadLegible =>
		string.IsNullOrWhiteSpace(Severidad) ? "Sin severidad" : Severidad!.Trim();

	// El folio es lo que el CCO puede buscar; se muestra tal cual, sin interpretar su formato.
	public string ReferenciaPrincipal => TieneFolio ? FolioCentral! : ClaveLocal;

	// Contra vacío y no solo nulo: un folio vacío dejaría la referencia en blanco.
	public bool TieneFolio => !string.IsNullOrWhiteSpace(FolioCentral);

	// Solo fallidos: un pendiente puede arrastrar el código de un intento ya superado.
	public bool HayMotivoFallo => Estado == EstadoSincronizacion.Fallido;

	public string MotivoFallo => CodigosErrorJacob.EsFuncional(UltimoErrorCodigo)
		// Solo aquí se dice qué hacer: es el único caso en que esperar no sirve.
		? $"El CCO la rechazó: {Detalle} Corríjala: no saldrá sola."
		: $"No llegó al CCO: {Detalle}";

	// Solo rechazos funcionales: un fallo técnico se reintenta solo.
	public bool SePuedeCorregir =>
		Estado == EstadoSincronizacion.Fallido && CodigosErrorJacob.EsFuncional(UltimoErrorCodigo);

	// Solo fallos técnicos: un rechazo funcional no sale hasta que alguien lo corrija.
	public bool HayReintentoProgramado =>
		Estado == EstadoSincronizacion.Fallido
		&& !CodigosErrorJacob.EsFuncional(UltimoErrorCodigo)
		&& UltimoIntentoUtc is not null;

	// La espera crece con cada fallo; se publica el instante y la vista decide cómo mostrarlo.
	public DateTime? ReintentoUtc => HayReintentoProgramado
		? UltimoIntentoUtc!.Value + ReglaEsperaReintento.Para(Intentos)
		: null;

	// La misma decisión que la tanda. Enviando cuenta porque un envío a medias solo lo recupera una tanda.
	public bool TocaIntentarlo(DateTime ahoraUtc) => Estado switch
	{
		EstadoSincronizacion.Pendiente => true,
		EstadoSincronizacion.Enviando => true,
		EstadoSincronizacion.Fallido => ReintentoUtc is { } reintento && reintento <= ahoraUtc,
		EstadoSincronizacion.Sincronizado =>
			ReintentoEvidenciaUtc is { } evidencia && evidencia <= ahoraUtc,
		_ => false,
	};

	public bool HayEvidenciaSinEnviar =>
		Estado == EstadoSincronizacion.Sincronizado && EvidenciasSinEnviar > 0;

	public string AvisoEvidencia => !HayEvidenciaSinEnviar
		? string.Empty
		: EvidenciasSinEnviar == 1
			? "1 evidencia pendiente de enviar al CCO."
			: $"{EvidenciasSinEnviar} evidencias pendientes de enviar al CCO.";

	// El código es el último recurso, pero se muestra: es lo que se dicta por radio al CCO.
	private string Detalle
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(UltimoErrorMensaje))
			{
				var mensaje = UltimoErrorMensaje!.Trim();
				return mensaje.EndsWith('.') ? mensaje : $"{mensaje}.";
			}

			return string.IsNullOrWhiteSpace(UltimoErrorCodigo)
				// Lo guardado antes de registrar intentos no tiene motivo: se dice en vez de callar.
				? "no se registró el motivo."
				: $"código {UltimoErrorCodigo!.Trim()}.";
		}
	}
}

// Se reintentan por separado: una evidencia fallida no revierte una incidencia confirmada.
public enum ClaseRegistro
{
	Incidencia = 1,
	Evidencia = 2,
}
