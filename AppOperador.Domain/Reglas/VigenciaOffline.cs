namespace AppOperador.Domain.Reglas;

public sealed class VigenciaOffline
{
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

	public DateTime LastValidatedAtUtc { get; }

	public DateTime OfflineUntilUtc { get; }

	public TimeSpan Ventana => OfflineUntilUtc - LastValidatedAtUtc;

	public static VigenciaOffline Validada(DateTime instanteValidacionUtc)
	{
		InstanteUtc.Exigir(instanteValidacionUtc, nameof(instanteValidacionUtc));
		return new VigenciaOffline(instanteValidacionUtc);
	}

	// Se adopta la ventana del servidor sin recalcularla: el reloj del teléfono puede estar desfasado.
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

	public VigenciaOffline RenovarConExito(DateTime instanteRefreshUtc)
	{
		InstanteUtc.Exigir(instanteRefreshUtc, nameof(instanteRefreshUtc));
		return new VigenciaOffline(instanteRefreshUtc);
	}

	public VigenciaOffline RenovarConFallo() => this;

	// Límite excluyente: justo en OfflineUntilUtc la ventana ya venció.
	public bool EstaVigenteEn(DateTime instanteUtc)
	{
		InstanteUtc.Exigir(instanteUtc, nameof(instanteUtc));
		return instanteUtc < OfflineUntilUtc;
	}

	// Al reanudar sin conexión se usa esta y no EstaVigenteEn, que depende del reloj.
	public bool EstaVigenteTras(TranscursoOffline transcurso)
	{
		ArgumentNullException.ThrowIfNull(transcurso);
		return transcurso.CabeEn(Ventana);
	}

	public TimeSpan RestanteEn(DateTime instanteUtc)
	{
		InstanteUtc.Exigir(instanteUtc, nameof(instanteUtc));
		var restante = OfflineUntilUtc - instanteUtc;
		return restante > TimeSpan.Zero ? restante : TimeSpan.Zero;
	}
}
