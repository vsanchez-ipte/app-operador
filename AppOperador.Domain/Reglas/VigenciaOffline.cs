namespace AppOperador.Domain.Reglas;

/// <summary>
/// Ventana durante la cual el operador puede seguir trabajando sin conexión.
/// </summary>
/// <remarks>
/// <para>
/// <c>OfflineUntilUtc = LastValidatedAtUtc + 8 horas</c>, donde
/// <see cref="LastValidatedAtUtc"/> es el instante del login o del refresh exitoso.
/// Ambos nombres están fijados en inglés por el documento de arquitectura.
/// </para>
/// <para>
/// El tipo es inmutable y no consulta el reloj: ni <see cref="DateTime.Now"/> ni
/// <see cref="DateTime.UtcNow"/> aparecen aquí. Todo instante entra por parámetro para
/// que la regla sea comprobable.
/// </para>
/// </remarks>
public sealed class VigenciaOffline
{
	/// <summary>Duración de la ventana offline.</summary>
	public static readonly TimeSpan Duracion = TimeSpan.FromHours(8);

	private VigenciaOffline(DateTime lastValidatedAtUtc)
	{
		LastValidatedAtUtc = lastValidatedAtUtc;
		OfflineUntilUtc = lastValidatedAtUtc + Duracion;
	}

	private VigenciaOffline(DateTime lastValidatedAtUtc, DateTime offlineUntilUtc)
	{
		LastValidatedAtUtc = lastValidatedAtUtc;
		OfflineUntilUtc = offlineUntilUtc;
	}

	/// <summary>Instante del último login o refresh exitoso, en UTC.</summary>
	public DateTime LastValidatedAtUtc { get; }

	/// <summary>Instante hasta el que se admite trabajo sin conexión, en UTC.</summary>
	public DateTime OfflineUntilUtc { get; }

	/// <summary>
	/// Anchura de la ventana, tal como la fijó quien la abrió.
	/// </summary>
	/// <remarks>
	/// Normalmente son las ocho horas de <see cref="Duracion"/>, pero cuando la ventana
	/// viene del servidor manda la suya. Sirve para medirla contra un transcurso en lugar
	/// de contra una fecha (JTT-1383).
	/// </remarks>
	public TimeSpan Ventana => OfflineUntilUtc - LastValidatedAtUtc;

	/// <summary>
	/// Abre una ventana de vigencia a partir de un login o refresh exitoso.
	/// </summary>
	/// <param name="instanteValidacionUtc">Instante de la validación. Debe ser UTC.</param>
	/// <exception cref="ArgumentException">El instante no está expresado en UTC.</exception>
	public static VigenciaOffline Validada(DateTime instanteValidacionUtc)
	{
		InstanteUtc.Exigir(instanteValidacionUtc, nameof(instanteValidacionUtc));
		return new VigenciaOffline(instanteValidacionUtc);
	}

	/// <summary>
	/// Adopta la ventana <b>tal como la calculó el servidor</b>, sin recalcularla.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Es la vía que debe usarse cuando la ventana viene de Jacob CCO (JTT-1382 CA 3 y CA 4):
	/// el servidor manda <c>lastValidatedAtUtc</c> y <c>offlineUntilUtc</c>, y la app los
	/// adopta. Volver a sumar ocho horas sobre el reloj del teléfono daría una ventana
	/// distinta en cuanto ese reloj esté desfasado, y el operador seguiría trabajando
	/// creyéndose en vigencia cuando el servidor ya lo dio por vencido.
	/// </para>
	/// <para>
	/// No se valida que la diferencia sean ocho horas exactas: la duración la decide el
	/// servidor y esta clase no está para discutírsela. Sí se exige que la ventana no vaya
	/// hacia atrás, porque eso solo puede ser un error de integración.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// Algún instante no está en UTC, o el fin de la ventana es anterior a la validación.
	/// </exception>
	public static VigenciaOffline DelServidor(DateTime lastValidatedAtUtc, DateTime offlineUntilUtc)
	{
		InstanteUtc.Exigir(lastValidatedAtUtc, nameof(lastValidatedAtUtc));
		InstanteUtc.Exigir(offlineUntilUtc, nameof(offlineUntilUtc));

		if (offlineUntilUtc < lastValidatedAtUtc)
		{
			throw new ArgumentException(
				"La ventana offline no puede terminar antes de la validación que la abre; " +
				$"se recibió {offlineUntilUtc:o} contra {lastValidatedAtUtc:o}.",
				nameof(offlineUntilUtc));
		}

		return new VigenciaOffline(lastValidatedAtUtc, offlineUntilUtc);
	}

	/// <summary>
	/// Devuelve la vigencia resultante de un refresh exitoso: recorre la ventana hasta
	/// el nuevo instante de validación.
	/// </summary>
	/// <exception cref="ArgumentException">El instante no está expresado en UTC.</exception>
	public VigenciaOffline RenovarConExito(DateTime instanteRefreshUtc)
	{
		InstanteUtc.Exigir(instanteRefreshUtc, nameof(instanteRefreshUtc));
		return new VigenciaOffline(instanteRefreshUtc);
	}

	/// <summary>
	/// Devuelve la vigencia resultante de un refresh fallido, que por regla de negocio
	/// no modifica ninguna de las dos fechas.
	/// </summary>
	public VigenciaOffline RenovarConFallo() => this;

	/// <summary>
	/// Indica si en <paramref name="instanteUtc"/> todavía se admite trabajo offline.
	/// </summary>
	/// <remarks>
	/// El límite es excluyente: justo en <see cref="OfflineUntilUtc"/> la ventana ya
	/// venció. El enunciado no fijó este extremo; se eligió el criterio habitual de
	/// "válido hasta" y queda cubierto por pruebas.
	/// </remarks>
	/// <exception cref="ArgumentException">El instante no está expresado en UTC.</exception>
	public bool EstaVigenteEn(DateTime instanteUtc)
	{
		InstanteUtc.Exigir(instanteUtc, nameof(instanteUtc));
		return instanteUtc < OfflineUntilUtc;
	}

	/// <summary>
	/// Indica si todavía se admite trabajo offline según un transcurso medido, en vez de
	/// según el reloj del dispositivo.
	/// </summary>
	/// <remarks>
	/// Es la comprobación que debe usarse al reanudar sin conexión (JTT-1383 CA 2, 5 y 6):
	/// <see cref="EstaVigenteEn"/> se fía del reloj, y el reloj se puede mover.
	/// </remarks>
	public bool EstaVigenteTras(TranscursoOffline transcurso)
	{
		ArgumentNullException.ThrowIfNull(transcurso);
		return transcurso.CabeEn(Ventana);
	}

	/// <summary>Tiempo que resta de ventana, o <see cref="TimeSpan.Zero"/> si ya venció.</summary>
	/// <exception cref="ArgumentException">El instante no está expresado en UTC.</exception>
	public TimeSpan RestanteEn(DateTime instanteUtc)
	{
		InstanteUtc.Exigir(instanteUtc, nameof(instanteUtc));
		var restante = OfflineUntilUtc - instanteUtc;
		return restante > TimeSpan.Zero ? restante : TimeSpan.Zero;
	}

}
