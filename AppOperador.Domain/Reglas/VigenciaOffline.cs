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

	/// <summary>Instante del último login o refresh exitoso, en UTC.</summary>
	public DateTime LastValidatedAtUtc { get; }

	/// <summary>Instante hasta el que se admite trabajo sin conexión, en UTC.</summary>
	public DateTime OfflineUntilUtc { get; }

	/// <summary>
	/// Abre una ventana de vigencia a partir de un login o refresh exitoso.
	/// </summary>
	/// <param name="instanteValidacionUtc">Instante de la validación. Debe ser UTC.</param>
	/// <exception cref="ArgumentException">El instante no está expresado en UTC.</exception>
	public static VigenciaOffline Validada(DateTime instanteValidacionUtc)
	{
		ExigirUtc(instanteValidacionUtc, nameof(instanteValidacionUtc));
		return new VigenciaOffline(instanteValidacionUtc);
	}

	/// <summary>
	/// Devuelve la vigencia resultante de un refresh exitoso: recorre la ventana hasta
	/// el nuevo instante de validación.
	/// </summary>
	/// <exception cref="ArgumentException">El instante no está expresado en UTC.</exception>
	public VigenciaOffline RenovarConExito(DateTime instanteRefreshUtc)
	{
		ExigirUtc(instanteRefreshUtc, nameof(instanteRefreshUtc));
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
		ExigirUtc(instanteUtc, nameof(instanteUtc));
		return instanteUtc < OfflineUntilUtc;
	}

	/// <summary>Tiempo que resta de ventana, o <see cref="TimeSpan.Zero"/> si ya venció.</summary>
	/// <exception cref="ArgumentException">El instante no está expresado en UTC.</exception>
	public TimeSpan RestanteEn(DateTime instanteUtc)
	{
		ExigirUtc(instanteUtc, nameof(instanteUtc));
		var restante = OfflineUntilUtc - instanteUtc;
		return restante > TimeSpan.Zero ? restante : TimeSpan.Zero;
	}

	// Se exige Kind.Utc explícito: aceptar Unspecified dejaría entrar horas locales
	// sin que nada avise, y toda la regla se compara en UTC.
	private static void ExigirUtc(DateTime instante, string nombreParametro)
	{
		if (instante.Kind != DateTimeKind.Utc)
		{
			throw new ArgumentException(
				$"El instante debe estar expresado en UTC (DateTimeKind.Utc); se recibió {instante.Kind}.",
				nombreParametro);
		}
	}
}
