using System.Collections.ObjectModel;
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

/// <summary>Borradores: guardar, abrir, editar, convertir y eliminar (JTT-1399).</summary>
public sealed partial class CapturaViewModel
{
	[ObservableProperty]
	public partial string? BorradorEnEdicion { get; set; }

	/// <summary>Borradores guardados, listados bajo el formulario.</summary>
	public ObservableCollection<RegistroColaVista> Borradores { get; } = [];

	public bool HayBorradores => Borradores.Count > 0;

	[RelayCommand(CanExecute = nameof(PuedeRegistrar))]
	private async Task GuardarBorradorAsync()
	{
		// Un borrador es captura a medias, así que necesita la misma autorización que registrar.
		if (!_capacidades.Puede(CapacidadOperador.RegistrarIncidencia))
		{
			// Mismo caso que en el guardado: se refresca para que el aviso explique el bloqueo.
			NotificarAutorizacion();
			return;
		}

		// Un borrador se guarda tal cual esté: no se valida, porque su razón de ser es
		// permitir dejar la captura a medias sin perderla.
		MensajeError = null;

		if (BorradorEnEdicion is { } claveEnEdicion)
		{
			// Editar actualiza el que ya existe. Crear uno nuevo dejaría al operador con dos
			// borradores del mismo hecho cada vez que guardara su avance (CA 8, «editarlo»).
			await _incidencias.ActualizarBorradorAsync(
				claveEnEdicion, TipoSeleccionado, Kilometro, SeveridadSeleccionada, Nota.Trim());
			CancelarEdicionBorrador();
		}
		else
		{
			await _incidencias.GuardarBorradorAsync(
				TipoSeleccionado, Kilometro, SeveridadSeleccionada, Nota.Trim());
			LimpiarFormulario();
		}

		await RecargarBorradoresAsync();
	}

	/// <summary>
	/// Convierte el borrador abierto, delegando el CA 9 en el caso de uso.
	/// </summary>
	/// <remarks>
	/// <b>La pantalla no revalida por su cuenta.</b> El caso de uso es el único dueño de las
	/// validaciones de envío; aquí solo se traduce su respuesta a un aviso. Repetir las
	/// comprobaciones daría dos redacciones del mismo criterio, que es como se separan.
	/// </remarks>
	private async Task ConvertirBorradorAbiertoAsync(string clave)
	{
		var resultado = await _convertirBorrador.EjecutarAsync(
			clave,
			TipoSeleccionado,
			Kilometro,
			FuenteKilometro,
			SeveridadSeleccionada,
			Nota,
			posicionGps: FuenteKilometro == KilometerSource.GPS ? _posicionGps : null);

		if (resultado != ResultadoConversionBorrador.Convertido)
		{
			SenalarError(CampoDe(resultado), MensajeDe(resultado));

			// Si ya no existe, el formulario tiene que soltarlo: seguir editando un borrador
			// que desapareció deja al operador escribiendo sobre nada.
			if (resultado == ResultadoConversionBorrador.NoEncontrado)
			{
				BorradorEnEdicion = null;
				await RecargarBorradoresAsync();
			}

			return;
		}

		MensajeError = null;
		CancelarEdicionBorrador();
		await RecargarBorradoresAsync();

		// Convertir también crea una incidencia, así que también intenta salir en el momento.
		await IntentarEnviarRecienGuardadaAsync(clave);
	}

	/// <summary>Indica si el formulario está editando un borrador ya guardado.</summary>
	public bool EstaEditandoBorrador => BorradorEnEdicion is not null;

	/// <inheritdoc cref="TextoBotonPrimario" />
	public string TextoBotonSecundario =>
		EstaEditandoBorrador ? "Actualizar borrador" : "Guardar borrador";

	/// <summary>
	/// Carga un borrador en el formulario para seguir capturándolo (JTT-1399 CA 8, «abrirlo»).
	/// </summary>
	/// <remarks>
	/// <b>Se reutiliza el mismo formulario en vez de abrir otra pantalla.</b> Editar un borrador
	/// es exactamente capturar, y una segunda pantalla obligaría a mantener dos veces las mismas
	/// validaciones y el mismo diseño.
	/// </remarks>
	[RelayCommand]
	private async Task AbrirBorradorAsync(RegistroColaVista? vista)
	{
		if (vista is null)
		{
			return;
		}

		var borrador = await _incidencias.ObtenerBorradorAsync(vista.ClaveLocal);
		if (borrador is null)
		{
			// Pudo eliminarse desde otro punto, o pertenecer a otra sesión.
			MensajeError = MensajeBorradorNoEncontrado;
			await RecargarBorradoresAsync();
			return;
		}

		// Un borrador abierto desplaza a cualquier rechazado que estuviera en corrección: los
		// dos modos no coexisten, y con los dos encendidos el botón diría «Reenviar» y convertiría.
		RechazadaEnCorreccion = null;
		MotivoRechazoEnCorreccion = null;
		MensajeError = null;
		BorradorEnEdicion = borrador.ClaveLocal;

		// Sus evidencias vienen con él: se adjuntaron a este UUID y siguen siendo suyas.
		// Reponer el UUID no basta —la lista y el contador siguen los del formulario anterior,
		// que SoltarEvidencias dejó vacíos—, así que hay que releerlas del repositorio.
		_uuidParaEvidencias = borrador.Uuid;
		await RecargarEvidenciasAsync();

		TipoSeleccionado = Tipos.FirstOrDefault(t => t.Id == borrador.TipoId);
		SeveridadSeleccionada = Severidades.FirstOrDefault(s => s.Id == borrador.SeveridadId);
		Nota = borrador.Nota;

		// El kilómetro se repone tal cual se guardó, aunque esté a medio escribir, y como
		// manual: reponerlo no es una lectura del GPS por mucho que lo fuera al capturarlo.
		Kilometro = borrador.Kilometro ?? string.Empty;
		FuenteKilometro = KilometerSource.Manual;
		_posicionGps = null;
	}

	/// <summary>
	/// Abandona la edición sin tocar el borrador (JTT-1399 CA 8).
	/// </summary>
	/// <remarks>
	/// Sin esta salida, quien abriera un borrador por error quedaría atrapado: los dos botones
	/// actuarían sobre él y no habría forma de volver a capturar uno nuevo.
	/// </remarks>
	[RelayCommand]
	private void CancelarEdicionBorrador()
	{
		BorradorEnEdicion = null;
		MensajeError = null;
		LimpiarFormulario();
	}

	/// <summary>
	/// Elimina el borrador que se está editando (JTT-1399 CA 8, «eliminarlo»).
	/// </summary>
	[RelayCommand]
	private async Task EliminarBorradorAsync()
	{
		if (BorradorEnEdicion is not { } clave)
		{
			return;
		}

		await _incidencias.EliminarBorradorAsync(clave);
		CancelarEdicionBorrador();
		await RecargarBorradoresAsync();
	}

	private string MensajeDe(ResultadoConversionBorrador motivo) => motivo switch
	{
		ResultadoConversionBorrador.FaltaTipo => MensajeBorradorSinTipo,
		ResultadoConversionBorrador.FaltaSeveridad => MensajeBorradorSinSeveridad,
		ResultadoConversionBorrador.KilometroInvalido => MensajeKilometroInvalido,
		ResultadoConversionBorrador.NotaInsuficiente => MensajeDescripcionRequerida(),
		_ => MensajeBorradorNoEncontrado,
	};

	private async Task RecargarBorradoresAsync()
	{
		Borradores.Clear();
		foreach (var borrador in await _incidencias.ObtenerBorradoresAsync())
		{
			Borradores.Add(new RegistroColaVista(borrador));
		}

		OnPropertyChanged(nameof(HayBorradores));
	}

	partial void OnBorradorEnEdicionChanged(string? value)
	{
		OnPropertyChanged(nameof(EstaEditandoBorrador));
		OnPropertyChanged(nameof(TextoBotonPrimario));
		OnPropertyChanged(nameof(TextoBotonSecundario));
	}

	private static CampoCaptura? CampoDe(ResultadoConversionBorrador resultado) => resultado switch
	{
		ResultadoConversionBorrador.FaltaTipo => CampoCaptura.Tipo,
		ResultadoConversionBorrador.KilometroInvalido => CampoCaptura.Kilometro,
		ResultadoConversionBorrador.FaltaSeveridad => CampoCaptura.Severidad,
		ResultadoConversionBorrador.NotaInsuficiente => CampoCaptura.Nota,
		_ => null,
	};
}
